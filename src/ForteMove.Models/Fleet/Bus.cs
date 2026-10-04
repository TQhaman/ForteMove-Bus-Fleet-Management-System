using System;

namespace ForteMove.Models.Fleet
{
    public sealed class Bus
    {
        public long BusId { get; set; }

        public int BusCategoryId { get; set; }

        public int PropulsionTypeId { get; set; }

        public string FleetNumber { get; set; }

        public string RegistrationNumber { get; set; }

        public string Vin { get; set; }

        public string Make { get; set; }

        public string Model { get; set; }

        public int ManufactureYear { get; set; }

        public int PassengerCapacity { get; set; }

        public decimal? FuelTankCapacityLitres { get; set; }

        public decimal? BatteryCapacityKwh { get; set; }

        public decimal OdometerKilometres { get; set; }

        public DateTime LicenceExpiryDate { get; set; }

        public DateTime RoadworthyExpiryDate { get; set; }

        public DateTime InsuranceExpiryDate { get; set; }

        public BusOperationalState BaseOperationalState { get; set; }
    }
}
