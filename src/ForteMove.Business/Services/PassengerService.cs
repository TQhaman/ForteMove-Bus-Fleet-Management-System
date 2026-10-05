using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Time;
using ForteMove.Models.Common;
using ForteMove.Models.Drivers;
using ForteMove.Models.Passengers;
using ForteMove.Models.Scheduling;

namespace ForteMove.Business.Services
{
    public sealed class PassengerService
    {
        public const decimal MaximumSupportedBalance = 9999999999.99m;
        private readonly IPassengerRepository repository;
        private readonly IClock clock;

        public PassengerService(IPassengerRepository repository) : this(repository, new SystemClock()) { }

        public PassengerService(IPassengerRepository repository, IClock clock)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            if (clock == null) throw new ArgumentNullException("clock");
            this.repository = repository;
            this.clock = clock;
        }

        public PassengerHomeDetails GetHome(long userAccountId)
        {
            return userAccountId <= 0 ? null : repository.GetHome(userAccountId, clock.OperationalNow);
        }

        public PassengerAccountDetails GetAccount(long userAccountId)
        {
            return userAccountId <= 0 ? null : repository.GetAccount(userAccountId);
        }

        public PassengerWalletDetails GetWallet(long userAccountId, int recentTransactionCount)
        {
            if (userAccountId <= 0) return null;
            int count = Math.Max(0, Math.Min(recentTransactionCount, 100));
            return repository.GetWallet(userAccountId, count);
        }

        public IList<JourneyRouteOption> GetJourneyOptions()
        {
            return repository.GetJourneyRoutes();
        }

        public ServiceResult<TopUpPreview> PreviewTopUp(long userAccountId, decimal? amount, Guid operationToken)
        {
            IList<ValidationError> errors = ValidateTopUpAmount(amount);
            PassengerWalletDetails wallet = userAccountId <= 0 ? null : repository.GetWallet(userAccountId, 0);
            if (wallet == null) errors.Add(new ValidationError(string.Empty, "The Passenger wallet is unavailable."));
            if (wallet != null && amount.HasValue && amount.Value > MaximumSupportedBalance - wallet.CurrentBalance)
                errors.Add(new ValidationError("Amount", "This top-up would exceed the supported wallet balance."));
            if (errors.Count > 0) return ServiceResult<TopUpPreview>.Failure(errors);

            Guid token = operationToken == Guid.Empty ? Guid.NewGuid() : operationToken;
            TopUpPreview preview = new TopUpPreview
            {
                Amount = amount.Value,
                BalanceBefore = wallet.CurrentBalance,
                BalanceAfter = wallet.CurrentBalance + amount.Value,
                WalletRowVersion = wallet.RowVersion,
                OperationToken = token
            };
            preview.Fingerprint = TopUpFingerprint(preview);
            return ServiceResult<TopUpPreview>.Success(preview);
        }

        public ServiceResult<TopUpResult> TopUpWallet(long userAccountId, TopUpRequest request)
        {
            if (request == null)
                return ServiceResult<TopUpResult>.Failure(string.Empty, "Top-up details are required.");
            if (request.OperationToken != Guid.Empty)
            {
                TopUpResult completed = repository.GetCompletedTopUp(userAccountId, request.OperationToken);
                if (completed != null)
                {
                    completed.WasAlreadyProcessed = true;
                    return ServiceResult<TopUpResult>.Success(completed);
                }
            }
            ServiceResult<TopUpPreview> current = PreviewTopUp(userAccountId, request.Amount, request.OperationToken);
            if (!current.Succeeded) return ServiceResult<TopUpResult>.Failure(current.Errors);
            if (!request.ReviewedBalanceBefore.HasValue || request.WalletRowVersion == null ||
                request.ReviewedBalanceBefore.Value != current.Value.BalanceBefore ||
                !BytesEqual(request.WalletRowVersion, current.Value.WalletRowVersion) ||
                !FixedEquals(request.PreviewFingerprint, current.Value.Fingerprint))
            {
                return ServiceResult<TopUpResult>.Failure("Preview", "Your wallet balance changed. Review the updated top-up before confirming.");
            }
            request.ClientIpAddress = NormalizeIp(request.ClientIpAddress);
            try { return ServiceResult<TopUpResult>.Success(repository.TopUpWallet(userAccountId, request, clock.UtcNow)); }
            catch (PassengerPersistenceException exception) { return ServiceResult<TopUpResult>.Failure(exception.Field, exception.Message); }
        }

