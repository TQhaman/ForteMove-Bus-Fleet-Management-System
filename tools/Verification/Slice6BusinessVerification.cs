using System;
using System.Collections.Generic;
using System.Linq;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Services;
using ForteMove.Business.Time;
using ForteMove.Models.Drivers;
using ForteMove.Models.Passengers;
using ForteMove.Models.Scheduling;

internal static class Slice6BusinessVerification
{
    private static int assertions;

    public static int Main()
    {
        try
        {
            DateTime now = new DateTime(2026, 10, 5, 12, 0, 0);
            FakeRepository repository = new FakeRepository();
            PassengerService service = new PassengerService(repository, new FixedClock(now));

            repository.Wallet.CurrentBalance = PassengerService.MaximumSupportedBalance - 0.50m;
            Assert(!service.PreviewTopUp(10, 0.501m, Guid.NewGuid()).Succeeded, "top-up precision");
            Assert(!service.PreviewTopUp(10, 0m, Guid.NewGuid()).Succeeded, "positive top-up");
            Assert(!service.PreviewTopUp(10, 0.51m, Guid.NewGuid()).Succeeded, "wallet overflow");
            Assert(service.PreviewTopUp(10, 0.50m, Guid.NewGuid()).Succeeded, "wallet maximum boundary");
            repository.CompletedTopUp = new TopUpResult { WalletTransactionCode="WTX-000001", BalanceAfter=10m };
            Assert(service.TopUpWallet(10,new TopUpRequest{Amount=1m,OperationToken=Guid.NewGuid()}).Succeeded,"top-up replay idempotency");
            repository.CompletedTopUp = null;

            PassengerJourneyCandidate candidate = Eligible(now);
            repository.Candidates = new List<PassengerJourneyCandidate> { candidate };
            Assert(service.FindJourneys(new JourneyQuery { ServiceDate = now.Date }).Value.Count == 1, "eligible sale");
            AssertRejected(service, repository, candidate, delegate(PassengerJourneyCandidate x) { x.ScheduledDepartureTime = now.TimeOfDay; }, "departure equal to now");
            AssertRejected(service, repository, candidate, delegate(PassengerJourneyCandidate x) { x.TripAssignmentId = null; }, "missing assignment");
            AssertRejected(service, repository, candidate, delegate(PassengerJourneyCandidate x) { x.TripStatus = TripStatus.InProgress; }, "started status");
            AssertRejected(service, repository, candidate, delegate(PassengerJourneyCandidate x) { x.RequiresReview = true; }, "review required");
            AssertRejected(service, repository, candidate, delegate(PassengerJourneyCandidate x) { x.DriverAccountIsActive = false; }, "inactive Driver");
            AssertRejected(service, repository, candidate, delegate(PassengerJourneyCandidate x) { x.DriverLicenceExpiryDate = now.Date; }, "expired Driver licence through finish");
            AssertRejected(service, repository, candidate, delegate(PassengerJourneyCandidate x) { x.BusOperationalState = "OutOfService"; }, "bus status");
            AssertRejected(service, repository, candidate, delegate(PassengerJourneyCandidate x) { x.BusInsuranceExpiryDate = now.Date; }, "bus compliance through finish");
            AssertRejected(service, repository, candidate, delegate(PassengerJourneyCandidate x) { x.HasOpenCannotProceed = true; }, "Cannot Proceed blocker");
            AssertRejected(service, repository, candidate, delegate(PassengerJourneyCandidate x) { x.HasUnresolvedCriticalDefect = true; }, "Critical defect blocker");
            AssertRejected(service, repository, candidate, delegate(PassengerJourneyCandidate x) { x.HasOperationalConflict = true; }, "operational conflict");
            AssertRejected(service, repository, candidate, delegate(PassengerJourneyCandidate x) { x.ScheduleExpectedCapacity = x.BusPassengerCapacity + 1; }, "Schedule expected-capacity suitability");
            AssertRejected(service, repository, candidate, delegate(PassengerJourneyCandidate x) { x.PurchasedTicketCount = x.BusPassengerCapacity; }, "assigned bus capacity");

            PassengerJourneyCandidate zeroFare = Clone(candidate); zeroFare.DefaultFare = 0m;
            repository.Candidate = zeroFare; repository.Wallet.CurrentBalance = 0m;
            var zeroPreview = service.GetJourneyDetails(10, zeroFare.TripId, Guid.NewGuid());
            Assert(zeroPreview.Succeeded && zeroPreview.Value.CanPurchase && zeroPreview.Value.BalanceAfterPurchase == 0m, "R0 Ticket preview");
            repository.CompletedPurchase = new TicketPurchaseResult { TicketId=55, TicketCode="TKT-000055" };
            Assert(service.PurchaseTicket(10,new PurchaseTicketRequest{TripId=zeroFare.TripId,OperationToken=Guid.NewGuid()}).Succeeded,"purchase replay idempotency");

            FakeRepository registrationRepository = new FakeRepository();
            PassengerRegistrationService registration = new PassengerRegistrationService(registrationRepository, new ForteMove.Business.Security.PasswordHasher(), new FixedClock(now));
            var registered = registration.Register(new RegisterPassengerRequest { FirstName = "  Palesa ", LastName = " Mokoena ", Email = "Passenger.Example@example.test", PhoneNumber = " 0123456789 ", Password = "a secure passenger passphrase", ConfirmPassword = "a secure passenger passphrase" });
            Assert(registered.Succeeded, "Passenger registration");
            Assert(registrationRepository.Registered.PasswordHash.Iterations == 600000 && registrationRepository.Registered.PasswordHash.Salt.Length == 32 && registrationRepository.Registered.PasswordHash.Hash.Length == 32, "shared password hashing");
            registrationRepository.DuplicateEmail = true;
            Assert(!registration.Register(new RegisterPassengerRequest { FirstName = "Palesa", LastName = "Mokoena", Email = "passenger.example@example.test", Password = "a secure passenger passphrase", ConfirmPassword = "a secure passenger passphrase" }).Succeeded, "duplicate email");

            Console.WriteLine("Slice 6 Business verification passed: " + assertions + " assertions.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    private static void AssertRejected(PassengerService service, FakeRepository repository, PassengerJourneyCandidate baseline, Action<PassengerJourneyCandidate> change, string name)
    {
        PassengerJourneyCandidate value = Clone(baseline); change(value); repository.Candidates = new List<PassengerJourneyCandidate> { value };
        Assert(service.FindJourneys(new JourneyQuery { ServiceDate = baseline.ServiceDate }).Value.Count == 0, name);
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Verification failed: " + name);
        assertions++;
    }

    private static PassengerJourneyCandidate Eligible(DateTime now)
    {
        return new PassengerJourneyCandidate
        {
            TripId=100,TripCode="TR-VERIFY",RouteId=20,RouteCode="FM-R99",RouteName="Verification Route",OriginName="Origin",DestinationName="Destination",
            ServiceDate=now.Date,ScheduledDepartureTime=now.TimeOfDay.Add(TimeSpan.FromHours(2)),ExpectedFinishLocal=now.Date.AddDays(1).AddHours(1),TripStatus=TripStatus.Scheduled,
            DefaultFare=15m,RouteIsActive=true,ScheduleIsActive=true,TripAssignmentId=200,DriverProfileId=300,BusId=400,
            DriverAccountIsActive=true,DriverRoleIsActive=true,DriverEmploymentStatus="Active",DriverAvailability=DriverAvailabilityStatus.Available,
            DriverDateOfBirth=now.Date.AddYears(-30),DriverLicenceCode=DriverLicenceCode.C,DriverLicenceExpiryDate=now.Date.AddDays(2),DriverPrdpExpiryDate=now.Date.AddDays(2),
            BusOperationalState="Operational",BusGrossVehicleMassKg=17000,BusPassengerCapacity=40,ScheduleExpectedCapacity=35,BusLicenceExpiryDate=now.Date.AddDays(2),BusRoadworthyExpiryDate=now.Date.AddDays(2),BusInsuranceExpiryDate=now.Date.AddDays(2),
            TripRowVersion=new byte[]{1,2,3,4,5,6,7,8},RouteRowVersion=new byte[]{8,7,6,5,4,3,2,1}
        };
    }

    private static PassengerJourneyCandidate Clone(PassengerJourneyCandidate x)
    {
        return new PassengerJourneyCandidate
        {
            TripId=x.TripId,TripCode=x.TripCode,RouteId=x.RouteId,RouteCode=x.RouteCode,RouteName=x.RouteName,OriginName=x.OriginName,DestinationName=x.DestinationName,ServiceDate=x.ServiceDate,ScheduledDepartureTime=x.ScheduledDepartureTime,ExpectedFinishLocal=x.ExpectedFinishLocal,TripStatus=x.TripStatus,DefaultFare=x.DefaultFare,RouteIsActive=x.RouteIsActive,ScheduleIsActive=x.ScheduleIsActive,RequiresReview=x.RequiresReview,HasExecution=x.HasExecution,EstimatedDelayMinutes=x.EstimatedDelayMinutes,TripAssignmentId=x.TripAssignmentId,DriverProfileId=x.DriverProfileId,BusId=x.BusId,DriverAccountIsActive=x.DriverAccountIsActive,DriverRoleIsActive=x.DriverRoleIsActive,DriverEmploymentStatus=x.DriverEmploymentStatus,DriverAvailability=x.DriverAvailability,DriverDateOfBirth=x.DriverDateOfBirth,DriverLicenceCode=x.DriverLicenceCode,DriverLicenceExpiryDate=x.DriverLicenceExpiryDate,DriverPrdpExpiryDate=x.DriverPrdpExpiryDate,BusOperationalState=x.BusOperationalState,BusGrossVehicleMassKg=x.BusGrossVehicleMassKg,BusPassengerCapacity=x.BusPassengerCapacity,ScheduleExpectedCapacity=x.ScheduleExpectedCapacity,BusLicenceExpiryDate=x.BusLicenceExpiryDate,BusRoadworthyExpiryDate=x.BusRoadworthyExpiryDate,BusInsuranceExpiryDate=x.BusInsuranceExpiryDate,HasOpenCannotProceed=x.HasOpenCannotProceed,HasUnresolvedCriticalDefect=x.HasUnresolvedCriticalDefect,HasOperationalConflict=x.HasOperationalConflict,PurchasedTicketCount=x.PurchasedTicketCount,TripRowVersion=x.TripRowVersion,RouteRowVersion=x.RouteRowVersion
        };
    }

    private sealed class FixedClock : IClock
    {
        private readonly DateTime now; public FixedClock(DateTime value){now=value;}
        public DateTime UtcNow{get{return DateTime.SpecifyKind(now.AddHours(-2),DateTimeKind.Utc);}}
        public DateTime Today{get{return now.Date;}}
        public DateTime OperationalNow{get{return now;}}
        public DateTime ToOperationalTime(DateTime utc){return utc.AddHours(2);}
    }

    private sealed class FakeRepository : IPassengerRepository
    {
        public PassengerWalletDetails Wallet=new PassengerWalletDetails{PassengerWalletId=1,CurrentBalance=100m,RowVersion=new byte[]{1,1,1,1,1,1,1,1}};
        public IList<PassengerJourneyCandidate> Candidates=new List<PassengerJourneyCandidate>(); public PassengerJourneyCandidate Candidate; public PassengerRegistrationAggregate Registered; public bool DuplicateEmail; public TopUpResult CompletedTopUp; public TicketPurchaseResult CompletedPurchase;
        public PassengerRegistrationResult RegisterPassenger(PassengerRegistrationAggregate value,DateTime utc){if(DuplicateEmail)throw new PassengerPersistenceException("An account with this email address already exists.","Email",null);Registered=value;return new PassengerRegistrationResult{UserAccountId=10,PassengerProfileId=11,PassengerWalletId=12};}
        public PassengerAccountDetails GetAccount(long id){return new PassengerAccountDetails{UserAccountId=id,FirstName="Test",LastName="Passenger"};}
        public PassengerWalletDetails GetWallet(long id,int count){return Wallet;}
        public IList<JourneyRouteOption> GetJourneyRoutes(){return new List<JourneyRouteOption>();}
        public IList<PassengerJourneyCandidate> FindJourneyCandidates(JourneyQuery query){return Candidates;}
        public PassengerJourneyCandidate GetJourneyCandidate(long tripId){return Candidate??Candidates.FirstOrDefault(x=>x.TripId==tripId);}
        public TopUpResult GetCompletedTopUp(long id,Guid token){return CompletedTopUp;}
        public TopUpResult TopUpWallet(long id,TopUpRequest request,DateTime utc){throw new NotSupportedException();}
        public TicketPurchaseResult GetCompletedPurchase(long id,Guid token){return CompletedPurchase;}
        public TicketPurchaseResult PurchaseTicket(long id,PurchaseTicketRequest request,DateTime local,DateTime utc){throw new NotSupportedException();}
        public IList<TicketListItem> GetTickets(long id,TicketListMode mode,DateTime local){return new List<TicketListItem>();}
        public TicketDetails GetTicketDetails(long id,long ticketId){return null;}
        public PassengerHomeDetails GetHome(long id,DateTime local){return null;}
    }
}
