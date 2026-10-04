using System.Collections.Generic;

namespace ForteMove.Models.Routing
{
    public sealed class RouteDetails
    {
        public RouteDetails()
        {
            Stops = new List<RouteStopDetails>();
        }

        public long RouteId { get; set; }

        public string RouteCode { get; set; }

        public string RouteName { get; set; }

        public string OriginStopName { get; set; }

        public string DestinationStopName { get; set; }

        public decimal EstimatedDistanceKm { get; set; }

        public int EstimatedDurationMinutes { get; set; }

        public decimal DefaultFare { get; set; }

        public bool IsActive { get; set; }

        public IList<RouteStopDetails> Stops { get; set; }
    }
}
