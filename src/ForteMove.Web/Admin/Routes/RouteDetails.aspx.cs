using System;
using System.Globalization;
using ForteMove.Models.Routing;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Admin.Routes
{
    public partial class RouteDetailsPage : AdminPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BindRoute();
            }
        }

        protected string FormatMinutesFromOrigin(object value)
        {
            int minutes = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return minutes == 0
                ? "Origin"
                : string.Format(CultureInfo.CurrentCulture, "+{0:N0} min", minutes);
        }

        private void BindRoute()
        {
            long routeId;
            RouteDetails details = null;
            if (long.TryParse(Request.QueryString["id"], NumberStyles.None, CultureInfo.InvariantCulture, out routeId) && routeId > 0)
            {
                details = ServiceFactory.CreateRouteService().GetRouteDetails(routeId);
            }

            if (details == null)
            {
                Response.StatusCode = 404;
                Response.TrySkipIisCustomErrors = true;
                pnlNotFound.Visible = true;
                return;
            }

            Title = details.RouteCode + " route details";
            litRouteCode.Text = Server.HtmlEncode(details.RouteCode);
            litRouteName.Text = Server.HtmlEncode(details.RouteName);
            litOrigin.Text = Server.HtmlEncode(details.OriginStopName);
            litDestination.Text = Server.HtmlEncode(details.DestinationStopName);
            litDistance.Text = string.Format(CultureInfo.CurrentCulture, "{0:N1} km", details.EstimatedDistanceKm);
            litDuration.Text = details.EstimatedDurationMinutes == 1
                ? "1 minute"
                : string.Format(CultureInfo.CurrentCulture, "{0:N0} minutes", details.EstimatedDurationMinutes);
            litFare.Text = string.Format(CultureInfo.CurrentCulture, "R {0:N2}", details.DefaultFare);
            litStopCount.Text = details.Stops.Count == 1
                ? "1 stop"
                : string.Format(CultureInfo.CurrentCulture, "{0:N0} stops", details.Stops.Count);
            lblRouteStatus.Text = details.IsActive ? "Active" : "Inactive";
            lblRouteStatus.CssClass = details.IsActive
                ? "status-badge status-success"
                : "status-badge status-neutral";

            rptStops.DataSource = details.Stops;
            rptStops.DataBind();
            pnlDetails.Visible = true;
        }
    }
}
