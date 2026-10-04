using System;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using ForteMove.Business.Services;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Admin
{
    public partial class AdminMaster : MasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            ForteMovePrincipal principal = Context.User as ForteMovePrincipal;
            if (principal == null)
            {
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(principal.Context.DisplayName)
                ? principal.Context.Email
                : principal.Context.DisplayName;
            lblDisplayName.Text = HttpUtility.HtmlEncode(displayName);
            lblRole.Text = HttpUtility.HtmlEncode(principal.Context.RoleDisplayName);
            litInitial.Text = HttpUtility.HtmlEncode(GetInitial(displayName));
            MarkActiveNavigation(Request.AppRelativeCurrentExecutionFilePath);
        }

        protected void btnLogout_Click(object sender, EventArgs e)
        {
            ForteMovePrincipal principal = Context.User as ForteMovePrincipal;
            try
            {
                if (principal != null)
                {
                    AuthenticationService authenticationService = ServiceFactory.CreateAuthenticationService();
                    authenticationService.RecordLogout(
                        principal.Context.UserAccountId,
                        ClientIpAddress.From(Request));
                }
            }
            catch
            {
                // Logout must still complete if audit persistence is temporarily unavailable.
            }
            finally
            {
                FormsAuthentication.SignOut();
                Session.Clear();
                Session.Abandon();
            }

            Response.Redirect(ResolveUrl("~/Account/Login.aspx"), false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private void MarkActiveNavigation(string appRelativePath)
        {
            lnkDashboard.CssClass = BuildNavigationClass(
                appRelativePath,
                "~/Admin/Dashboard.aspx");
            lnkRegisterBus.CssClass = BuildNavigationClass(
                appRelativePath,
                "~/Admin/RegisterBus.aspx");
            lnkFleetList.CssClass = BuildNavigationClass(
                appRelativePath,
                "~/Admin/FleetList.aspx");
            lnkRouteList.CssClass = BuildNavigationClass(
                appRelativePath,
                "~/Admin/Routes/RouteList.aspx",
                "~/Admin/Routes/RouteDetails.aspx");
            lnkCreateRoute.CssClass = BuildNavigationClass(
                appRelativePath,
                "~/Admin/Routes/CreateRoute.aspx");
            lnkScheduleList.CssClass = BuildNavigationClass(
                appRelativePath,
                "~/Admin/Scheduling/ScheduleList.aspx",
                "~/Admin/Scheduling/ScheduleDetails.aspx",
                "~/Admin/Scheduling/ChangeSchedule.aspx");
            lnkCreateSchedule.CssClass = BuildNavigationClass(
                appRelativePath,
                "~/Admin/Scheduling/CreateSchedule.aspx");
            lnkTripList.CssClass = BuildNavigationClass(
                appRelativePath,
                "~/Admin/Scheduling/TripList.aspx");

            SetAriaCurrent(lnkDashboard);
            SetAriaCurrent(lnkRegisterBus);
            SetAriaCurrent(lnkFleetList);
            SetAriaCurrent(lnkRouteList);
            SetAriaCurrent(lnkCreateRoute);
            SetAriaCurrent(lnkScheduleList);
            SetAriaCurrent(lnkCreateSchedule);
            SetAriaCurrent(lnkTripList);
        }

        private static string BuildNavigationClass(string currentPath, params string[] targetPaths)
        {
            if (targetPaths != null)
            {
                foreach (string targetPath in targetPaths)
                {
                    if (string.Equals(currentPath, targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return "app-nav-link active";
                    }
                }
            }

            return "app-nav-link";
        }

        private static void SetAriaCurrent(System.Web.UI.WebControls.HyperLink link)
        {
            if (link.CssClass.IndexOf(" active", StringComparison.Ordinal) >= 0)
            {
                link.Attributes["aria-current"] = "page";
            }
            else
            {
                link.Attributes.Remove("aria-current");
            }
        }

        private static string GetInitial(string displayName)
        {
            return string.IsNullOrWhiteSpace(displayName)
                ? "A"
                : displayName.Trim().Substring(0, 1).ToUpperInvariant();
        }
    }
}
