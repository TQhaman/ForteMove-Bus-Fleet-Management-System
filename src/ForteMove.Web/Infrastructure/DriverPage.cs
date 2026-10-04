using System;
using System.Globalization;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using ForteMove.Models.Security;

namespace ForteMove.Web.Infrastructure
{
    public abstract class DriverPage : Page
    {
        protected ForteMovePrincipal CurrentForteMovePrincipal { get { return Context.User as ForteMovePrincipal; } }
        protected PrincipalContext CurrentPrincipalContext { get { return CurrentForteMovePrincipal == null ? null : CurrentForteMovePrincipal.Context; } }
        protected override void OnInit(EventArgs e)
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache); Response.Cache.SetNoStore();
            ForteMovePrincipal principal=CurrentForteMovePrincipal;
            if(principal==null||!principal.Identity.IsAuthenticated){FormsAuthentication.SignOut();Response.Redirect(ResolveUrl(FormsAuthentication.LoginUrl),true);return;}
            if(principal.Context.MustChangePassword){Response.Redirect(ResolveUrl("~/Account/ChangePassword.aspx"),true);return;}
            if(!principal.IsInRole(RoleCode.Driver.ToString())){Response.Redirect(ResolveUrl("~/Errors/AccessDenied.aspx"),true);return;}
            ViewStateUserKey=principal.Context.UserAccountId.ToString(CultureInfo.InvariantCulture);base.OnInit(e);
        }
    }
}
