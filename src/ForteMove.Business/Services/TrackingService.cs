using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ForteMove.Business.Contracts;
using ForteMove.Business.Time;
using ForteMove.Business.Tracking;
using ForteMove.Models.Tracking;

namespace ForteMove.Business.Services
{
    public sealed class TrackingService
    {
        private readonly ITrackingRepository repository;
        private readonly IClock clock;
        public TrackingService(ITrackingRepository repository) : this(repository,new SystemClock()) { }
        public TrackingService(ITrackingRepository repository,IClock clock) { if(repository==null)throw new ArgumentNullException("repository");if(clock==null)throw new ArgumentNullException("clock");this.repository=repository;this.clock=clock; }
        public IList<AdminTrackingListItem> GetAdminActiveTrips(TrackingQuery query,long actorUserAccountId)
        {
            DateTime now=clock.UtcNow; query=query??new TrackingQuery(); query.ServiceDate=(query.ServiceDate??clock.ToOperationalTime(now)).Date;
            return repository.GetAdminTrips(query,actorUserAccountId).Select(t=>new AdminTrackingListItem {TripId=t.TripId,Snapshot=Project<AdminTrackingSnapshot>(t,now)}).ToList();
        }
        public AdminTrackingSnapshot GetAdminTrackingSnapshot(long id,long user) { return Get<AdminTrackingSnapshot>(TrackingAudience.Administrator,id,user); }
        public DriverTrackingSnapshot GetDriverTrackingSnapshot(long id,long user) { return Get<DriverTrackingSnapshot>(TrackingAudience.Driver,id,user); }
        public PassengerTrackingSnapshot GetPassengerTrackingSnapshot(long ticketId,long user) { return Get<PassengerTrackingSnapshot>(TrackingAudience.Passenger,ticketId,user); }
        private T Get<T>(TrackingAudience audience,long id,long user) where T:TrackingSnapshot,new() { if(id<=0||user<=0)return null;DateTime now=clock.UtcNow;var t=repository.GetTrip(audience,user,id);return t==null?null:Project<T>(t,now); }
        private T Project<T>(TrackingTimeline t,DateTime now) where T:TrackingSnapshot,new()
        {
            var result=new T {TripCode=t.TripCode,RouteCode=t.RouteCode,RouteName=t.RouteName,FleetNumber=t.FleetNumber,
                ScheduledDepartureLocal=Local(t.ServiceDate.Date.Add(t.DepartureTime)),ExpectedFinishLocal=Local(t.ExpectedFinishLocal),
                ActualStartLocal=t.ActualStartUtc.HasValue?Local(clock.ToOperationalTime(t.ActualStartUtc.Value)):null,
                ActualCompletionLocal=t.ActualCompletionUtc.HasValue?Local(clock.ToOperationalTime(t.ActualCompletionUtc.Value)):null,
                LastCalculatedLocalTime=clock.ToOperationalTime(now).ToString("dd MMM yyyy HH:mm:ss",CultureInfo.GetCultureInfo("en-ZA"))};
            TrackingCalculator.Calculate(t,result,now);
            var admin=result as AdminTrackingSnapshot;
            if(admin!=null) { admin.DriverName=t.DriverName;admin.EmployeeNumber=t.EmployeeNumber;var attention=new List<string>();
                if(result.Availability==TrackingAvailability.Unavailable)attention.Add(result.AvailabilityMessage);
                if(t.RequiresReview)attention.Add("Service requires administrator review.");
                if(t.HasCriticalDefect)attention.Add("Assigned Bus has an unresolved Critical defect.");
                if(t.Pauses.Any(p=>p.IsCannotProceed&&!p.EndedUtc.HasValue))attention.Add("Cannot Proceed awaits resolution.");
                if(!t.ActualStartUtc.HasValue&&t.ServiceDate.Date.Add(t.DepartureTime)<clock.ToOperationalTime(now))attention.Add("Departure passed - Trip not started.");
                admin.RequiresAttention=attention.Count>0;admin.AttentionMessage=string.Join(" ",attention); }
            return result;
        }
        private static string Local(DateTime value) { return value.ToString("dd MMM yyyy HH:mm",CultureInfo.GetCultureInfo("en-ZA")); }
    }
}
