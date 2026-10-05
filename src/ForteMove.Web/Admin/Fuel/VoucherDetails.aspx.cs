using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using ForteMove.Business.Fuel;
using ForteMove.Models.Fuel;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Admin.Fuel
{
    public partial class VoucherDetailsPage : AdminPage
    {
        private long Actor { get { return CurrentPrincipalContext.UserAccountId; } }
        private long Id { get { return FuelPresentation.Id(Request.QueryString["id"]); } }
        private void Missing() { Response.StatusCode=404;pnlMessage.Visible=true;litMessage.Text="This Fuel item is unavailable."; }
protected void Page_Load(object sender,EventArgs e)
        {
            var service=ServiceFactory.CreateAdminFuelService();var item=service.GetVoucher(Id,Actor);if(item==null) { Missing();return; }
            litDetails.Text=FuelPresentation.Voucher(item);litWarnings.Text=FuelPresentation.Encode(FuelPresentation.Warnings(item.Request.Context));pnlWarnings.Visible=litWarnings.Text.Length>0;
            if(Request.QueryString["approved"]=="1") { pnlMessage.Visible=true;pnlMessage.CssClass="alert alert-success";litMessage.Text="Request approved. One-use Fuel Voucher created."; }
        }
    }
}
