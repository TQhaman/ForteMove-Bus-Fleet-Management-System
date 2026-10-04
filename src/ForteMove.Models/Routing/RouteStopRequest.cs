namespace ForteMove.Models.Routing
{
    public sealed class RouteStopRequest
    {
        public long? ExistingStopId { get; set; }

        public CreateStopRequest NewStop { get; set; }

        public int StopOrder { get; set; }

        public int? EstimatedMinutesFromOrigin { get; set; }
    }
}
