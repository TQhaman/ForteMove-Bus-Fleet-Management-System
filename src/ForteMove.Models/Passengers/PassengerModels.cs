using System;
using System.Collections.Generic;
using ForteMove.Models.Drivers;
using ForteMove.Models.Security;
using ForteMove.Models.Scheduling;

namespace ForteMove.Models.Passengers
{
    public enum TicketStatus
    {
        Purchased,
        Refunded
    }

    public enum WalletTransactionType
    {
        SimulatedTopUp,
        TicketPurchase,
        TripCancellationRefund
    }

    public enum TicketListMode
    {
        Upcoming,
        History
    }

    public sealed class RegisterPassengerRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
        public string ClientIpAddress { get; set; }
    }

    public sealed class PassengerRegistrationAggregate
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string NormalizedEmail { get; set; }
        public string PhoneNumber { get; set; }
        public PasswordHash PasswordHash { get; set; }
        public string ClientIpAddress { get; set; }
    }

    public sealed class PassengerRegistrationResult
    {
        public long UserAccountId { get; set; }
        public long PassengerProfileId { get; set; }
        public long PassengerWalletId { get; set; }
    }

    public sealed class PassengerAccountDetails
    {
        public long PassengerProfileId { get; set; }
        public long UserAccountId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public bool AccountIsActive { get; set; }
        public byte[] ProfileRowVersion { get; set; }
    }

    public sealed class WalletTransactionItem
    {
        public long WalletTransactionId { get; set; }
        public string WalletTransactionCode { get; set; }
        public WalletTransactionType TransactionType { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public long? TicketId { get; set; }
        public string TicketCode { get; set; }
        public string RouteName { get; set; }
        public DateTime OccurredUtc { get; set; }
    }

    public sealed class PassengerWalletDetails
    {
        public PassengerWalletDetails()
        {
            RecentTransactions = new List<WalletTransactionItem>();
        }

        public long PassengerWalletId { get; set; }
        public decimal CurrentBalance { get; set; }
        public byte[] RowVersion { get; set; }
        public IList<WalletTransactionItem> RecentTransactions { get; set; }
    }

    public sealed class TopUpRequest
    {
        public decimal? Amount { get; set; }
        public decimal? ReviewedBalanceBefore { get; set; }
        public byte[] WalletRowVersion { get; set; }
        public Guid OperationToken { get; set; }
        public string PreviewFingerprint { get; set; }
        public string ClientIpAddress { get; set; }
    }

    public sealed class TopUpPreview
    {
        public decimal Amount { get; set; }
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public byte[] WalletRowVersion { get; set; }
        public Guid OperationToken { get; set; }
        public string Fingerprint { get; set; }
    }

    public sealed class TopUpResult
    {
        public string WalletTransactionCode { get; set; }
        public decimal BalanceAfter { get; set; }
        public bool WasAlreadyProcessed { get; set; }
    }

    public sealed class JourneyQuery
    {
        public DateTime? ServiceDate { get; set; }
        public long? RouteId { get; set; }
        public string Search { get; set; }
    }

    public sealed class JourneyRouteOption
    {
        public long RouteId { get; set; }
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
    }

    public sealed class PassengerJourneyCandidate
    {
        public long TripId { get; set; }
        public string TripCode { get; set; }
        public long RouteId { get; set; }
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public string OriginName { get; set; }
        public string DestinationName { get; set; }
        public DateTime ServiceDate { get; set; }
        public TimeSpan ScheduledDepartureTime { get; set; }
        public DateTime ExpectedFinishLocal { get; set; }
        public TripStatus TripStatus { get; set; }
        public decimal DefaultFare { get; set; }
        public bool RouteIsActive { get; set; }
        public bool ScheduleIsActive { get; set; }
        public bool RequiresReview { get; set; }
        public bool HasExecution { get; set; }
        public int? EstimatedDelayMinutes { get; set; }
        public long? TripAssignmentId { get; set; }
        public long? DriverProfileId { get; set; }
        public long? BusId { get; set; }
        public bool DriverAccountIsActive { get; set; }
        public bool DriverRoleIsActive { get; set; }
        public string DriverEmploymentStatus { get; set; }
        public DriverAvailabilityStatus? DriverAvailability { get; set; }
        public DateTime? DriverDateOfBirth { get; set; }
        public DriverLicenceCode? DriverLicenceCode { get; set; }
        public DateTime? DriverLicenceExpiryDate { get; set; }
        public DateTime? DriverPrdpExpiryDate { get; set; }
        public string BusOperationalState { get; set; }
        public int? BusGrossVehicleMassKg { get; set; }
        public int BusPassengerCapacity { get; set; }
        public int? ScheduleExpectedCapacity { get; set; }
        public DateTime? BusLicenceExpiryDate { get; set; }
        public DateTime? BusRoadworthyExpiryDate { get; set; }
        public DateTime? BusInsuranceExpiryDate { get; set; }
        public bool HasOpenCannotProceed { get; set; }
        public bool HasUnresolvedCriticalDefect { get; set; }
        public bool HasOperationalConflict { get; set; }
        public int PurchasedTicketCount { get; set; }
        public byte[] TripRowVersion { get; set; }
        public byte[] RouteRowVersion { get; set; }

        public DateTime ScheduledDepartureLocal
        {
            get { return ServiceDate.Date.Add(ScheduledDepartureTime); }
        }
    }

    public sealed class JourneyListItem
    {
        public long TripId { get; set; }
        public string TripCode { get; set; }
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public string OriginName { get; set; }
        public string DestinationName { get; set; }
        public DateTime ServiceDate { get; set; }
        public TimeSpan ScheduledDepartureTime { get; set; }
        public DateTime ExpectedFinishLocal { get; set; }
        public TripStatus TripStatus { get; set; }
        public int? EstimatedDelayMinutes { get; set; }
        public decimal Fare { get; set; }
        public int RemainingSeats { get; set; }
    }

    public sealed class JourneyDetails
    {
        public PassengerJourneyCandidate Candidate { get; set; }
        public decimal WalletBalance { get; set; }
        public decimal BalanceAfterPurchase { get; set; }
        public int RemainingSeats { get; set; }
        public byte[] WalletRowVersion { get; set; }
        public string PreviewFingerprint { get; set; }
        public Guid OperationToken { get; set; }
        public string SaleUnavailableReason { get; set; }
        public bool CanPurchase { get { return string.IsNullOrEmpty(SaleUnavailableReason); } }
    }

    public sealed class PurchaseTicketRequest
    {
        public long TripId { get; set; }
        public decimal ReviewedFare { get; set; }
        public byte[] TripRowVersion { get; set; }
        public byte[] RouteRowVersion { get; set; }
        public byte[] WalletRowVersion { get; set; }
        public Guid OperationToken { get; set; }
        public string PreviewFingerprint { get; set; }
        public string ClientIpAddress { get; set; }
    }

    public sealed class TicketPurchaseResult
    {
        public long TicketId { get; set; }
        public string TicketCode { get; set; }
        public string WalletTransactionCode { get; set; }
        public decimal FareAmount { get; set; }
        public decimal WalletBalanceAfter { get; set; }
        public bool WasAlreadyProcessed { get; set; }
    }

    public sealed class TicketQuery
    {
        public TicketListMode Mode { get; set; }
    }

    public sealed class TicketListItem
    {
        public long TicketId { get; set; }
        public string TicketCode { get; set; }
        public string TripCode { get; set; }
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public string OriginName { get; set; }
        public string DestinationName { get; set; }
        public DateTime ServiceDate { get; set; }
        public TimeSpan ScheduledDepartureTime { get; set; }
        public DateTime ExpectedFinishLocal { get; set; }
        public TripStatus TripStatus { get; set; }
        public TicketStatus TicketStatus { get; set; }
        public decimal FareAmount { get; set; }
        public DateTime PurchasedUtc { get; set; }
        public DateTime? RefundedUtc { get; set; }
        public int? EstimatedDelayMinutes { get; set; }
        public string FleetNumber { get; set; }
    }

    public sealed class TicketDetails
    {
        public TicketListItem Ticket { get; set; }
        public string RefundReason { get; set; }
    }

    public sealed class PassengerHomeDetails
    {
        public PassengerHomeDetails()
        {
            RecentTransactions = new List<WalletTransactionItem>();
        }

        public string DisplayName { get; set; }
        public decimal WalletBalance { get; set; }
        public TicketListItem NextTicket { get; set; }
        public IList<WalletTransactionItem> RecentTransactions { get; set; }
    }
}
