using System;
namespace ForteMove.Models.Fuel
{
    public sealed class SubmitFuelRequest
    {
        public long TripId { get; set; }
        public string Justification { get; set; }
        public Guid SubmissionToken { get; set; }
        public byte[] TripRowVersion { get; set; }
        public byte[] AssignmentRowVersion { get; set; }
    }
    public sealed class ApproveFuelRequest
    {
        public long FuelRequestId { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? Amount { get; set; }
        public long FuelStationId { get; set; }
        public DateTime? ValidUntilLocal { get; set; }
        public string Note { get; set; }
        public byte[] RequestRowVersion { get; set; }
        public byte[] TripRowVersion { get; set; }
        public byte[] AssignmentRowVersion { get; set; }
        public byte[] BusRowVersion { get; set; }
        public byte[] StationRowVersion { get; set; }
    }
    public sealed class RejectFuelRequest
    {
        public long FuelRequestId { get; set; }
        public string Reason { get; set; }
        public byte[] RowVersion { get; set; }
    }
    public sealed class SaveFuelStationRequest
    {
        public long FuelStationId { get; set; }
        public string StationName { get; set; }
        public string AreaDescription { get; set; }
        public bool IsActive { get; set; }
        public bool SupportsDiesel { get; set; }
        public bool SupportsElectricCharging { get; set; }
        public byte[] RowVersion { get; set; }
    }
    public sealed class RedeemFuelVoucherRequest
    {
        public string Token { get; set; }
        public long FuelStationId { get; set; }
        public byte[] VoucherRowVersion { get; set; }
        public byte[] StationRowVersion { get; set; }
        public string PreviewFingerprint { get; set; }
    }
    public sealed class FuelQuery
    {
        public string Search { get; set; }
        public string Status { get; set; }
    }
}

