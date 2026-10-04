using System;
using System.Web.Security;
using System.Web.UI;
using ForteMove.Business.Services;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Driver
{
    public partial class DriverMaster : MasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            ForteMovePrincipal principal = Context.User as ForteMovePrincipal;
            if (principal != null) lblName.Text = Server.HtmlEncode(principal.Context.DisplayName);
            string path = Request.AppRelativeCurrentExecutionFilePath;
            Mark(lnkToday, path == "~/Driver/Today.aspx" || path == "~/Driver/TripDetails.aspx" || path == "~/Driver/PreTripCheck.aspx" || path == "~/Driver/ReportDelay.aspx" || path == "~/Driver/CannotProceed.aspx" || path == "~/Driver/ReportDefect.aspx" || path == "~/Driver/CompleteTrip.aspx");
            Mark(lnkUpcoming, path == "~/Driver/Upcoming.aspx");
            Mark(lnkHistory, path == "~/Driver/History.aspx");
            Mark(lnkAccount, path == "~/Driver/Account.aspx");
        }

        private static void Mark(System.Web.UI.WebControls.HyperLink link, bool active)
        {
            link.CssClass = active ? "active" : string.Empty;
            if (active) link.Attributes["aria-current"] = "page";
            else link.Attributes.Remove("aria-current");
        }

        protected void btnLogout_Click(object sender, EventArgs e)
        {
            ForteMovePrincipal principal = Context.User as ForteMovePrincipal;
            try
            {
                if (principal != null)
                {
                    AuthenticationService service = ServiceFactory.CreateAuthenticationService();
                    service.RecordLogout(principal.Context.UserAccountId, ClientIpAddress.From(Request));
                }
            }
            catch { }
            finally
            {
                FormsAuthentication.SignOut(); Session.Clear(); Session.Abandon();
            }
            Response.Redirect(ResolveUrl("~/Account/Login.aspx"), false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
