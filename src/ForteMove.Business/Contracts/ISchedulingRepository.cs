using System;
using System.Collections.Generic;
using ForteMove.Models.Scheduling;

namespace ForteMove.Business.Contracts
{
    public interface ISchedulingRepository
    {
        int GetNextScheduleCodeSequence();
        SchedulingCreationOptions GetCreationOptions();
        ScheduleCreationResult CreateSchedule(ScheduleCreationAggregate aggregate, long actorUserAccountId);
        IList<ScheduleListItem> GetScheduleList(ScheduleQuery query, DateTime operationalNow);
        ScheduleDetails GetScheduleDetails(long routeScheduleId, DateTime operationalNow);
        IList<TripListItem> GetTripList(TripQuery query);
        ScheduleChangeOptions GetChangeOptions(long routeScheduleId, DateTime? fromDate);
        ScheduleChangeResult ApplyScheduleChange(ScheduleChangeAggregate aggregate, long actorUserAccountId);
    }
}
