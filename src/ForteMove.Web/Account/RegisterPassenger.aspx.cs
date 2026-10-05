using System;
using System.Linq;
using System.Web.UI;
using ForteMove.Models.Passengers;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Account
{
    public partial class RegisterPassenger : Page
    {
        protected override void OnInit(EventArgs e)
        {
            ViewStateUserKey = Session.SessionID;
            base.OnInit(e);
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            ForteMovePrincipal principal = Context.User as ForteMovePrincipal;
            if (!IsPostBack && principal != null && principal.Identity.IsAuthenticated)
                Response.Redirect(ResolveUrl("~/Default.aspx"), true);
        }

        protected void btnRegister_Click(object sender, EventArgs e)
        {
            var result = ServiceFactory.CreatePassengerRegistrationService().Register(
                new RegisterPassengerRequest
                {
                    FirstName = txtFirstName.Text,
                    LastName = txtLastName.Text,
                    Email = txtEmail.Text,
                    PhoneNumber = txtPhone.Text,
                    Password = txtPassword.Text,
                    ConfirmPassword = txtConfirmPassword.Text,
                    ClientIpAddress = ClientIpAddress.From(Request)
                });
            if (!result.Succeeded)
            {
                rptErrors.DataSource = result.Errors.ToList();
                rptErrors.DataBind();
                pnlErrors.Visible = true;
                return;
            }
            Response.Redirect(ResolveUrl("~/Account/Login.aspx?registered=1"), true);
        }
    }
}
