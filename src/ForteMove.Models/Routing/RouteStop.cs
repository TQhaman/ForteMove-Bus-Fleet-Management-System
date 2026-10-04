namespace ForteMove.Models.Routing
{
    public sealed class RouteStop
    {
        public long RouteStopId { get; set; }

        public long RouteId { get; set; }

        public long StopId { get; set; }

        public int StopOrder { get; set; }

        public int? EstimatedMinutesFromOrigin { get; set; }
    }
}
