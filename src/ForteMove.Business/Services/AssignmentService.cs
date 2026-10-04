using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Time;
using ForteMove.Models.Assignments;
using ForteMove.Models.Common;
using ForteMove.Models.Drivers;
using ForteMove.Models.Scheduling;

namespace ForteMove.Business.Services
{
    public sealed class AssignmentService
    {
        private static readonly TimeSpan Turnaround = TimeSpan.FromMinutes(15);
        private readonly IAssignmentRepository repository;
        private readonly IClock clock;

        public AssignmentService(IAssignmentRepository repository) : this(repository, new SystemClock()) { }
        public AssignmentService(IAssignmentRepository repository, IClock clock)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            if (clock == null) throw new ArgumentNullException("clock");
            this.repository=repository; this.clock=clock;
        }

        public AssignmentQueueResult GetAssignmentQueue(AssignmentQueueQuery query)
        {
            DateTime date=(query==null||query.ServiceDate==DateTime.MinValue?clock.Today:query.ServiceDate).Date;
            AssignmentData data=repository.GetAssignmentData(date) ?? new AssignmentData();
            DateTime now=clock.OperationalNow;
            AssignmentQueueResult result=new AssignmentQueueResult{EvaluatedAtLocal=now};
            foreach(AssignmentTripCandidate review in data.Trips.Where(t=>t.RequiresReview).OrderBy(t=>t.ScheduledDepartureLocal)) result.ReviewTrips.Add(review);
            foreach(AssignmentTripCandidate trip in data.Trips.Where(t=>!t.RequiresReview&&t.Status==TripStatus.Unassigned&&t.ScheduledDepartureLocal>now)
                .OrderBy(t=>t.ScheduledDepartureLocal).ThenBy(t=>t.RouteCode,StringComparer.Ordinal).ThenBy(t=>t.TripCode,StringComparer.Ordinal))
            {
                AssignmentRecommendation recommendation=BuildRecommendation(trip,data.Buses,data.Drivers);
                result.Recommendations.Add(recommendation);
                if(recommendation.HasRecommendation)
                {
                    AssignmentResourceWindow window=new AssignmentResourceWindow{TripId=trip.TripId,StartLocal=trip.ScheduledDepartureLocal,FinishLocal=trip.ExpectedFinishLocal};
                    recommendation.Bus.Windows.Add(window); recommendation.Driver.Windows.Add(window);
                }
            }
            return result;
        }

        public AssignmentDetails GetDetails(long tripId) { return tripId<=0?null:repository.GetDetails(tripId); }

        public IList<AssignmentRecommendation> GetEligibleAlternatives(long tripId)
        {
            AssignmentDetails details=repository.GetDetails(tripId); if(details==null||details.Trip==null)return new List<AssignmentRecommendation>();
            AssignmentData data=repository.GetAssignmentData(details.Trip.ServiceDate);
            IList<AssignmentRecommendation> alternatives=new List<AssignmentRecommendation>();
            foreach(AssignmentBusCandidate bus in RankBuses(details.Trip,data.Buses).Take(20))
                foreach(AssignmentDriverCandidate driver in RankDrivers(details.Trip,bus,data.Drivers).Take(20))
                    alternatives.Add(CreateRecommendation(details.Trip,bus,driver));
            return alternatives.Take(50).ToList();
        }

        public ServiceResult<AssignmentSaveResult> ConfirmAssignments(ConfirmAssignmentsRequest request,long actorUserAccountId)
        {
            IList<ValidationError> errors=ValidateConfirmation(request,actorUserAccountId);
            if(errors.Count>0)return ServiceResult<AssignmentSaveResult>.Failure(errors);
            try{repository.ConfirmAssignments(request,actorUserAccountId,clock.OperationalNow,clock.UtcNow);return ServiceResult<AssignmentSaveResult>.Success(new AssignmentSaveResult{AssignmentCount=request.Items.Count});}
            catch(AssignmentPersistenceException ex){return ServiceResult<AssignmentSaveResult>.Failure(string.Empty,ex.Message);}
        }

