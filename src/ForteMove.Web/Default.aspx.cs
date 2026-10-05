using System;
using System.Web.Security;
using System.Web.UI;
using ForteMove.Models.Security;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web
{
    public partial class DefaultPage : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            ForteMovePrincipal principal = Context.User as ForteMovePrincipal;
            if (principal == null || !principal.Identity.IsAuthenticated)
            {
                Response.Redirect(ResolveUrl(FormsAuthentication.LoginUrl), true);
                return;
            }

            if (principal.Context.MustChangePassword)
            {
                Response.Redirect(ResolveUrl("~/Account/ChangePassword.aspx"), true);
                return;
            }

            if (principal.Context.Role == RoleCode.TransportAdministrator)
            {
                Response.Redirect(ResolveUrl("~/Admin/Dashboard.aspx"), true);
                return;
            }

            if (principal.Context.Role == RoleCode.Driver)
            {
                Response.Redirect(ResolveUrl("~/Driver/Today.aspx"), true);
                return;
            }


            if (principal.Context.Role == RoleCode.Passenger)
            {
                Response.Redirect(ResolveUrl("~/Passenger/Home.aspx"), true);
                return;
            }

            Response.Redirect(ResolveUrl("~/Errors/AccessDenied.aspx"), true);
        }
    }
}
