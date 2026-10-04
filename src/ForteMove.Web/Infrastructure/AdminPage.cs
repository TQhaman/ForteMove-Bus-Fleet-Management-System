using System;
using System.Globalization;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using ForteMove.Models.Security;

namespace ForteMove.Web.Infrastructure
{
    public abstract class AdminPage : Page
    {
        public ForteMovePrincipal CurrentForteMovePrincipal
        {
            get { return Context.User as ForteMovePrincipal; }
        }

        public PrincipalContext CurrentPrincipalContext
        {
            get
            {
                ForteMovePrincipal principal = CurrentForteMovePrincipal;
                return principal == null ? null : principal.Context;
            }
        }

        protected override void OnInit(EventArgs e)
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);

            ForteMovePrincipal principal = CurrentForteMovePrincipal;
            if (principal == null || !principal.Identity.IsAuthenticated)
            {
                FormsAuthentication.SignOut();
                string loginUrl = ResolveUrl(FormsAuthentication.LoginUrl);
                string separator = loginUrl.IndexOf('?') >= 0 ? "&" : "?";
                Response.Redirect(
                    loginUrl + separator + "returnUrl=" + Server.UrlEncode(Request.RawUrl),
                    true);
                return;
            }

            if (!principal.IsInRole(RoleCode.TransportAdministrator.ToString()))
            {
                Response.Redirect(ResolveUrl("~/Errors/AccessDenied.aspx"), true);
                return;
            }

            if (principal.Context.MustChangePassword)
            {
                Response.Redirect(ResolveUrl("~/Account/ChangePassword.aspx"), true);
                return;
            }

            ViewStateUserKey = principal.Context.UserAccountId.ToString(CultureInfo.InvariantCulture);
            base.OnInit(e);
        }
    }
}
