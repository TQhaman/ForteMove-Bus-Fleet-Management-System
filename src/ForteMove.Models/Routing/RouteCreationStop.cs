namespace ForteMove.Models.Routing
{
    public sealed class RouteCreationStop
    {
        public long? ExistingStopId { get; set; }

        public Stop NewStop { get; set; }

        public int StopOrder { get; set; }

        public int? EstimatedMinutesFromOrigin { get; set; }
    }
}
