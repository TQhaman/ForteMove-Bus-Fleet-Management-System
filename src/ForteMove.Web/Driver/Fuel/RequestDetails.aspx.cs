using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using ForteMove.Business.Fuel;
using ForteMove.Models.Fuel;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Driver.Fuel
{
    public partial class RequestDetailsPage : DriverPage
    {
        private long Actor { get { return CurrentPrincipalContext.UserAccountId; } }
        private long Id { get { return FuelPresentation.Id(Request.QueryString["id"]); } }
        private void Missing() { Response.StatusCode=404;pnlMessage.Visible=true;litMessage.Text="This Fuel item is unavailable."; }
protected void Page_Load(object sender,EventArgs e)
        {
            var item=ServiceFactory.CreateDriverFuelService().GetRequest(Id,Actor);if(item==null) { Missing();return; }
            litDetails.Text=FuelPresentation.Context(item.Context)+"<dl class='fuel-detail-grid'>"+FuelPresentation.Pair("Request",item.RequestCode)+FuelPresentation.Pair("Status",item.Status)+
                FuelPresentation.Pair("Requested",FuelPresentation.Local(item.SubmittedUtc))+FuelPresentation.Pair("Supply",FuelPolicy.SupplyLabel(item.SupplyType))+
                FuelPresentation.Pair("Reason",item.Justification)+FuelPresentation.Pair("Administrator note",item.DecisionNote??item.CancellationReason??"None")+"</dl>";
            lnkVoucher.Visible=item.FuelVoucherId.HasValue;
            if(item.FuelVoucherId.HasValue) lnkVoucher.NavigateUrl="VoucherDetails.aspx?id="+item.FuelVoucherId;
            if(Request.QueryString["submitted"]=="1") { pnlMessage.Visible=true;pnlMessage.CssClass="alert alert-success";litMessage.Text="Fuel Request submitted."; }
        }
    }
}
