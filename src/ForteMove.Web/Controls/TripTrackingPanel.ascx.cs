using System;
using System.Configuration;
using System.Web.UI;

namespace ForteMove.Web.Controls
{
    public partial class TripTrackingPanel : UserControl
    {
        public string SnapshotUrl { get; set; }
        protected override void OnPreRender(EventArgs e)
        {
            trackingPanel.Attributes["data-tracking-url"]=SnapshotUrl;
            string tiles=ConfigurationManager.AppSettings["TrackingTileUrl"]??"https://tile.openstreetmap.org/{z}/{x}/{y}.png";
            // Only the reviewed OSM host is supported; an empty setting disables external tiles.
            trackingPanel.Attributes["data-tile-url"]=tiles=="https://tile.openstreetmap.org/{z}/{x}/{y}.png"?tiles:string.Empty;
            Page.ClientScript.RegisterClientScriptInclude("leaflet",ResolveUrl("~/Scripts/vendor/leaflet/leaflet.js"));
            Page.ClientScript.RegisterClientScriptInclude("fortemove-tracking",ResolveUrl("~/Scripts/fortemove-tracking.js"));
            base.OnPreRender(e);
        }
    }
}
