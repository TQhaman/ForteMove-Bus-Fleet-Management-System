using System.Collections.Generic;

namespace ForteMove.Models.Routing
{
    public sealed class CreateRouteRequest
    {
        public CreateRouteRequest()
        {
            Stops = new List<RouteStopRequest>();
        }

        public string RouteName { get; set; }

        public decimal? EstimatedDistanceKm { get; set; }

        public int? EstimatedDurationMinutes { get; set; }

        public decimal? DefaultFare { get; set; }

        public IList<RouteStopRequest> Stops { get; set; }
    }
}
