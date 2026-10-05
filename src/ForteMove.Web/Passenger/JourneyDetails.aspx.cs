using System;
using System.Globalization;
using ForteMove.Models.Passengers;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Passenger
{
    public partial class JourneyDetailsPage : PassengerPage
    {
        private long TripId { get { long value; return long.TryParse(Request.QueryString["id"],NumberStyles.None,CultureInfo.InvariantCulture,out value)?value:0; } }
        private Guid OperationToken { get { return ViewState["OperationToken"]==null?Guid.Empty:(Guid)ViewState["OperationToken"]; } set { ViewState["OperationToken"]=value; } }
        private decimal ReviewedFare { get { return ViewState["ReviewedFare"]==null?0m:(decimal)ViewState["ReviewedFare"]; } set { ViewState["ReviewedFare"]=value; } }
        private byte[] TripRowVersion { get { return ViewState["TripRv"] as byte[]; } set { ViewState["TripRv"]=value; } }
        private byte[] RouteRowVersion { get { return ViewState["RouteRv"] as byte[]; } set { ViewState["RouteRv"]=value; } }
        private byte[] WalletRowVersion { get { return ViewState["WalletRv"] as byte[]; } set { ViewState["WalletRv"]=value; } }
        private string PreviewFingerprint { get { return ViewState["PreviewFingerprint"] as string; } set { ViewState["PreviewFingerprint"]=value; } }

        protected void Page_Load(object sender, EventArgs e) { if(!IsPostBack){OperationToken=Guid.NewGuid();BindPage();} }

        private void BindPage()
        {
            var result=ServiceFactory.CreatePassengerService().GetJourneyDetails(CurrentPrincipalContext.UserAccountId,TripId,OperationToken);
            if(!result.Succeeded){pnlNotFound.Visible=true;pnlDetails.Visible=false;return;}
            JourneyDetails details=result.Value; PassengerJourneyCandidate item=details.Candidate;
            pnlDetails.Visible=true;pnlNotFound.Visible=false;
            litRoute.Text=Server.HtmlEncode(item.RouteCode+" - "+item.RouteName);litEndpoints.Text=Server.HtmlEncode(item.OriginName+" to "+item.DestinationName);
            litDate.Text=Server.HtmlEncode(item.ServiceDate.ToString("dddd, d MMMM yyyy",CultureInfo.CurrentCulture));litDeparture.Text=item.ScheduledDepartureTime.ToString("hh\\:mm");litFinish.Text=Server.HtmlEncode(item.ExpectedFinishLocal.ToString("ddd d MMM, HH:mm",CultureInfo.CurrentCulture));litStatus.Text=Server.HtmlEncode(item.TripStatus.ToString().Replace("InProgress","In progress"));litFare.Text=item.DefaultFare.ToString("C",CultureInfo.CurrentCulture);litSeats.Text=details.RemainingSeats.ToString("N0",CultureInfo.CurrentCulture);
            pnlDelay.Visible=item.EstimatedDelayMinutes.HasValue;if(item.EstimatedDelayMinutes.HasValue)litDelay.Text=Server.HtmlEncode("Estimated delay: "+item.EstimatedDelayMinutes.Value.ToString(CultureInfo.CurrentCulture)+" minutes.");
            litWalletBalance.Text=details.WalletBalance.ToString("C",CultureInfo.CurrentCulture);litBalanceAfter.Text=details.BalanceAfterPurchase.ToString("C",CultureInfo.CurrentCulture);
            pnlUnavailable.Visible=!details.CanPurchase;litUnavailable.Text=Server.HtmlEncode(details.SaleUnavailableReason);pnlInsufficient.Visible=details.CanPurchase&&details.WalletBalance<item.DefaultFare;btnPurchase.Visible=details.CanPurchase&&details.WalletBalance>=item.DefaultFare;
            ReviewedFare=item.DefaultFare;TripRowVersion=item.TripRowVersion;RouteRowVersion=item.RouteRowVersion;WalletRowVersion=details.WalletRowVersion;PreviewFingerprint=details.PreviewFingerprint;OperationToken=details.OperationToken;
        }

        protected void btnPurchase_Click(object sender, EventArgs e)
        {
            var result=ServiceFactory.CreatePassengerService().PurchaseTicket(CurrentPrincipalContext.UserAccountId,new PurchaseTicketRequest{TripId=TripId,ReviewedFare=ReviewedFare,TripRowVersion=TripRowVersion,RouteRowVersion=RouteRowVersion,WalletRowVersion=WalletRowVersion,OperationToken=OperationToken,PreviewFingerprint=PreviewFingerprint,ClientIpAddress=ClientIpAddress.From(Request)});
            if(result.Succeeded){Response.Redirect(ResolveUrl("~/Passenger/TicketDetails.aspx?id="+result.Value.TicketId.ToString(CultureInfo.InvariantCulture)+"&purchased=1"),true);return;}
            rptErrors.DataSource=result.Errors;rptErrors.DataBind();pnlMessage.Visible=true;BindPage();
        }
    }
}
