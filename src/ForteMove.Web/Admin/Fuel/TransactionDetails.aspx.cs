using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using ForteMove.Business.Fuel;
using ForteMove.Models.Fuel;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Admin.Fuel
{
    public partial class TransactionDetailsPage : AdminPage
    {
        private long Actor { get { return CurrentPrincipalContext.UserAccountId; } }
        private long Id { get { return FuelPresentation.Id(Request.QueryString["id"]); } }
        private void Missing() { Response.StatusCode=404;pnlMessage.Visible=true;litMessage.Text="This Fuel item is unavailable."; }
protected void Page_Load(object sender,EventArgs e)
        {
            var item=ServiceFactory.CreateAdminFuelService().GetTransaction(Id,Actor);if(item==null) { Missing();return; }
            litDetails.Text="<dl class='fuel-detail-grid'>"+FuelPresentation.Pair("Transaction",item.TransactionCode)+FuelPresentation.Pair("Voucher",item.VoucherCode)+
                FuelPresentation.Pair("Request",item.RequestCode)+FuelPresentation.Pair("Trip",item.TripCode)+FuelPresentation.Pair("Driver",item.EmployeeNumber+" - "+item.DriverName)+
                FuelPresentation.Pair("Bus",item.FleetNumber)+FuelPresentation.Pair("Supply",FuelPolicy.SupplyLabel(item.SupplyType))+
                FuelPresentation.Pair("Quantity",FuelPresentation.Quantity(item.Quantity,item.Unit))+FuelPresentation.Pair("Value",FuelPresentation.Money(item.Amount))+
                FuelPresentation.Pair("Station",item.StationCode+" - "+item.StationName)+FuelPresentation.Pair("Station area",item.StationArea)+
                FuelPresentation.Pair("Bus odometer at redemption",item.OdometerAtRedemption.ToString("N1")+" km")+FuelPresentation.Pair("Redeemed",FuelPresentation.Local(item.RedeemedUtc))+"</dl>";
            if(Request.QueryString["redeemed"]=="1") { pnlMessage.Visible=true;pnlMessage.CssClass="alert alert-success";litMessage.Text="Voucher redeemed. One Fuel Transaction recorded."; }
        }
    }
}
