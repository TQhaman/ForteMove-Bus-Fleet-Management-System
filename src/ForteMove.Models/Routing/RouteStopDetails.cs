namespace ForteMove.Models.Routing
{
    public sealed class RouteStopDetails
    {
        public long StopId { get; set; }

        public string StopCode { get; set; }

        public string StopName { get; set; }

        public string Area { get; set; }

        public int StopOrder { get; set; }

        public int? EstimatedMinutesFromOrigin { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public bool HasCoordinates { get { return Latitude.HasValue && Longitude.HasValue; } }
    }
}
