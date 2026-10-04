namespace ForteMove.Models.Routing
{
    public sealed class CreateStopRequest
    {
        public string StopName { get; set; }

        public string Area { get; set; }

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }
    }
}
