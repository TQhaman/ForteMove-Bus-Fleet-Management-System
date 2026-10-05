using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using ForteMove.Business.Fuel;
using ForteMove.Models.Fuel;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Admin.Fuel
{
    public partial class PrototypeTerminalPage : AdminPage
    {
        private long Actor { get { return CurrentPrincipalContext.UserAccountId; } }
        private long Id { get { return FuelPresentation.Id(Request.QueryString["id"]); } }
        private void Missing() { Response.StatusCode=404;pnlMessage.Visible=true;litMessage.Text="This Fuel item is unavailable."; }
protected void Page_Load(object sender,EventArgs e)
        {
            if(IsPostBack) return;ddlStations.Items.Add(new ListItem("Choose the demonstration Station",""));
            foreach(var station in ServiceFactory.CreateFuelStationService().GetStations(Actor).Where(x=>x.IsActive))
                ddlStations.Items.Add(new ListItem(station.StationCode+" - "+station.StationName,station.FuelStationId.ToString(CultureInfo.InvariantCulture)));
        }
        protected void Review(object sender,EventArgs e)
        {
            pnlReview.Visible=false;ViewState.Remove("Token");ddlStations.Enabled=true;
            var service=ServiceFactory.CreateAdminFuelService();
            var result=service.PreviewRedemption(txtToken.Text,FuelPresentation.Id(ddlStations.SelectedValue),Actor);
            if(!result.Succeeded) { FuelPresentation.Feedback(result,pnlMessage,litMessage,"");return; }
            var preview=result.Value;pnlReview.Visible=true;litDetails.Text=FuelPresentation.Voucher(preview.Voucher);litWarnings.Text=FuelPresentation.Encode(string.Join(" ",preview.Warnings));
            ViewState["Token"]=txtToken.Text.Trim();ViewState["Station"]=preview.Voucher.FuelStationId;ViewState["VoucherRv"]=preview.Voucher.RowVersion;
            ViewState["StationRv"]=preview.StationRowVersion;ViewState["Fingerprint"]=preview.Fingerprint;
            ddlStations.Enabled=false;txtToken.Text="";pnlMessage.Visible=false;
        }
        protected void Confirm(object sender,EventArgs e)
        {
            var result=ServiceFactory.CreateAdminFuelService().Redeem(new RedeemFuelVoucherRequest { Token=ViewState["Token"] as string,
                FuelStationId=ViewState["Station"] is long?(long)ViewState["Station"]:0,VoucherRowVersion=ViewState["VoucherRv"] as byte[],
                StationRowVersion=ViewState["StationRv"] as byte[],PreviewFingerprint=ViewState["Fingerprint"] as string },Actor);
            ViewState.Remove("Token");pnlReview.Visible=false;ddlStations.Enabled=true;
            if(result.Succeeded) { Response.Redirect(ResolveUrl("~/Admin/Fuel/TransactionDetails.aspx?id="+result.Value.FuelTransactionId+"&redeemed=1"),true);return; }
            FuelPresentation.Feedback(result,pnlMessage,litMessage,"Voucher redeemed.");
        }
    }
}
