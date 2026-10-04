using System;
using System.Globalization;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using ForteMove.Business.Services;
using ForteMove.Models.Security;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Account
{
    public partial class Login : Page
    {
        protected override void OnInit(EventArgs e)
        {
            ViewStateUserKey = Session.SessionID;
            base.OnInit(e);
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            if (IsPostBack)
            {
                return;
            }

            ForteMovePrincipal principal = Context.User as ForteMovePrincipal;
            if (principal != null && principal.Identity.IsAuthenticated)
            {
                RedirectAuthenticatedUser(principal.Context);
            }
        }

        protected void btnSignIn_Click(object sender, EventArgs e)
        {
            pnlError.Visible = false;

            AuthenticationService authenticationService = ServiceFactory.CreateAuthenticationService();
            AuthenticationResult result = authenticationService.Authenticate(new LoginRequest
            {
                Email = txtEmail.Text,
                Password = txtPassword.Text,
                ClientIpAddress = ClientIpAddress.From(Request)
            });

            if (!result.Succeeded)
            {
                ShowError(result.ErrorMessage);
                return;
            }

            IssueAuthenticationCookie(result.Principal.UserAccountId);
            RedirectAuthenticatedUser(result.Principal);
        }

        private void IssueAuthenticationCookie(long userAccountId)
        {
            DateTime issuedAt = DateTime.Now;
            FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(
                1,
                userAccountId.ToString(CultureInfo.InvariantCulture),
                issuedAt,
                issuedAt.AddMinutes(30),
                false,
                string.Empty,
                FormsAuthentication.FormsCookiePath);

            HttpCookie cookie = new HttpCookie(
                FormsAuthentication.FormsCookieName,
                FormsAuthentication.Encrypt(ticket));
            cookie.HttpOnly = true;
            cookie.Secure = true;
            cookie.SameSite = SameSiteMode.Lax;
            cookie.Path = FormsAuthentication.FormsCookiePath;
            Response.Cookies.Add(cookie);
        }

        private void RedirectAuthenticatedUser(PrincipalContext principal)
        {
            if (principal.MustChangePassword)
            {
                Response.Redirect(ResolveUrl("~/Account/ChangePassword.aspx"), true);
                return;
            }

            if (principal.Role == RoleCode.Driver)
            {
                Response.Redirect(ResolveUrl("~/Driver/Today.aspx"), true);
                return;
            }

            if (principal.Role != RoleCode.TransportAdministrator)
            {
                Response.Redirect(ResolveUrl("~/Errors/AccessDenied.aspx"), true);
                return;
            }

            string returnUrl = SafeRedirects.GetAdminReturnUrl(Request.QueryString["returnUrl"]);
            Response.Redirect(returnUrl ?? ResolveUrl("~/Admin/Dashboard.aspx"), true);
        }

        private void ShowError(string message)
        {
            pnlError.Visible = true;
            string safeMessage = string.IsNullOrWhiteSpace(message)
                ? AuthenticationService.GenericAuthenticationError
                : message;
            lblError.Text = Server.HtmlEncode(safeMessage);
        }
    }
}
