using System;
using System.Globalization;
using ForteMove.Models.Passengers;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Passenger
{
    public partial class TicketDetailsPage : PassengerPage
    {
        protected void Page_Load(object sender,EventArgs e){if(!IsPostBack)BindPage();}
        private void BindPage()
        {
            long id; if(!long.TryParse(Request.QueryString["id"],NumberStyles.None,CultureInfo.InvariantCulture,out id)){pnlNotFound.Visible=true;return;}
            TicketDetails details=ServiceFactory.CreatePassengerService().GetTicketDetails(CurrentPrincipalContext.UserAccountId,id);if(details==null){pnlNotFound.Visible=true;return;}
            TicketListItem item=details.Ticket;pnlDetails.Visible=true;pnlPurchased.Visible=Request.QueryString["purchased"]=="1";
            bool past=item.TicketStatus==TicketStatus.Purchased&&(item.TripStatus==ForteMove.Models.Scheduling.TripStatus.Completed||item.ServiceDate.Date.Add(item.ScheduledDepartureTime)<=ServiceFactory.CreatePassengerService().OperationalNow);
            string label=item.TicketStatus==TicketStatus.Refunded?"Refunded":past?"Past ticket":"Purchased";
            litTicketCode.Text=Server.HtmlEncode(item.TicketCode);litStatus.Text=Server.HtmlEncode(label);litTicketBadge.Text=Server.HtmlEncode(label);
            litRoute.Text=Server.HtmlEncode(item.RouteCode+" - "+item.RouteName);litEndpoints.Text=Server.HtmlEncode(item.OriginName+" to "+item.DestinationName);litService.Text=Server.HtmlEncode(item.ServiceDate.ToString("ddd, d MMM yyyy",CultureInfo.CurrentCulture)+" at "+item.ScheduledDepartureTime.ToString("hh\\:mm"));litFinish.Text=Server.HtmlEncode(item.ExpectedFinishLocal.ToString("ddd, d MMM HH:mm",CultureInfo.CurrentCulture));litFare.Text=item.FareAmount.ToString("C",CultureInfo.CurrentCulture);litFleet.Text=Server.HtmlEncode(string.IsNullOrWhiteSpace(item.FleetNumber)?"Assignment pending":item.FleetNumber);
            pnlDelay.Visible=item.EstimatedDelayMinutes.HasValue;if(item.EstimatedDelayMinutes.HasValue)litDelay.Text=Server.HtmlEncode("Estimated delay: "+item.EstimatedDelayMinutes.Value.ToString(CultureInfo.CurrentCulture)+" minutes.");
            pnlRefund.Visible=item.TicketStatus==TicketStatus.Refunded;if(pnlRefund.Visible)litRefund.Text=Server.HtmlEncode(details.RefundReason??"The fare was returned to your wallet.");
            lnkTrackTrip.Visible=item.TicketStatus==TicketStatus.Purchased && item.TripStatus!=ForteMove.Models.Scheduling.TripStatus.Cancelled;
            lnkTrackTrip.NavigateUrl=ResolveUrl("~/Passenger/TrackTrip.aspx?ticketId="+id.ToString(CultureInfo.InvariantCulture));
        }
    }
}
