using System.Collections.Generic;

namespace ForteMove.Models.Routing
{
    public sealed class RouteCreationOptions
    {
        public RouteCreationOptions()
        {
            ActiveStops = new List<StopOption>();
        }

        public string SuggestedRouteCode { get; set; }

        public string SuggestedStopCode { get; set; }

        public IList<StopOption> ActiveStops { get; set; }
    }
}
