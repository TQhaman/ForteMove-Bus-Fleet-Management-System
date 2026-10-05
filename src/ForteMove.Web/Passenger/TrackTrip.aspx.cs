using System;
using System.Globalization;
using ForteMove.Models.Tracking;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Passenger { public partial class TrackTripPage:PassengerPage {
protected void Page_Load(object sender,EventArgs e) { long id;var snapshot=long.TryParse(Request.QueryString["ticketId"],NumberStyles.None,CultureInfo.InvariantCulture,out id)?ServiceFactory.CreateTrackingService().GetPassengerTrackingSnapshot(id,CurrentPrincipalContext.UserAccountId):null;
if(snapshot==null){pnlNotFound.Visible=true;tracking.Visible=false;Response.StatusCode=404;Response.TrySkipIisCustomErrors=true;return;}lnkTicket.NavigateUrl=ResolveUrl("~/Passenger/TicketDetails.aspx?id="+id.ToString(CultureInfo.InvariantCulture));
if(snapshot.UnavailableReason==TrackingUnavailableReason.TicketRefunded||snapshot.UnavailableReason==TrackingUnavailableReason.Cancelled){Response.Redirect(lnkTicket.NavigateUrl,true);return;}
tracking.SnapshotUrl=ResolveUrl("~/Tracking/Snapshot.ashx?ticketId="+id.ToString(CultureInfo.InvariantCulture)); }
} }