        public ServiceResult<IList<JourneyListItem>> FindJourneys(JourneyQuery query)
        {
            query = query ?? new JourneyQuery();
            DateTime date = (query.ServiceDate ?? clock.Today).Date;
            if (date < clock.Today)
                return ServiceResult<IList<JourneyListItem>>.Failure("ServiceDate", "Choose today or a future service date.");
            query.ServiceDate = date;
            query.Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
            if (query.Search != null && query.Search.Length > 100)
                return ServiceResult<IList<JourneyListItem>>.Failure("Search", "Search cannot exceed 100 characters.");
            DateTime now = clock.OperationalNow;
            IList<JourneyListItem> items = repository.FindJourneyCandidates(query)
                .Select(item => ToListItem(item, EvaluateSale(item, now)))
                .Where(item => item != null)
                .OrderBy(item => item.ScheduledDepartureTime)
                .ThenBy(item => item.RouteCode)
                .ToList();
            return ServiceResult<IList<JourneyListItem>>.Success(items);
        }

        public ServiceResult<JourneyDetails> GetJourneyDetails(long userAccountId, long tripId, Guid operationToken)
        {
            if (userAccountId <= 0 || tripId <= 0)
                return ServiceResult<JourneyDetails>.Failure(string.Empty, "The journey could not be identified.");
            PassengerJourneyCandidate candidate = repository.GetJourneyCandidate(tripId);
            PassengerWalletDetails wallet = repository.GetWallet(userAccountId, 0);
            if (candidate == null || wallet == null)
                return ServiceResult<JourneyDetails>.Failure(string.Empty, "This journey is no longer available.");
            string reason = EvaluateSale(candidate, clock.OperationalNow);
            Guid token = operationToken == Guid.Empty ? Guid.NewGuid() : operationToken;
            JourneyDetails details = new JourneyDetails
            {
                Candidate = candidate,
                WalletBalance = wallet.CurrentBalance,
                BalanceAfterPurchase = wallet.CurrentBalance >= candidate.DefaultFare ? wallet.CurrentBalance - candidate.DefaultFare : wallet.CurrentBalance,
                RemainingSeats = Math.Max(0, candidate.BusPassengerCapacity - candidate.PurchasedTicketCount),
                WalletRowVersion = wallet.RowVersion,
                OperationToken = token,
                SaleUnavailableReason = reason
            };
            details.PreviewFingerprint = PurchaseFingerprint(details);
            return ServiceResult<JourneyDetails>.Success(details);
        }

        public ServiceResult<TicketPurchaseResult> PurchaseTicket(long userAccountId, PurchaseTicketRequest request)
        {
            if (request == null || request.TripId <= 0 || request.OperationToken == Guid.Empty)
                return ServiceResult<TicketPurchaseResult>.Failure(string.Empty, "The ticket purchase could not be identified.");
            TicketPurchaseResult completed = repository.GetCompletedPurchase(userAccountId, request.OperationToken);
            if (completed != null)
            {
                completed.WasAlreadyProcessed = true;
                return ServiceResult<TicketPurchaseResult>.Success(completed);
            }
            ServiceResult<JourneyDetails> current = GetJourneyDetails(userAccountId, request.TripId, request.OperationToken);
            if (!current.Succeeded) return ServiceResult<TicketPurchaseResult>.Failure(current.Errors);
            JourneyDetails details = current.Value;
            if (!details.CanPurchase)
                return ServiceResult<TicketPurchaseResult>.Failure(string.Empty, details.SaleUnavailableReason);
            if (request.ReviewedFare != details.Candidate.DefaultFare ||
                !BytesEqual(request.TripRowVersion, details.Candidate.TripRowVersion) ||
                !BytesEqual(request.RouteRowVersion, details.Candidate.RouteRowVersion) ||
                !BytesEqual(request.WalletRowVersion, details.WalletRowVersion) ||
                !FixedEquals(request.PreviewFingerprint, details.PreviewFingerprint))
            {
                return ServiceResult<TicketPurchaseResult>.Failure("Preview", "The journey, fare, or wallet balance changed. Review the updated details before purchasing.");
            }
            if (details.WalletBalance < details.Candidate.DefaultFare)
            {
                decimal shortfall = details.Candidate.DefaultFare - details.WalletBalance;
                return ServiceResult<TicketPurchaseResult>.Failure("Wallet", "Insufficient wallet balance. You need " + shortfall.ToString("C", CultureInfo.GetCultureInfo("en-ZA")) + " more to purchase this journey.");
            }
            request.ClientIpAddress = NormalizeIp(request.ClientIpAddress);
            try { return ServiceResult<TicketPurchaseResult>.Success(repository.PurchaseTicket(userAccountId, request, clock.OperationalNow, clock.UtcNow)); }
            catch (PassengerPersistenceException exception) { return ServiceResult<TicketPurchaseResult>.Failure(exception.Field, exception.Message); }
        }

