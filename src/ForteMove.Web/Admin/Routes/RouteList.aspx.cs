using System;
using System.Collections.Generic;
using System.Globalization;
using ForteMove.Business.Services;
using ForteMove.Models.Routing;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Admin.Routes
{
    public partial class RouteList : AdminPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                ShowFlashMessage();
                BindRoutes();
            }
        }

        protected void btnApplyFilters_Click(object sender, EventArgs e)
        {
            BindRoutes();
        }

        protected void btnClearFilters_Click(object sender, EventArgs e)
        {
            Response.Redirect(ResolveUrl("~/Admin/Routes/RouteList.aspx"), true);
        }

        protected string FormatDistance(object value)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0:N1} km", Convert.ToDecimal(value, CultureInfo.InvariantCulture));
        }

        protected string FormatDuration(object value)
        {
            int minutes = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return minutes == 1 ? "1 minute" : string.Format(CultureInfo.CurrentCulture, "{0:N0} minutes", minutes);
        }

        protected string FormatFare(object value)
        {
            return string.Format(CultureInfo.CurrentCulture, "R {0:N2}", Convert.ToDecimal(value, CultureInfo.InvariantCulture));
        }

        protected string FormatStatus(object value)
        {
            return Convert.ToBoolean(value, CultureInfo.InvariantCulture) ? "Active" : "Inactive";
        }

        protected string GetStatusCss(object value)
        {
            return Convert.ToBoolean(value, CultureInfo.InvariantCulture)
                ? "status-badge status-success"
                : "status-badge status-neutral";
        }

        protected string GetDetailsUrl(object routeId)
        {
            return ResolveUrl("~/Admin/Routes/RouteDetails.aspx?id=" +
                Convert.ToInt64(routeId, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
        }

        private void BindRoutes()
        {
            IList<RouteListItem> routes = ServiceFactory.CreateRouteService().GetRouteList(new RouteQuery
            {
                SearchTerm = txtSearch.Text,
                IsActive = GetSelectedStatus()
            });

            bool hasResults = routes != null && routes.Count > 0;
            pnlResults.Visible = hasResults;
            pnlEmpty.Visible = !hasResults;

            if (hasResults)
            {
                rptRoutes.DataSource = routes;
                rptRoutes.DataBind();
                litRouteCount.Text = routes.Count == 1
                    ? "1 route matches the current view."
                    : string.Format(CultureInfo.CurrentCulture, "{0:N0} routes match the current view.", routes.Count);
                return;
            }

            bool hasFilters = !string.IsNullOrWhiteSpace(txtSearch.Text) || !string.IsNullOrWhiteSpace(ddlStatus.SelectedValue);
            litEmptyHeading.Text = hasFilters ? "No routes match these filters" : "No routes created yet";
            litEmptyMessage.Text = hasFilters
                ? "Adjust or clear the filters to return to the complete route network."
                : "Create the first approved service path and arrange its stops in travel order.";
            lnkEmptyAction.Visible = !hasFilters;
        }

        private bool? GetSelectedStatus()
        {
            bool value;
            return bool.TryParse(ddlStatus.SelectedValue, out value) ? value : (bool?)null;
        }

        private void ShowFlashMessage()
        {
            FlashMessage message = FlashMessageStore.Take(Session);
            if (message == null || string.IsNullOrWhiteSpace(message.Text))
            {
                return;
            }

            pnlFlash.CssClass = message.IsWarning ? "alert alert-warning app-alert" : "alert alert-success app-alert";
            lblFlash.Text = Server.HtmlEncode(message.Text);
            pnlFlash.Visible = true;
        }
    }
}
