using System;
using System.Web.Security;
using System.Web.UI;
using ForteMove.Business.Services;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Passenger
{
    public partial class PassengerMaster : MasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            ForteMovePrincipal principal = Context.User as ForteMovePrincipal;
            if (principal != null) lblName.Text = Server.HtmlEncode(principal.Context.DisplayName);
            string path = Request.AppRelativeCurrentExecutionFilePath;
            Mark(lnkHome, path == "~/Passenger/Home.aspx");
            Mark(lnkJourneys, path == "~/Passenger/Journeys.aspx" || path == "~/Passenger/JourneyDetails.aspx");
            Mark(lnkTickets, path == "~/Passenger/Tickets.aspx" || path == "~/Passenger/TicketDetails.aspx");
            Mark(lnkWallet, path == "~/Passenger/Wallet.aspx");
            Mark(lnkAccount, path == "~/Passenger/Account.aspx");
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
                    ServiceFactory.CreateAuthenticationService().RecordLogout(
                        principal.Context.UserAccountId, ClientIpAddress.From(Request));
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
