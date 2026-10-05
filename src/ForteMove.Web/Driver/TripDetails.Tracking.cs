using System;
using System.Globalization;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Driver
{
    public partial class TripDetailsPage
    {
        protected override void OnPreRender(EventArgs e)
        {
            // Recheck current assignment ownership, including after an operational POST.
            tracking.Visible = ServiceFactory.CreateTrackingService()
                .GetDriverTrackingSnapshot(Id, CurrentPrincipalContext.UserAccountId) != null;
            tracking.SnapshotUrl = ResolveUrl("~/Tracking/Snapshot.ashx?tripId=" + Id.ToString(CultureInfo.InvariantCulture));
            base.OnPreRender(e);
        }
    }
}
