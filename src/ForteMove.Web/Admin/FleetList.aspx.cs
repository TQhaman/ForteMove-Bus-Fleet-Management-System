using System;
using System.Collections.Generic;
using System.Globalization;
using ForteMove.Business.Services;
using ForteMove.Models.Fleet;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Admin
{
    public partial class FleetList : AdminPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                ShowFlashMessage();
                BindFleet();
            }
        }

        protected void btnApplyFilters_Click(object sender, EventArgs e)
        {
            BindFleet();
        }

        protected void btnClearFilters_Click(object sender, EventArgs e)
        {
            Response.Redirect(ResolveUrl("~/Admin/FleetList.aspx"), true);
        }

        protected string FormatOdometer(object value)
        {
            decimal kilometres = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return string.Format(CultureInfo.CurrentCulture, "{0:N0} km", kilometres);
        }

        protected string FormatCompliance(object value)
        {
            BusComplianceStatus status = (BusComplianceStatus)value;
            return status == BusComplianceStatus.Compliant ? "Compliant" : "Expired";
        }

        protected string GetComplianceCss(object value)
        {
            BusComplianceStatus status = (BusComplianceStatus)value;
            return status == BusComplianceStatus.Compliant
                ? "status-badge status-success"
                : "status-badge status-danger";
        }

        protected string FormatComplianceDates(object dataItem)
        {
            BusListItem bus = dataItem as BusListItem;
            if (bus == null)
            {
                return string.Empty;
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                "Licence {0:d} | Roadworthy {1:d} | Insurance {2:d}",
                bus.LicenceExpiryDate,
                bus.RoadworthyExpiryDate,
                bus.InsuranceExpiryDate);
        }

        protected string FormatGvm(object dataItem)
        {
            BusListItem bus = dataItem as BusListItem;
            if (bus == null || !bus.GrossVehicleMassKg.HasValue) return "GVM required";
            return string.Format(CultureInfo.CurrentCulture, "GVM {0:N0} kg | Licence {1}", bus.GrossVehicleMassKg.Value, bus.RequiredLicenceCode);
        }

        protected string FormatState(object value)
        {
            switch ((BusOperationalState)value)
            {
                case BusOperationalState.Operational:
                    return "Operational";
                case BusOperationalState.OutOfService:
                    return "Out of service";
                case BusOperationalState.UnderMaintenance:
                    return "Under maintenance";
                case BusOperationalState.Retired:
                    return "Retired";
                default:
                    return "Unknown";
            }
        }

        protected string GetStateCss(object value)
        {
            switch ((BusOperationalState)value)
            {
                case BusOperationalState.Operational:
                    return "status-badge status-success";
                case BusOperationalState.OutOfService:
                    return "status-badge status-danger";
                case BusOperationalState.UnderMaintenance:
                    return "status-badge status-warning";
                default:
                    return "status-badge status-neutral";
            }
        }

        private void BindFleet()
        {
            BusService busService = ServiceFactory.CreateBusService();
            IList<BusListItem> fleet = busService.GetFleetList(new FleetQuery
            {
                SearchTerm = txtSearch.Text,
                BaseOperationalState = GetSelectedState()
            });

            bool hasResults = fleet != null && fleet.Count > 0;
            pnlResults.Visible = hasResults;
            pnlEmpty.Visible = !hasResults;

            if (hasResults)
            {
                rptFleet.DataSource = fleet;
                rptFleet.DataBind();
                litFleetCount.Text = fleet.Count == 1
                    ? "1 bus matches the current view."
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        "{0:N0} buses match the current view.",
                        fleet.Count);
                return;
            }

            bool hasFilters = !string.IsNullOrWhiteSpace(txtSearch.Text) ||
                !string.IsNullOrWhiteSpace(ddlState.SelectedValue);
            litEmptyHeading.Text = hasFilters ? "No buses match these filters" : "No buses registered yet";
            litEmptyMessage.Text = hasFilters
                ? "Adjust or clear the filters to return to the complete fleet."
                : "Register the first approved vehicle to begin the ForteMove fleet record.";
            lnkEmptyAction.Visible = !hasFilters;
        }

        private BusOperationalState? GetSelectedState()
        {
            BusOperationalState state;
            return Enum.TryParse(ddlState.SelectedValue, true, out state)
                ? state
                : (BusOperationalState?)null;
        }

        private void ShowFlashMessage()
        {
            FlashMessage message = FlashMessageStore.Take(Session);
            if (message == null || string.IsNullOrWhiteSpace(message.Text))
            {
                return;
            }

            pnlFlash.CssClass = message.IsWarning
                ? "alert alert-warning app-alert"
                : "alert alert-success app-alert";
            litFlashIcon.Text = message.IsWarning ? "!" : "i";
            lblFlash.Text = Server.HtmlEncode(message.Text);
            pnlFlash.Visible = true;
        }
    }
}
