using System;

namespace ForteMove.Models.Fleet
{
    public sealed class BusDetails
    {
        public long BusId { get; set; }
        public int BusCategoryId { get; set; }
        public string CategoryName { get; set; }
        public string FleetNumber { get; set; }
        public string RegistrationNumber { get; set; }
        public string Vin { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public int ManufactureYear { get; set; }
        public int PassengerCapacity { get; set; }
        public int? GrossVehicleMassKg { get; set; }
        public decimal OdometerKilometres { get; set; }
        public DateTime LicenceExpiryDate { get; set; }
        public DateTime RoadworthyExpiryDate { get; set; }
        public DateTime InsuranceExpiryDate { get; set; }
        public BusOperationalState BaseOperationalState { get; set; }
        public string RequiredLicenceCode { get; set; }
        public byte[] RowVersion { get; set; }
    }

    public sealed class UpdateBusEligibilityRequest
    {
        public long BusId { get; set; }
        public int? BusCategoryId { get; set; }
        public int? PassengerCapacity { get; set; }
        public int? GrossVehicleMassKg { get; set; }
        public DateTime? LicenceExpiryDate { get; set; }
        public DateTime? RoadworthyExpiryDate { get; set; }
        public DateTime? InsuranceExpiryDate { get; set; }
        public BusOperationalState? BaseOperationalState { get; set; }
        public byte[] RowVersion { get; set; }
    }
}
