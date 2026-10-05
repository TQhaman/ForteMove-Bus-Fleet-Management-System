using System;
using System.Collections.Generic;
using ForteMove.Models.Scheduling;
namespace ForteMove.Models.Fuel
{
    public sealed class FuelStation
    {
        public long FuelStationId { get; set; }
        public string StationCode { get; set; }
        public string StationName { get; set; }
        public string AreaDescription { get; set; }
        public bool IsActive { get; set; }
        public bool SupportsDiesel { get; set; }
        public bool SupportsElectricCharging { get; set; }
        public byte[] RowVersion { get; set; }
    }
    public sealed class FuelTripContext
    {
        public long TripId { get; set; }
        public long TripAssignmentId { get; set; }
        public long DriverProfileId { get; set; }
        public long BusId { get; set; }
        public long DriverUserAccountId { get; set; }
        public string TripCode { get; set; }
        public string RouteName { get; set; }
        public string DriverName { get; set; }
        public string EmployeeNumber { get; set; }
        public string FleetNumber { get; set; }
        public string PropulsionCode { get; set; }
        public TripStatus TripStatus { get; set; }
        public bool AssignmentIsCurrent { get; set; }
        public bool DriverAccountActive { get; set; }
        public bool RequiresReview { get; set; }
        public bool HasOpenCannotProceed { get; set; }
        public bool HasCriticalDefect { get; set; }
        public bool HasExecution { get; set; }
        public DateTime ExpectedFinishLocal { get; set; }
        public DateTime ServiceDate { get; set; }
        public decimal OdometerKilometres { get; set; }
        public decimal? FuelTankCapacityLitres { get; set; }
        public decimal? BatteryCapacityKwh { get; set; }
        public string VehicleStatus { get; set; }
        public DateTime LicenceExpiryDate { get; set; }
        public DateTime RoadworthyExpiryDate { get; set; }
        public DateTime InsuranceExpiryDate { get; set; }
        public byte[] TripRowVersion { get; set; }
        public byte[] AssignmentRowVersion { get; set; }
        public byte[] BusRowVersion { get; set; }
    }
    public sealed class FuelRequestDetails
    {
        public long FuelRequestId { get; set; }
        public string RequestCode { get; set; }
        public FuelTripContext Context { get; set; }
        public FuelSupplyType SupplyType { get; set; }
        public string Justification { get; set; }
        public FuelRequestStatus Status { get; set; }
        public decimal OdometerAtRequest { get; set; }
        public DateTime SubmittedUtc { get; set; }
        public string DecisionNote { get; set; }
        public string CancellationReason { get; set; }
        public byte[] RowVersion { get; set; }
        public long? FuelVoucherId { get; set; }
    }
    public sealed class FuelVoucherDetails
    {
        public long FuelVoucherId { get; set; }
        public string VoucherCode { get; set; }
        public FuelRequestDetails Request { get; set; }
        public long FuelStationId { get; set; }
        public string StationCode { get; set; }
        public string StationName { get; set; }
        public string StationArea { get; set; }
        public string FleetNumber { get; set; }
        public FuelSupplyType SupplyType { get; set; }
        public FuelQuantityUnit Unit { get; set; }
        public decimal ApprovedQuantity { get; set; }
        public decimal ApprovedAmount { get; set; }
        public DateTime ApprovedUtc { get; set; }
        public DateTime ValidUntilLocal { get; set; }
        public FuelVoucherStatus Status { get; set; }
        public FuelVoucherDisplayState DisplayState { get; set; }
        public DateTime? RedeemedUtc { get; set; }
        public string CancellationReason { get; set; }
        public byte[] RowVersion { get; set; }
        // Returned only by the ownership-protected Driver display query.
        public byte[] ProtectedToken { get; set; }
    }
    public sealed class FuelTransactionDetails
    {
        public long FuelTransactionId { get; set; }
        public string TransactionCode { get; set; }
        public long FuelVoucherId { get; set; }
        public string VoucherCode { get; set; }
        public string RequestCode { get; set; }
        public string TripCode { get; set; }
        public string DriverName { get; set; }
        public string EmployeeNumber { get; set; }
        public string FleetNumber { get; set; }
        public string StationCode { get; set; }
        public string StationName { get; set; }
        public string StationArea { get; set; }
        public FuelSupplyType SupplyType { get; set; }
        public FuelQuantityUnit Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal Amount { get; set; }
        public decimal OdometerAtRedemption { get; set; }
        public DateTime RedeemedUtc { get; set; }
    }
    public sealed class FuelSubmissionResult
    {
        public long FuelRequestId { get; set; }
        public string RequestCode { get; set; }
        public bool AlreadyProcessed { get; set; }
    }
    public sealed class FuelRedemptionResult
    {
        public long FuelTransactionId { get; set; }
        public string TransactionCode { get; set; }
        public bool AlreadyRedeemed { get; set; }
    }
    public sealed class FuelRedemptionPreview
    {
        public FuelVoucherDetails Voucher { get; set; }
        public byte[] StationRowVersion { get; set; }
        public string Fingerprint { get; set; }
        public IList<string> Warnings { get; set; }
    }
}