        public ServiceResult<bool> ChangeAssignment(AssignmentMutationRequest request,long actorUserAccountId)
        {
            if(request==null||request.TripId<=0||!request.BusId.HasValue||!request.DriverProfileId.HasValue)return ServiceResult<bool>.Failure(string.Empty,"Select an eligible Driver and bus.");
            if(string.IsNullOrWhiteSpace(request.Reason))return ServiceResult<bool>.Failure("Reason","Explain why this assignment is being changed.");
            AssignmentDetails details=repository.GetDetails(request.TripId);
            if(details==null||details.Trip==null||details.CurrentAssignment==null)return ServiceResult<bool>.Failure(string.Empty,"The current assignment is no longer available.");
            if(details.Trip.Status!=TripStatus.Scheduled)return ServiceResult<bool>.Failure(string.Empty,"Only a Scheduled Trip can be reassigned in this release.");
            AssignmentData data=repository.GetAssignmentData(details.Trip.ServiceDate);
            AssignmentBusCandidate bus=data.Buses.FirstOrDefault(b=>b.BusId==request.BusId.Value);
            AssignmentDriverCandidate driver=data.Drivers.FirstOrDefault(d=>d.DriverProfileId==request.DriverProfileId.Value);
            if(bus==null||driver==null||!IsBusEligible(details.Trip,bus)||!IsDriverEligible(details.Trip,bus,driver))return ServiceResult<bool>.Failure(string.Empty,"The selected Driver and bus are no longer eligible for this Trip.");
            ConfirmAssignmentItem replacement=new ConfirmAssignmentItem{TripId=request.TripId,BusId=bus.BusId,DriverProfileId=driver.DriverProfileId,TripRowVersion=request.TripRowVersion,BusRowVersion=bus.RowVersion,DriverRowVersion=driver.DriverRowVersion,UserRowVersion=driver.UserRowVersion,StaffRowVersion=driver.StaffRowVersion,DecisionType=AssignmentDecisionType.Change,Reason=request.Reason.Trim(),Fingerprint=BuildFingerprint(details.Trip,bus,driver)};
            try{repository.ChangeAssignment(replacement,request.AssignmentRowVersion,actorUserAccountId,clock.OperationalNow,clock.UtcNow);return ServiceResult<bool>.Success(true);}
            catch(AssignmentPersistenceException ex){return ServiceResult<bool>.Failure(string.Empty,ex.Message);}
        }

        public ServiceResult<bool> RemoveAssignment(AssignmentMutationRequest request,long actorUserAccountId)
        {
            if(request==null||request.TripId<=0)return ServiceResult<bool>.Failure(string.Empty,"A valid Trip is required.");
            if(string.IsNullOrWhiteSpace(request.Reason))return ServiceResult<bool>.Failure("Reason","Explain why this assignment is being removed.");
            try{repository.RemoveAssignment(request,actorUserAccountId,clock.UtcNow);return ServiceResult<bool>.Success(true);}
            catch(AssignmentPersistenceException ex){return ServiceResult<bool>.Failure(string.Empty,ex.Message);}
        }

        public ServiceResult<bool> ResolveReview(long tripId,byte[] rowVersion,string note,long actorUserAccountId)
        {
            if(string.IsNullOrWhiteSpace(note))return ServiceResult<bool>.Failure("ReviewNote","Record the review outcome.");
            AssignmentDetails details = repository.GetDetails(tripId);
            if (details == null || details.Trip == null || !details.Trip.RequiresReview)
                return ServiceResult<bool>.Failure(string.Empty, "This Trip no longer requires review.");
            if (details.CurrentAssignment != null)
            {
                AssignmentData data = repository.GetAssignmentData(details.Trip.ServiceDate);
                AssignmentBusCandidate bus = data.Buses.FirstOrDefault(item => item.BusId == details.CurrentAssignment.BusId);
                AssignmentDriverCandidate driver = data.Drivers.FirstOrDefault(item => item.DriverProfileId == details.CurrentAssignment.DriverProfileId);
                if (bus == null || driver == null || !IsBusEligible(details.Trip, bus) || !IsDriverEligible(details.Trip, bus, driver))
                    return ServiceResult<bool>.Failure(string.Empty, "The current assignment is no longer eligible. Change or remove it before completing the review.");
            }
            try{repository.ResolveReview(tripId,rowVersion,note.Trim(),actorUserAccountId,clock.UtcNow);return ServiceResult<bool>.Success(true);}
            catch(AssignmentPersistenceException ex){return ServiceResult<bool>.Failure(string.Empty,ex.Message);}
        }

