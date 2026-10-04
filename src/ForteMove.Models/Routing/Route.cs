namespace ForteMove.Models.Routing
{
    public sealed class Route
    {
        public long RouteId { get; set; }

        public string RouteCode { get; set; }

        public string RouteName { get; set; }

        public decimal EstimatedDistanceKm { get; set; }

        public int EstimatedDurationMinutes { get; set; }

        public decimal DefaultFare { get; set; }

        public bool IsActive { get; set; }
    }
}
