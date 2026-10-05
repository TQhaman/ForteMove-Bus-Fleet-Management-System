using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using ForteMove.Business.Fuel;
using ForteMove.Models.Fuel;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Admin.Fuel
{
    public partial class RequestDetailsPage : AdminPage
    {
        private long Actor { get { return CurrentPrincipalContext.UserAccountId; } }
        private long Id { get { return FuelPresentation.Id(Request.QueryString["id"]); } }
        private void Missing() { Response.StatusCode=404;pnlMessage.Visible=true;litMessage.Text="This Fuel item is unavailable."; }
protected void Page_Load(object sender,EventArgs e) { if(!IsPostBack) Bind(true); }
        private void Bind(bool initial)
        {
            var item=ServiceFactory.CreateAdminFuelService().GetRequest(Id,Actor);if(item==null) { Missing();pnlActions.Visible=false;return; }
            litDetails.Text=FuelPresentation.Context(item.Context)+"<dl class='fuel-detail-grid'>"+FuelPresentation.Pair("Request",item.RequestCode)+FuelPresentation.Pair("Requested",FuelPresentation.Local(item.SubmittedUtc))+FuelPresentation.Pair("Odometer at request",item.OdometerAtRequest.ToString("N1")+" km")+FuelPresentation.Pair("Supply",FuelPolicy.SupplyLabel(item.SupplyType))+FuelPresentation.Pair("Status",item.Status)+FuelPresentation.Pair("Reason",item.Justification)+FuelPresentation.Pair("Administrator note",item.DecisionNote??item.CancellationReason??"None")+"</dl>";
            litUnit.Text=FuelPresentation.Encode(FuelPolicy.UnitLabel(FuelPolicy.Unit(item.SupplyType)));
            litWarnings.Text=FuelPresentation.Encode(FuelPresentation.Warnings(item.Context));pnlWarnings.Visible=litWarnings.Text.Length>0;
            pnlActions.Visible=item.Status==FuelRequestStatus.Pending;
            ViewState["RequestRv"]=item.RowVersion;ViewState["TripRv"]=item.Context.TripRowVersion;ViewState["AssignmentRv"]=item.Context.AssignmentRowVersion;ViewState["BusRv"]=item.Context.BusRowVersion;
            lnkVoucher.Visible=item.FuelVoucherId.HasValue;if(item.FuelVoucherId.HasValue) lnkVoucher.NavigateUrl="VoucherDetails.aspx?id="+item.FuelVoucherId;
            if(initial)
            {
                ddlStations.Items.Add(new ListItem("Select a compatible Station",""));
                foreach(var station in ServiceFactory.CreateFuelStationService().GetStations(Actor).Where(x=>FuelPolicy.Compatible(x,item.SupplyType)))
                    ddlStations.Items.Add(new ListItem(station.StationCode+" - "+station.StationName,station.FuelStationId.ToString(CultureInfo.InvariantCulture)));
                txtUntil.Text=item.Context.ExpectedFinishLocal.AddHours(2).ToString("yyyy-MM-ddTHH:mm",CultureInfo.InvariantCulture);
            }
            StationVersion();
        }
        private void StationVersion() { var station=ServiceFactory.CreateFuelStationService().GetStation(FuelPresentation.Id(ddlStations.SelectedValue),Actor);ViewState["StationRv"]=station==null?null:station.RowVersion; }
        protected void SelectStation(object sender,EventArgs e) { StationVersion(); }
        protected void Approve(object sender,EventArgs e)
        {
            var result=ServiceFactory.CreateAdminFuelService().Approve(new ApproveFuelRequest { FuelRequestId=Id,Quantity=FuelPresentation.Decimal(txtQuantity.Text),Amount=FuelPresentation.Decimal(txtAmount.Text),
                FuelStationId=FuelPresentation.Id(ddlStations.SelectedValue),ValidUntilLocal=FuelPresentation.Date(txtUntil.Text),Note=txtNote.Text,RequestRowVersion=ViewState["RequestRv"] as byte[],
                TripRowVersion=ViewState["TripRv"] as byte[],AssignmentRowVersion=ViewState["AssignmentRv"] as byte[],BusRowVersion=ViewState["BusRv"] as byte[],StationRowVersion=ViewState["StationRv"] as byte[] },Actor);
            if(result.Succeeded) { Response.Redirect(ResolveUrl("~/Admin/Fuel/VoucherDetails.aspx?id="+result.Value+"&approved=1"),true);return; }
            FuelPresentation.Feedback(result,pnlMessage,litMessage,"Voucher created.");Bind(false);
        }
        protected void Reject(object sender,EventArgs e)
        {
            var result=ServiceFactory.CreateAdminFuelService().Reject(new RejectFuelRequest { FuelRequestId=Id,Reason=txtReject.Text,RowVersion=ViewState["RequestRv"] as byte[] },Actor);
            if(result.Succeeded) { Response.Redirect(ResolveUrl("~/Admin/Fuel/Requests.aspx?rejected=1"),true);return; }
            FuelPresentation.Feedback(result,pnlMessage,litMessage,"Request rejected.");Bind(false);
        }
    }
}
