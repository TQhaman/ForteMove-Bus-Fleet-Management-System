using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using ForteMove.Business.Fuel;
using ForteMove.Models.Fuel;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Driver.Fuel
{
    public partial class VoucherDetailsPage : DriverPage
    {
        private long Actor { get { return CurrentPrincipalContext.UserAccountId; } }
        private long Id { get { return FuelPresentation.Id(Request.QueryString["id"]); } }
        private void Missing() { Response.StatusCode=404;pnlMessage.Visible=true;litMessage.Text="This Fuel item is unavailable."; }
protected void Page_Load(object sender,EventArgs e)
        {
            var service=ServiceFactory.CreateDriverFuelService();var item=service.GetVoucher(Id,Actor);if(item==null) { Missing();pnlToken.Visible=false;return; }
            litDetails.Text=FuelPresentation.Voucher(item);litWarnings.Text=FuelPresentation.Encode(FuelPresentation.Warnings(item.Request.Context));pnlWarnings.Visible=litWarnings.Text.Length>0;
            var token=service.GetVoucherToken(Id,Actor);pnlToken.Visible=token.Succeeded;if(token.Succeeded) { txtToken.Text=token.Value;imgQr.ImageUrl="VoucherQr.ashx?id="+Id; }
        }
    }
}