        private AssignmentRecommendation BuildRecommendation(AssignmentTripCandidate trip,IList<AssignmentBusCandidate> buses,IList<AssignmentDriverCandidate> drivers)
        {
            IList<AssignmentBusCandidate> eligibleBuses=RankBuses(trip,buses).ToList();
            if(eligibleBuses.Count==0)return new AssignmentRecommendation{Trip=trip,ExclusionReason=GetBusExclusion(trip,buses)};
            foreach(AssignmentBusCandidate bus in eligibleBuses)
            {
                AssignmentDriverCandidate driver=RankDrivers(trip,bus,drivers).FirstOrDefault();
                if(driver!=null)return CreateRecommendation(trip,bus,driver);
            }
            return new AssignmentRecommendation{Trip=trip,ExclusionReason="No available Driver has valid credentials, a compatible licence, and sufficient turnaround time."};
        }

        private IEnumerable<AssignmentBusCandidate> RankBuses(AssignmentTripCandidate trip,IEnumerable<AssignmentBusCandidate> buses)
        {
            return (buses??Enumerable.Empty<AssignmentBusCandidate>()).Where(b=>IsBusEligible(trip,b))
                .OrderByDescending(b=>trip.PreferredBusCategoryId.HasValue&&b.BusCategoryId==trip.PreferredBusCategoryId.Value)
                .ThenBy(b=>trip.ExpectedCapacity.HasValue?b.PassengerCapacity-trip.ExpectedCapacity.Value:0)
                .ThenBy(b=>b.Windows.Count(w=>w.StartLocal.Date==trip.ServiceDate.Date)).ThenBy(b=>b.FleetNumber,StringComparer.Ordinal);
        }

        private IEnumerable<AssignmentDriverCandidate> RankDrivers(AssignmentTripCandidate trip,AssignmentBusCandidate bus,IEnumerable<AssignmentDriverCandidate> drivers)
        {
            foreach(AssignmentDriverCandidate d in drivers??Enumerable.Empty<AssignmentDriverCandidate>()) d.RouteFamiliarityCount=repository.GetRouteFamiliarity(d.DriverProfileId,trip.RouteId);
            return (drivers??Enumerable.Empty<AssignmentDriverCandidate>()).Where(d=>IsDriverEligible(trip,bus,d))
                .OrderBy(d=>d.Windows.Count(w=>w.StartLocal.Date==trip.ServiceDate.Date))
                .ThenBy(d=>d.Windows.Count==0?0:1)
                .ThenBy(d=>d.Windows.Where(w=>w.FinishLocal<=trip.ScheduledDepartureLocal).Select(w=>(DateTime?)w.FinishLocal).Max()??DateTime.MinValue)
                .ThenByDescending(d=>d.RouteFamiliarityCount).ThenBy(d=>d.EmployeeNumber,StringComparer.Ordinal);
        }

        private static bool IsBusEligible(AssignmentTripCandidate trip,AssignmentBusCandidate bus)
        {
            return bus!=null&&string.Equals(bus.OperationalState,"Operational",StringComparison.Ordinal)&&bus.GrossVehicleMassKg.HasValue&&bus.GrossVehicleMassKg.Value>0
                &&(!trip.ExpectedCapacity.HasValue||bus.PassengerCapacity>=trip.ExpectedCapacity.Value)
                &&bus.LicenceExpiryDate.Date>=trip.ExpectedFinishLocal.Date&&bus.RoadworthyExpiryDate.Date>=trip.ExpectedFinishLocal.Date&&bus.InsuranceExpiryDate.Date>=trip.ExpectedFinishLocal.Date
                &&!bus.Windows.Any(w=>w.TripId!=trip.TripId&&Conflicts(trip,w));
        }

