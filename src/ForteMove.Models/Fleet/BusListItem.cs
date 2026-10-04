using System;

namespace ForteMove.Models.Fleet
{
    public sealed class BusListItem
    {
        public long BusId { get; set; }

        public string FleetNumber { get; set; }

        public string RegistrationNumber { get; set; }

        public string Vin { get; set; }

        public string Make { get; set; }

        public string Model { get; set; }

        public int ManufactureYear { get; set; }

        public string CategoryName { get; set; }

        public int PassengerCapacity { get; set; }

        public int? GrossVehicleMassKg { get; set; }

        public string RequiredLicenceCode { get; set; }

        public string PropulsionName { get; set; }

        public decimal OdometerKilometres { get; set; }

        public DateTime LicenceExpiryDate { get; set; }

        public DateTime RoadworthyExpiryDate { get; set; }

        public DateTime InsuranceExpiryDate { get; set; }

        public BusComplianceStatus ComplianceStatus { get; set; }

        public BusOperationalState BaseOperationalState { get; set; }
    }
}
