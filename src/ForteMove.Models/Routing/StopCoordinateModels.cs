using System.Collections.Generic;

namespace ForteMove.Models.Routing
{
    public sealed class StopCoordinateDetails
    {
        public long StopId { get; set; }
        public string StopCode { get; set; }
        public string StopName { get; set; }
        public string Area { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public byte[] RowVersion { get; set; }
        public IList<string> Routes { get; set; } = new List<string>();
        public IList<string> ActiveTripCodes { get; set; } = new List<string>();
    }

    public sealed class UpdateStopCoordinatesRequest
    {
        public long StopId { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public byte[] RowVersion { get; set; }
        public bool ActiveTripWarningAcknowledged { get; set; }
    }

    public sealed class StopCoordinateSaveResult
    {
        public bool Saved { get; set; }
        public StopCoordinateDetails Details { get; set; }
    }
}
