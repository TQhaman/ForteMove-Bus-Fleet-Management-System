namespace ForteMove.Models.Routing
{
    public sealed class Stop
    {
        public long StopId { get; set; }

        public string StopCode { get; set; }

        public string StopName { get; set; }

        public string NormalizedStopName { get; set; }

        public string Area { get; set; }

        public string NormalizedArea { get; set; }

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        public bool IsActive { get; set; }
    }
}
