using System;
using System.Globalization;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Admin
{
    public partial class Dashboard : AdminPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                litFirstName.Text = Server.HtmlEncode(GetFirstName(CurrentPrincipalContext.DisplayName));
                BindSummary();
            }
        }

        private void BindSummary()
        {
            var summary = ServiceFactory.CreateDashboardService().GetSummary();
            litTotalFleet.Text = summary.TotalFleet.ToString("N0", CultureInfo.CurrentCulture);
            litOperationalFleet.Text = summary.OperationalFleet.ToString("N0", CultureInfo.CurrentCulture);
            litOutOfServiceFleet.Text = summary.OutOfServiceFleet.ToString("N0", CultureInfo.CurrentCulture);
            litUnderMaintenanceFleet.Text = summary.UnderMaintenanceFleet.ToString("N0", CultureInfo.CurrentCulture);
            litRetiredFleet.Text = summary.RetiredFleet.ToString("N0", CultureInfo.CurrentCulture);
            litActiveRoutes.Text = summary.ActiveRoutes.ToString("N0", CultureInfo.CurrentCulture);
            litActiveSchedules.Text = summary.ActiveSchedules.ToString("N0", CultureInfo.CurrentCulture);
            litTodaysTrips.Text = summary.TodaysTrips.ToString("N0", CultureInfo.CurrentCulture);
            litUnassignedTrips.Text = summary.UnassignedTrips.ToString("N0", CultureInfo.CurrentCulture);
            litAvailableDrivers.Text = summary.AvailableDrivers.ToString("N0", CultureInfo.CurrentCulture);
            litScheduledTrips.Text = summary.ScheduledTrips.ToString("N0", CultureInfo.CurrentCulture);
            pnlRetired.Visible = summary.RetiredFleet > 0;
            SetBar(barOperational, summary.OperationalFleet, summary.TotalFleet, "Operational");
            SetBar(barOutOfService, summary.OutOfServiceFleet, summary.TotalFleet, "Out of service");
            SetBar(barUnderMaintenance, summary.UnderMaintenanceFleet, summary.TotalFleet, "Under maintenance");
            SetBar(barRetired, summary.RetiredFleet, summary.TotalFleet, "Retired");
        }

        private static void SetBar(
            System.Web.UI.HtmlControls.HtmlGenericControl bar,
            long value,
            long total,
            string label)
        {
            decimal percentage = total <= 0 ? 0m : Math.Round(value * 100m / total, 1);
            bar.Attributes["style"] = "width:" + percentage.ToString("0.0", CultureInfo.InvariantCulture) + "%";
            bar.Attributes["role"] = "progressbar";
            bar.Attributes["aria-label"] = label;
            bar.Attributes["aria-valuemin"] = "0";
            bar.Attributes["aria-valuemax"] = Math.Max(total, 1).ToString(CultureInfo.InvariantCulture);
            bar.Attributes["aria-valuenow"] = value.ToString(CultureInfo.InvariantCulture);
        }

        private static string GetFirstName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "Administrator";
            }

            string trimmed = displayName.Trim();
            int separator = trimmed.IndexOf(' ');
            return separator > 0 ? trimmed.Substring(0, separator) : trimmed;
        }
    }
}
