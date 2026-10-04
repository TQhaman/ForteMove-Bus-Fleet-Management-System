using System;
using System.Collections.Generic;

namespace ForteMove.Web.Infrastructure
{
    [Serializable]
    public sealed class RouteBuilderState
    {
        public RouteBuilderState()
        {
            Stops = new List<RouteDraftStop>();
        }

        public List<RouteDraftStop> Stops { get; set; }
    }

    [Serializable]
    public sealed class RouteDraftStop
    {
        public string DraftKey { get; set; }

        public long? ExistingStopId { get; set; }

        public string StopCode { get; set; }

        public string StopName { get; set; }

        public string Area { get; set; }

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        public int? EstimatedMinutesFromOrigin { get; set; }

        public bool IsNew
        {
            get { return !ExistingStopId.HasValue; }
        }
    }
}
