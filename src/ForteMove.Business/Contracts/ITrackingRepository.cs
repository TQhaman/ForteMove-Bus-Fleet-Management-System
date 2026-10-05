using System;
using System.Collections.Generic;
using ForteMove.Models.Tracking;

namespace ForteMove.Business.Contracts
{
    public interface ITrackingRepository
    {
        IList<TrackingTimeline> GetAdminTrips(TrackingQuery query, long userAccountId);
        TrackingTimeline GetTrip(TrackingAudience audience, long userAccountId, long resourceId);
    }
}