        public IList<TicketListItem> GetTickets(long userAccountId, TicketListMode mode)
        {
            return userAccountId <= 0 ? new List<TicketListItem>() : repository.GetTickets(userAccountId, mode, clock.OperationalNow);
        }

        public TicketDetails GetTicketDetails(long userAccountId, long ticketId)
        {
            return userAccountId <= 0 || ticketId <= 0 ? null : repository.GetTicketDetails(userAccountId, ticketId);
        }

        public DateTime ToOperationalTime(DateTime utc)
        {
            return clock.ToOperationalTime(utc);
        }

        public DateTime OperationalToday
        {
            get { return clock.Today; }
        }

        public DateTime OperationalNow
        {
            get { return clock.OperationalNow; }
        }

        private static IList<ValidationError> ValidateTopUpAmount(decimal? amount)
        {
            IList<ValidationError> errors = new List<ValidationError>();
            if (!amount.HasValue || amount.Value <= 0)
                errors.Add(new ValidationError("Amount", "Enter a top-up amount greater than R0.00."));
            else if (amount.Value > MaximumSupportedBalance || decimal.Round(amount.Value, 2) != amount.Value)
                errors.Add(new ValidationError("Amount", "Enter a valid Rand amount with no more than two decimal places."));
            return errors;
        }

        private static string EvaluateSale(PassengerJourneyCandidate item, DateTime now)
        {
            if (item == null || !item.RouteIsActive || !item.ScheduleIsActive) return "This service is not active.";
            if (item.ScheduledDepartureLocal <= now) return "Ticket sales have closed for this departure.";
            if (!item.TripAssignmentId.HasValue) return "This service is not currently available for ticket purchases.";
            if (item.TripStatus != TripStatus.Scheduled && item.TripStatus != TripStatus.Ready && item.TripStatus != TripStatus.Delayed) return "This Trip is not open for ticket sales.";
            if (item.HasExecution) return "This Trip has already started.";
            if (item.RequiresReview) return "This service is temporarily unavailable for ticket purchases.";
            if (!item.DriverAccountIsActive || !item.DriverRoleIsActive || !string.Equals(item.DriverEmploymentStatus, "Active", StringComparison.Ordinal) || item.DriverAvailability != DriverAvailabilityStatus.Available) return "This service is not currently available for ticket purchases.";
            if (!item.DriverDateOfBirth.HasValue || item.DriverDateOfBirth.Value.AddYears(21).Date > item.ServiceDate.Date) return "This service is not currently available for ticket purchases.";
            DateTime finishDate = item.ExpectedFinishLocal.Date;
            if (!item.DriverLicenceExpiryDate.HasValue || item.DriverLicenceExpiryDate.Value.Date < finishDate || !item.DriverPrdpExpiryDate.HasValue || item.DriverPrdpExpiryDate.Value.Date < finishDate) return "This service is not currently available for ticket purchases.";
            if (!string.Equals(item.BusOperationalState, "Operational", StringComparison.Ordinal) || !item.BusGrossVehicleMassKg.HasValue || item.BusGrossVehicleMassKg.Value <= 0) return "This service is not currently available for ticket purchases.";
            if (item.ScheduleExpectedCapacity.HasValue && item.BusPassengerCapacity < item.ScheduleExpectedCapacity.Value) return "This service is not currently available for ticket purchases.";
            if (!item.BusLicenceExpiryDate.HasValue || item.BusLicenceExpiryDate.Value.Date < finishDate || !item.BusRoadworthyExpiryDate.HasValue || item.BusRoadworthyExpiryDate.Value.Date < finishDate || !item.BusInsuranceExpiryDate.HasValue || item.BusInsuranceExpiryDate.Value.Date < finishDate) return "This service is not currently available for ticket purchases.";
            if (!LicenceCompatible(item.DriverLicenceCode, item.BusGrossVehicleMassKg)) return "This service is not currently available for ticket purchases.";
            if (ForteMove.Business.Fleet.BusSafetyPolicy.MaintenanceBlocks(item.MaintenancePlans,now.Date,item.BusCurrentOdometer).Count>0) return "This service is temporarily unavailable for ticket purchases.";
            if (item.HasOpenCannotProceed) return "Ticket sales are temporarily unavailable for this service.";
            if (item.HasUnresolvedCriticalDefect) return "Ticket sales are temporarily unavailable for this service.";
            if (item.HasOperationalConflict) return "This service is not currently available for ticket purchases.";
            if (item.BusPassengerCapacity <= item.PurchasedTicketCount) return "This service is fully booked.";
            return null;
        }