        private static bool IsDriverEligible(AssignmentTripCandidate trip,AssignmentBusCandidate bus,AssignmentDriverCandidate driver)
        {
            return driver!=null&&driver.AccountIsActive&&driver.RoleIsActive&&string.Equals(driver.EmploymentStatus,"Active",StringComparison.Ordinal)
                &&driver.AvailabilityStatus==DriverAvailabilityStatus.Available&&driver.DateOfBirth.Date.AddYears(21)<=trip.ServiceDate.Date
                &&driver.LicenceExpiryDate.Date>=trip.ExpectedFinishLocal.Date&&driver.PrdpExpiryDate.Date>=trip.ExpectedFinishLocal.Date
                &&LicenceCompatible(bus.GrossVehicleMassKg.Value,driver.LicenceCode)&&!driver.Windows.Any(w=>w.TripId!=trip.TripId&&Conflicts(trip,w));
        }

        private static bool LicenceCompatible(int gvm,DriverLicenceCode code)
        {
            if(gvm<=3500)return true;
            if(gvm<=16000)return code==DriverLicenceCode.C1||code==DriverLicenceCode.C||code==DriverLicenceCode.EC1||code==DriverLicenceCode.EC;
            return code==DriverLicenceCode.C||code==DriverLicenceCode.EC;
        }
        private static bool Conflicts(AssignmentTripCandidate trip,AssignmentResourceWindow w){return w.StartLocal<trip.ExpectedFinishLocal.Add(Turnaround)&&w.FinishLocal.Add(Turnaround)>trip.ScheduledDepartureLocal;}
        private static AssignmentRecommendation CreateRecommendation(AssignmentTripCandidate trip,AssignmentBusCandidate bus,AssignmentDriverCandidate driver){return new AssignmentRecommendation{Trip=trip,Bus=bus,Driver=driver,Explanation="Recommended for capacity, compliance, licence compatibility and current workload.",Fingerprint=BuildFingerprint(trip,bus,driver)};}
        private static string BuildFingerprint(AssignmentTripCandidate t,AssignmentBusCandidate b,AssignmentDriverCandidate d){string raw=t.TripId+":"+Convert.ToBase64String(t.RowVersion??new byte[0])+":"+b.BusId+":"+Convert.ToBase64String(b.RowVersion??new byte[0])+":"+d.DriverProfileId+":"+Convert.ToBase64String(d.DriverRowVersion??new byte[0]);using(SHA256 s=SHA256.Create())return Convert.ToBase64String(s.ComputeHash(Encoding.UTF8.GetBytes(raw)));}
        private static string GetBusExclusion(AssignmentTripCandidate trip,IEnumerable<AssignmentBusCandidate> buses){if(!(buses??Enumerable.Empty<AssignmentBusCandidate>()).Any(b=>b.GrossVehicleMassKg.HasValue))return "No bus with an approved gross vehicle mass is available.";if(!(buses??Enumerable.Empty<AssignmentBusCandidate>()).Any(b=>string.Equals(b.OperationalState,"Operational",StringComparison.Ordinal)))return "No Operational bus is available.";if(trip.ExpectedCapacity.HasValue&&!(buses??Enumerable.Empty<AssignmentBusCandidate>()).Any(b=>b.PassengerCapacity>=trip.ExpectedCapacity.Value))return "No bus has sufficient passenger capacity.";return "No bus meets compliance and turnaround requirements.";}
        private static IList<ValidationError> ValidateConfirmation(ConfirmAssignmentsRequest request,long actor){IList<ValidationError> e=new List<ValidationError>();if(request==null||request.Items==null||request.Items.Count==0)e.Add(new ValidationError(string.Empty,"Select at least one recommendation."));else{if(request.Items.GroupBy(i=>i.TripId).Any(g=>g.Count()>1))e.Add(new ValidationError(string.Empty,"A Trip can only be selected once."));foreach(ConfirmAssignmentItem i in request.Items)if(i.DecisionType!=AssignmentDecisionType.RecommendationAccepted&&string.IsNullOrWhiteSpace(i.Reason))e.Add(new ValidationError("Reason","A reason is required for an alternative assignment."));}if(actor<=0)e.Add(new ValidationError(string.Empty,"A valid administrator is required."));return e;}
    }
}
