using System;
using ForteMove.Models.Passengers;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Passenger
{
    public partial class Account : PassengerPage
    {
        protected void Page_Load(object sender,EventArgs e)
        {
            if(IsPostBack)return;PassengerAccountDetails value=ServiceFactory.CreatePassengerService().GetAccount(CurrentPrincipalContext.UserAccountId);if(value==null){Response.Redirect(ResolveUrl("~/Errors/Unexpected.aspx"),true);return;}litName.Text=Server.HtmlEncode((value.FirstName+" "+value.LastName).Trim());litEmail.Text=Server.HtmlEncode(value.Email);litPhone.Text=Server.HtmlEncode(string.IsNullOrWhiteSpace(value.PhoneNumber)?"Not provided":value.PhoneNumber);litStatus.Text=value.AccountIsActive?"Active":"Inactive";
        }
    }
}
