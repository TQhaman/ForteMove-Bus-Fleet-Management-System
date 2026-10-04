using System;
using System.Globalization;
using ForteMove.Models.Drivers;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Admin.Drivers
{
    public partial class DriverDetailsPage : AdminPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;
            long id; if (!long.TryParse(Request.QueryString["id"], out id)) { Response.Redirect(ResolveUrl("~/Admin/Drivers/DriverList.aspx"), true); return; }
            ForteMove.Models.Drivers.Driver driver = ServiceFactory.CreateDriverService().GetDriver(id);
            if (driver == null) { Response.Redirect(ResolveUrl("~/Admin/Drivers/DriverList.aspx"), true); return; }
            FlashMessage message=FlashMessageStore.Take(Session); if(message!=null){litFlash.Text=Server.HtmlEncode(message.Text);pnlFlash.CssClass=message.IsWarning?"alert alert-warning":"alert alert-success";pnlFlash.Visible=true;}
            litName.Text=Server.HtmlEncode(driver.FullName);litEmployeeNumber.Text=Server.HtmlEncode(driver.EmployeeNumber);litEmail.Text=Server.HtmlEncode(driver.Email);litPhone.Text=Server.HtmlEncode(driver.PhoneNumber??"Not provided");
            litAvailability.Text=Server.HtmlEncode(driver.AvailabilityStatus==DriverAvailabilityStatus.OnLeave?"On leave":driver.AvailabilityStatus.ToString());litDob.Text=driver.DateOfBirth.ToString("d",CultureInfo.CurrentCulture);
            litLicence.Text=Server.HtmlEncode(driver.LicenceCode+" · "+driver.LicenceNumber);litLicenceExpiry.Text=driver.LicenceExpiryDate.ToString("d",CultureInfo.CurrentCulture);litPrdp.Text=Server.HtmlEncode("P · "+driver.PrdpNumber);litPrdpExpiry.Text=driver.PrdpExpiryDate.ToString("d",CultureInfo.CurrentCulture);litCredentialStatus.Text=Server.HtmlEncode(driver.CredentialStatus==CredentialStatus.ExpiringSoon?"Expiring soon":driver.CredentialStatus.ToString());lnkEdit.NavigateUrl="~/Admin/Drivers/EditDriver.aspx?id="+id;
        }
    }
}
