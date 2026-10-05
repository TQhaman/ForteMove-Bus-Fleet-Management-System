using System;
using System.Globalization;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Admin.Tracking { public partial class TripTrackingPage:AdminPage {
protected void Page_Load(object sender,EventArgs e) { long id;var snapshot=long.TryParse(Request.QueryString["id"],NumberStyles.None,CultureInfo.InvariantCulture,out id)?ServiceFactory.CreateTrackingService().GetAdminTrackingSnapshot(id,CurrentPrincipalContext.UserAccountId):null;
if(snapshot==null){pnlNotFound.Visible=true;tracking.Visible=false;Response.StatusCode=404;Response.TrySkipIisCustomErrors=true;return;}tracking.SnapshotUrl=ResolveUrl("~/Tracking/Snapshot.ashx?tripId="+id.ToString(CultureInfo.InvariantCulture));pnlAttention.Visible=true;litAttention.Text="To correct shared Stop coordinates, open the Route's details. "; }
} }
