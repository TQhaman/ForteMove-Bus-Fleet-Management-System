using System.Collections.Generic;

namespace ForteMove.Models.Routing
{
    public sealed class RouteCreationAggregate
    {
        public RouteCreationAggregate()
        {
            Stops = new List<RouteCreationStop>();
        }

        public Route Route { get; set; }

        public IList<RouteCreationStop> Stops { get; set; }
    }
}
