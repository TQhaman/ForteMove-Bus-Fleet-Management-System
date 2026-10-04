using System;
using System.Globalization;
using System.Threading;
using System.Web;
using System.Web.Security;
using ForteMove.Business.Services;
using ForteMove.Models.Security;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web
{
    public class Global : HttpApplication
    {
        protected void Application_PostAuthenticateRequest(object sender, EventArgs e)
        {
            HttpCookie authenticationCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authenticationCookie == null || string.IsNullOrWhiteSpace(authenticationCookie.Value))
            {
                return;
            }

            FormsAuthenticationTicket ticket;
            try
            {
                ticket = FormsAuthentication.Decrypt(authenticationCookie.Value);
            }
            catch
            {
                ExpireAuthenticationCookie();
                return;
            }

            long userAccountId;
            if (ticket == null ||
                ticket.Expired ||
                !long.TryParse(ticket.Name, NumberStyles.None, CultureInfo.InvariantCulture, out userAccountId))
            {
                ExpireAuthenticationCookie();
                return;
            }

            AuthenticationService authenticationService = ServiceFactory.CreateAuthenticationService();
            PrincipalContext principalContext = authenticationService.GetPrincipalContext(userAccountId);
            if (!AuthenticationService.IsCurrentPrincipalValid(principalContext))
            {
                ExpireAuthenticationCookie();
                return;
            }

            ForteMovePrincipal principal = new ForteMovePrincipal(principalContext);
            Context.User = principal;
            Thread.CurrentPrincipal = principal;
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            string path = Request.AppRelativeCurrentExecutionFilePath ?? string.Empty;
            if (path.StartsWith("~/Errors/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Server.ClearError();
            Response.Clear();
            Response.StatusCode = 500;
            Response.TrySkipIisCustomErrors = true;
            Server.Transfer("~/Errors/Unexpected.aspx");
        }

        private void ExpireAuthenticationCookie()
        {
            FormsAuthentication.SignOut();
            Context.User = null;
            Thread.CurrentPrincipal = null;
        }
    }
}
