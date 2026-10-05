using System;
using System.Collections.Generic;
using System.Linq;
using ForteMove.Models.Passengers;
using ForteMove.Models.Scheduling;
using ForteMove.Models.Tracking;

namespace ForteMove.Business.Tracking
{
    public static class TrackingCalculator
    {
        public static void Calculate(TrackingTimeline timeline, TrackingSnapshot result, DateTime evaluationUtc)
        {
            result.CanPoll = timeline.Status != TripStatus.Completed && timeline.Status != TripStatus.Cancelled
                && timeline.TicketStatus != TicketStatus.Refunded;
            result.TripStatus = timeline.Status.ToString();
            result.Stops = timeline.Stops.OrderBy(s => s.Order).Select(s => new TrackingStop {
                Order=s.Order, Code=s.Code, Name=s.Name, Latitude=s.Latitude, Longitude=s.Longitude }).ToList();
            result.Origin = result.Stops.FirstOrDefault()?.Name;
            result.Destination = result.Stops.LastOrDefault()?.Name;
            result.SafeStatus = timeline.Status == TripStatus.Completed ? "Completed"
                : timeline.Status == TripStatus.Cancelled ? "Cancelled"
                : timeline.ActualStartUtc.HasValue ? "In progress"
                : timeline.Status == TripStatus.Ready ? "Preparing to depart. Tracking starts when the Trip begins."
                : timeline.Status == TripStatus.Delayed ? "Delayed. Tracking starts when the Trip begins."
                : "Tracking starts when the Trip begins.";
            if (timeline.Status != TripStatus.Completed && timeline.Status != TripStatus.Cancelled
                && (timeline.HasCriticalDefect || timeline.Pauses.Any(p => !p.EndedUtc.HasValue && (p.IsCannotProceed || p.IsInTrip))))
                result.SafeStatus = "Service experiencing an operational issue.";
            result.IsMovementFrozen = !timeline.ActualStartUtc.HasValue || timeline.Status==TripStatus.Completed || timeline.Status==TripStatus.Cancelled
                || timeline.Pauses.Any(p=>p.IsInTrip&&!p.EndedUtc.HasValue);
            if (timeline.TicketStatus == TicketStatus.Refunded) { Unavailable(result,TrackingUnavailableReason.TicketRefunded,"This Ticket has been refunded. View Ticket details for its refund status."); result.Stops.Clear(); return; }
            if (timeline.Status == TripStatus.Cancelled) { Unavailable(result,TrackingUnavailableReason.Cancelled,"This service has been cancelled."); return; }
            var stops=timeline.Stops.OrderBy(s=>s.Order).ToList();
            result.MissingCoordinateCount=stops.Count(s=>!s.Latitude.HasValue||!s.Longitude.HasValue);
            if (result.MissingCoordinateCount>0) { Unavailable(result,TrackingUnavailableReason.MissingCoordinates,"Tracking unavailable: "+result.MissingCoordinateCount+" route stops do not have coordinates."); return; }
            if(stops.Any(s=>s.Latitude < -90 || s.Latitude > 90 || s.Longitude < -180 || s.Longitude > 180)) { Unavailable(result,TrackingUnavailableReason.InvalidCoordinates,"Tracking unavailable: route coordinates need correction."); return; }
            if(stops.Count<2 || stops.Where((s,i)=>s.Order!=i+1).Any()) { Unavailable(result,TrackingUnavailableReason.UnusableGeometry,"Tracking unavailable: the ordered route itinerary is incomplete."); return; }
            double duration=(timeline.ExpectedFinishLocal-timeline.ServiceDate.Date.Add(timeline.DepartureTime)).TotalSeconds;
            if(duration<=0 || timeline.ActualStartUtc>evaluationUtc || timeline.ActualCompletionUtc<timeline.ActualStartUtc
                || (timeline.Status==TripStatus.InProgress && !timeline.ActualStartUtc.HasValue)
                || (timeline.Status==TripStatus.Completed && (!timeline.ActualStartUtc.HasValue || !timeline.ActualCompletionUtc.HasValue))
                || timeline.Pauses.Any(p=>p.EndedUtc.HasValue && p.EndedUtc.Value<p.StartedUtc)) { Unavailable(result,TrackingUnavailableReason.InvalidTimeline,"Tracking unavailable: the service timeline needs administrator review."); return; }
            double[] weights=new double[stops.Count];
            bool timed=stops.All(s=>s.EstimatedMinutesFromOrigin.HasValue && s.EstimatedMinutesFromOrigin>=0)
                && stops[0].EstimatedMinutesFromOrigin==0 && stops.Last().EstimatedMinutesFromOrigin>0
                && !stops.Where((s,i)=>i>0 && s.EstimatedMinutesFromOrigin<stops[i-1].EstimatedMinutesFromOrigin).Any();
            for(int i=1;i<stops.Count;i++) weights[i]=timed?stops[i].EstimatedMinutesFromOrigin.Value:weights[i-1]+Distance(stops[i-1],stops[i]);
            if(weights.Last()<=0) { Unavailable(result,TrackingUnavailableReason.UnusableGeometry,"Tracking unavailable: route geometry does not provide usable segments."); return; }
            for(int i=0;i<weights.Length;i++) weights[i]/=weights.Last();
            bool completed=timeline.Status==TripStatus.Completed;
            double fraction=0;
            if(completed) { fraction=1; result.SafeStatus="Completed"; result.IsMovementFrozen=true; }
            else if(timeline.ActualStartUtc.HasValue)
            {
                if(timeline.Status!=TripStatus.InProgress && timeline.Status!=TripStatus.Delayed) { Unavailable(result,TrackingUnavailableReason.InvalidTimeline,"Tracking unavailable: the service timeline needs administrator review."); return; }
                DateTime start=timeline.ActualStartUtc.Value;
                var intervals=timeline.Pauses.Where(p=>p.IsInTrip && p.StartedUtc<evaluationUtc)
                    .Select(p=>new Interval { Start=p.StartedUtc<start?start:p.StartedUtc, End=!p.EndedUtc.HasValue||p.EndedUtc>evaluationUtc?evaluationUtc:p.EndedUtc.Value })
                    .Where(p=>p.End>p.Start).OrderBy(p=>p.Start).ToList();
                double paused=0; Interval merged=null;
                foreach(var interval in intervals) { if(merged==null) merged=interval; else if(interval.Start<=merged.End) { if(interval.End>merged.End) merged.End=interval.End; } else { paused+=(merged.End-merged.Start).TotalSeconds; merged=interval; } }
                if(merged!=null) paused+=(merged.End-merged.Start).TotalSeconds;
                double moving=Math.Max(0,(evaluationUtc-start).TotalSeconds-paused);
                bool openDelay=timeline.Pauses.Any(p=>p.IsInTrip&&!p.IsCannotProceed&&!p.EndedUtc.HasValue);
                if(timeline.Status==TripStatus.Delayed&&!openDelay) { Unavailable(result,TrackingUnavailableReason.InvalidTimeline,"Tracking unavailable: delay timing needs administrator review."); return; }
                fraction=Math.Min(.99,moving/duration); result.IsOverrunning=moving>=duration;
                result.IsMovementFrozen=timeline.Pauses.Any(p=>p.IsInTrip&&!p.EndedUtc.HasValue);
                result.SafeStatus=result.IsMovementFrozen?"Service experiencing an operational issue.":"In progress";
                if(result.IsOverrunning&&!result.IsMovementFrozen) result.SafeStatus="In progress - awaiting recorded completion.";
            }
            else if(!timeline.HasCurrentAssignment) { Unavailable(result,TrackingUnavailableReason.NoAssignment,"Assignment pending. Tracking starts when the Trip begins."); return; }
            else if(timeline.Status==TripStatus.Ready) result.SafeStatus="Preparing to depart. Tracking starts when the Trip begins.";
            else if(timeline.Status==TripStatus.Delayed) result.SafeStatus="Delayed. Tracking starts when the Trip begins.";
            if(!completed && (timeline.HasCriticalDefect || timeline.Pauses.Any(p=>p.IsCannotProceed&&!p.EndedUtc.HasValue))) result.SafeStatus="Service experiencing an operational issue.";
            result.Availability=TrackingAvailability.Ready; result.ProgressPercent=fraction*100;
            int index=0; while(index<stops.Count-1 && weights[index+1]<=fraction) index++;
            result.CurrentStop=stops[index].Name; result.NextStop=index+1<stops.Count?stops[index+1].Name:null;
            // Scheduled services have an overview, without claiming a Bus is physically at origin.
            bool showMarker=completed||timeline.ActualStartUtc.HasValue||timeline.Status==TripStatus.Ready||timeline.Status==TripStatus.Delayed;
            if(showMarker) { int next=Math.Min(index+1,stops.Count-1); double span=weights[next]-weights[index]; double segment=span>0?(fraction-weights[index])/span:0;
                result.CurrentLatitude=(double)stops[index].Latitude.Value+segment*(double)(stops[next].Latitude.Value-stops[index].Latitude.Value);
                result.CurrentLongitude=(double)stops[index].Longitude.Value+segment*(double)(stops[next].Longitude.Value-stops[index].Longitude.Value); }
        }

        private static void Unavailable(TrackingSnapshot result,TrackingUnavailableReason reason,string message) { result.Availability=TrackingAvailability.Unavailable; result.UnavailableReason=reason; result.AvailabilityMessage=message; }
        private static double Distance(TrackingStop a,TrackingStop b) { double lat1=(double)a.Latitude.Value*Math.PI/180,lat2=(double)b.Latitude.Value*Math.PI/180,dl=lat2-lat1,dn=(double)(b.Longitude.Value-a.Longitude.Value)*Math.PI/180;
            double h=Math.Pow(Math.Sin(dl/2),2)+Math.Cos(lat1)*Math.Cos(lat2)*Math.Pow(Math.Sin(dn/2),2);return 6371*2*Math.Atan2(Math.Sqrt(Math.Min(1,h)),Math.Sqrt(Math.Max(0,1-h))); }
        private sealed class Interval { public DateTime Start; public DateTime End; }
    }
}
