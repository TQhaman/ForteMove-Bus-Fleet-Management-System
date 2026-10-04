namespace ForteMove.Models.Fleet
{
    public sealed class FleetQuery
    {
        public string SearchTerm { get; set; }

        public BusOperationalState? BaseOperationalState { get; set; }
    }
}
