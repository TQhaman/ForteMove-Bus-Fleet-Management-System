using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;
using ForteMove.Business.Contracts;
using ForteMove.Business.Routing;
using ForteMove.Business.Services;
using ForteMove.Business.Time;
using ForteMove.Business.Tracking;
using ForteMove.Models.Passengers;
using ForteMove.Models.Scheduling;
using ForteMove.Models.Tracking;

internal static class Slice7BusinessVerification
{
    private static int checks;
    private static readonly DateTime Start = new DateTime(2026,10,5,12,0,0,DateTimeKind.Utc);
    public static int Main()
    {
        try
        {
            var t=Trip(); var s=Calculate(t,30);
            Check(s.CurrentLatitude==null && s.ProgressPercent==0,"Scheduled departure passing never starts movement");
            t.Status=TripStatus.Ready;s=Calculate(t,30);Check(s.CurrentLongitude==0 && s.IsMovementFrozen,"Ready stays at simulated origin");
            t.Status=TripStatus.Delayed;s=Calculate(t,30);Check(s.CurrentLongitude==0 && s.ProgressPercent==0,"Pre-start delay stays at origin");
            t.HasCurrentAssignment=false;s=Calculate(t,30);Check(s.UnavailableReason==TrackingUnavailableReason.NoAssignment&&s.CanPoll,"Assignment pending remains refreshable");
            t=Started();s=Calculate(t,15);Near(s.ProgressPercent,25,"Actual start is movement start");Near(s.CurrentLongitude,.5,"Haversine segment interpolation");
            s=Calculate(t,120);Near(s.ProgressPercent,99,"Unfinished progress capped at 99 percent");Check(s.IsOverrunning,"Overrun is identified");
            t.Status=TripStatus.Completed;t.ActualCompletionUtc=Start.AddMinutes(10);s=Calculate(t,10);Near(s.ProgressPercent,100,"Early completion snaps destination");Check(s.CurrentLongitude==2 && !s.CanPoll,"Completed terminal destination");
            t.Status=TripStatus.Cancelled;s=Calculate(t,20);Check(s.CurrentLatitude==null&&!s.CanPoll,"Cancelled no marker or polling");
            t=Started();t.Status=TripStatus.Delayed;t.Pauses.Add(Pause(12,null,false));s=Calculate(t,27);Near(s.ProgressPercent,20,"Open in-trip delay freezes at delay start");Check(s.IsMovementFrozen,"Delay freeze flag");
            t.Pauses[0].EndedUtc=Start.AddMinutes(22);t.Status=TripStatus.InProgress;s=Calculate(t,27);Near(s.ProgressPercent,17d/60*100,"Resume excludes delay time");
            t.Pauses.Add(Pause(15,25,true));s=Calculate(t,30);Near(s.ProgressPercent,17d/60*100,"Delay and Cannot Proceed union counts overlap once");
            t.Pauses.Add(Pause(27,null,true));s=Calculate(t,40);Near(s.ProgressPercent,14d/60*100,"Open Cannot Proceed freezes movement");Check(s.IsMovementFrozen,"Cannot Proceed freeze flag");
            t.Pauses[2].EndedUtc=Start.AddMinutes(37);s=Calculate(t,40);Near(s.ProgressPercent,17d/60*100,"Cannot Proceed resolution resumes without jump");
            t=Started();t.Pauses.Add(new TrackingPause {IsInTrip=false,StartedUtc=Start.AddMinutes(-10),EndedUtc=Start.AddMinutes(4)});s=Calculate(t,30);Near(s.ProgressPercent,50,"Pre-start delay is excluded from pause subtraction");
            t.HasCriticalDefect=true;s=Calculate(t,30);Near(s.ProgressPercent,50,"Critical defect alone does not freeze");Check(!s.IsMovementFrozen&&s.SafeStatus.Contains("operational issue"),"Critical defect only changes safe message");
            t=Started();t.Stops[0].EstimatedMinutesFromOrigin=0;t.Stops[1].EstimatedMinutesFromOrigin=10;t.Stops[2].EstimatedMinutesFromOrigin=60;s=Calculate(t,10);Near(s.CurrentLongitude,1,"Valid stop timings take precedence");
            t.Stops[1].EstimatedMinutesFromOrigin=70;s=Calculate(t,15);Near(s.CurrentLongitude,.5,"Invalid timings fall back to Haversine");
            t.Stops[1].EstimatedMinutesFromOrigin=null;s=Calculate(t,15);Near(s.CurrentLongitude,.5,"Incomplete timings fall back to Haversine");
            t=Started();t.Stops[1].Longitude=null;s=Calculate(t,15);Check(s.UnavailableReason==TrackingUnavailableReason.MissingCoordinates&&s.CanPoll,"Missing coordinates are refreshable domain result");Check(s.SafeStatus=="In progress","Unavailable geometry preserves operational status");
            t.Stops[1].Longitude=1;s=Calculate(t,15);Check(s.Availability==TrackingAvailability.Ready,"Coordinate correction becomes ready on next calculation");
            t.Stops[1].Latitude=100;s=Calculate(t,15);Check(s.UnavailableReason==TrackingUnavailableReason.InvalidCoordinates&&s.CanPoll,"Invalid coordinate domain result");
            t=Started();foreach(var stop in t.Stops)stop.Longitude=0;s=Calculate(t,15);Check(s.UnavailableReason==TrackingUnavailableReason.UnusableGeometry,"Zero-length geometry unavailable");
            t=Started();t.Stops[1].Order=4;s=Calculate(t,15);Check(s.UnavailableReason==TrackingUnavailableReason.UnusableGeometry,"Gapped itinerary unavailable");
            t=Started();t.ExpectedFinishLocal=t.ServiceDate.Add(t.DepartureTime);s=Calculate(t,15);Check(s.UnavailableReason==TrackingUnavailableReason.InvalidTimeline,"Invalid duration unavailable");
            t=Started();t.ActualStartUtc=Start.AddHours(1);s=Calculate(t,15);Check(s.UnavailableReason==TrackingUnavailableReason.InvalidTimeline,"Future start rejected");
            t=Started();t.Status=TripStatus.Delayed;s=Calculate(t,15);Check(s.UnavailableReason==TrackingUnavailableReason.InvalidTimeline,"Missing delay chronology not fabricated");
            t=Started();t.Pauses.Add(Pause(10,5,true));s=Calculate(t,15);Check(s.UnavailableReason==TrackingUnavailableReason.InvalidTimeline,"Inverted pause rejected");
            t=Started();t.TicketStatus=TicketStatus.Refunded;s=Calculate(t,15);Check(!s.CanPoll&&s.Stops.Count==0&&s.CurrentLatitude==null,"Refund stops map access");
            t=Started();t.ServiceDate=new DateTime(2026,10,5);t.DepartureTime=new TimeSpan(23,40,0);t.ExpectedFinishLocal=new DateTime(2026,10,6,0,40,0);s=Calculate(t,30);Near(s.ProgressPercent,50,"Overnight snapshot duration");
            t=Started();var repository=new MemoryRepository{Timeline=t};var clock=new FixedClock{UtcNow=Start.AddMinutes(15)};var service=new TrackingService(repository,clock);
            var passenger=service.GetPassengerTrackingSnapshot(42,7);Check(repository.Audience==TrackingAudience.Passenger&&repository.User==7&&repository.Resource==42,"Passenger repository receives authenticated ownership context");
            Check(passenger.ActualStartLocal=="05 Oct 2026 14:00","UTC display converted to South Africa");
            string json=new JavaScriptSerializer().Serialize(passenger);
            Check(!json.Contains("DriverName")&&!json.Contains("EmployeeNumber")&&!json.Contains("RowVersion")&&!json.Contains("TripId")&&!json.Contains("Pauses"),"Passenger projection excludes identity and operational internals");
            foreach(string culture in new[]{"en-ZA","de-DE","fr-FR"})
            {
                CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);json=new JavaScriptSerializer().Serialize(passenger);
                var fields=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);
                Check(fields["CurrentLongitude"] is decimal || fields["CurrentLongitude"] is double,"Numeric JSON coordinates under "+culture);
                Check(!(fields["ProgressPercent"] is string),"Numeric progress under "+culture);
            }
            var admin=service.GetAdminTrackingSnapshot(42,7);Check(admin.DriverName=="PRIVATE TEST"&&admin.EmployeeNumber=="PRIVATE-ID","Admin-only identity projection");
            repository.Timeline=null;Check(service.GetDriverTrackingSnapshot(42,8)==null,"Unauthorized repository result not projected");Check(service.GetPassengerTrackingSnapshot(0,7)==null,"Invalid resource rejected");
            Check(CoordinatePolicy.Validate(null,null).Count==0,"Empty coordinate pair accepted");
            Check(CoordinatePolicy.Validate(-33,28).Count==0,"Valid coordinate pair accepted");
            Check(CoordinatePolicy.Validate(-120,28).Count>0,"Latitude bounds");Check(CoordinatePolicy.Validate(-33,250).Count>0,"Longitude bounds");
            Check(CoordinatePolicy.Validate(null,28).Count>0&&CoordinatePolicy.Validate(-33,null).Count>0,"Paired coordinates");
            Check(CoordinatePolicy.Validate(-33.1234567m,28).Count>0,"Precision cannot silently round");
            Check(CoordinatePolicy.Validate(-90,180).Count==0,"Inclusive geographic bounds");
            Console.WriteLine("Slice 7 Business verification: {0} assertions passed. No data persisted.",checks);return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static TrackingTimeline Trip(){return new TrackingTimeline{TripId=42,TripCode="TEST-TRIP",RouteCode="TEST-ROUTE",RouteName="In-memory route",DriverName="PRIVATE TEST",EmployeeNumber="PRIVATE-ID",ServiceDate=Start.Date,DepartureTime=new TimeSpan(14,0,0),ExpectedFinishLocal=Start.Date.AddHours(15),Status=TripStatus.Scheduled,HasCurrentAssignment=true,Stops=new List<TrackingStop>{new TrackingStop{Order=1,Name="Origin",Latitude=0,Longitude=0},new TrackingStop{Order=2,Name="Middle",Latitude=0,Longitude=1},new TrackingStop{Order=3,Name="Destination",Latitude=0,Longitude=2}}};}
    private static TrackingTimeline Started(){var t=Trip();t.Status=TripStatus.InProgress;t.ActualStartUtc=Start;return t;}
    private static TrackingPause Pause(int from,int? to,bool cannot){return new TrackingPause{IsInTrip=true,IsCannotProceed=cannot,StartedUtc=Start.AddMinutes(from),EndedUtc=to.HasValue?Start.AddMinutes(to.Value):(DateTime?)null};}
    private static TrackingSnapshot Calculate(TrackingTimeline t,int minute){var result=new TrackingSnapshot();TrackingCalculator.Calculate(t,result,Start.AddMinutes(minute));return result;}
    private static void Near(double? actual,double expected,string name){Check(actual.HasValue&&Math.Abs(actual.Value-expected)<.00001,name);}
    private static void Check(bool pass,string name){if(!pass)throw new InvalidOperationException(name);checks++;}
    private sealed class FixedClock:IClock{public DateTime UtcNow{get;set;}public DateTime OperationalNow{get{return ToOperationalTime(UtcNow);}}public DateTime Today{get{return OperationalNow.Date;}}public DateTime ToOperationalTime(DateTime utc){return utc.AddHours(2);}}
    private sealed class MemoryRepository:ITrackingRepository{public TrackingTimeline Timeline;public TrackingAudience Audience;public long User,Resource;public TrackingTimeline GetTrip(TrackingAudience audience,long user,long resource){Audience=audience;User=user;Resource=resource;return Timeline;}public IList<TrackingTimeline> GetAdminTrips(TrackingQuery query,long user){return new List<TrackingTimeline>();}}
}
