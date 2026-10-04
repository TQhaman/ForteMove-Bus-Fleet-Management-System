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
            litActiveRoutes.Text = summary.ActiveRoutes.ToString("N0", CultureInfo.CurrentCulture);
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
