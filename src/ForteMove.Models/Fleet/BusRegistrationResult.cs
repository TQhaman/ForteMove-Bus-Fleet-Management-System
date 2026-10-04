namespace ForteMove.Models.Fleet
{
    public sealed class BusRegistrationResult
    {
        public long BusId { get; set; }

        public string FleetNumber { get; set; }

        public BusOperationalState BaseOperationalState { get; set; }
    }
}
