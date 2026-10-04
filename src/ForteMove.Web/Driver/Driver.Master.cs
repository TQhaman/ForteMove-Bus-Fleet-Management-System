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