        private static bool LicenceCompatible(DriverLicenceCode? code, int? gvm)
        {
            if (!code.HasValue || !gvm.HasValue) return false;
            if (gvm.Value <= 3500) return true;
            if (gvm.Value <= 16000) return code.Value == DriverLicenceCode.C1 || code.Value == DriverLicenceCode.C || code.Value == DriverLicenceCode.EC1 || code.Value == DriverLicenceCode.EC;
            return code.Value == DriverLicenceCode.C || code.Value == DriverLicenceCode.EC;
        }

        private static JourneyListItem ToListItem(PassengerJourneyCandidate item, string reason)
        {
            if (!string.IsNullOrEmpty(reason)) return null;
            return new JourneyListItem
            {
                TripId = item.TripId, TripCode = item.TripCode, RouteCode = item.RouteCode, RouteName = item.RouteName,
                OriginName = item.OriginName, DestinationName = item.DestinationName, ServiceDate = item.ServiceDate,
                ScheduledDepartureTime = item.ScheduledDepartureTime, ExpectedFinishLocal = item.ExpectedFinishLocal,
                TripStatus = item.TripStatus, EstimatedDelayMinutes = item.EstimatedDelayMinutes,
                Fare = item.DefaultFare, RemainingSeats = Math.Max(0, item.BusPassengerCapacity - item.PurchasedTicketCount)
            };
        }

        private static string TopUpFingerprint(TopUpPreview value)
        {
            return Hash(value.Amount.ToString("0.00", CultureInfo.InvariantCulture) + "|" + value.BalanceBefore.ToString("0.00", CultureInfo.InvariantCulture) + "|" + Convert.ToBase64String(value.WalletRowVersion) + "|" + value.OperationToken.ToString("D"));
        }

        private static string PurchaseFingerprint(JourneyDetails value)
        {
            return Hash(value.Candidate.TripId.ToString(CultureInfo.InvariantCulture) + "|" + value.Candidate.DefaultFare.ToString("0.00", CultureInfo.InvariantCulture) + "|" + Convert.ToBase64String(value.Candidate.TripRowVersion) + "|" + Convert.ToBase64String(value.Candidate.RouteRowVersion) + "|" + Convert.ToBase64String(value.WalletRowVersion) + "|" + value.OperationToken.ToString("D"));
        }

        private static string Hash(string value)
        {
            using (SHA256 hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            int difference = 0; for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i]; return difference == 0;
        }

        private static bool FixedEquals(string left, string right)
        {
            if (left == null || right == null) return false;
            return BytesEqual(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
        }

        private static string NormalizeIp(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string result = value.Trim(); return result.Length <= 45 ? result : result.Substring(0, 45);
        }
    }
}
