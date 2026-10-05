using System;
using System.Collections.Generic;
using ForteMove.Models.Scheduling;
using ForteMove.Models.Passengers;

namespace ForteMove.Models.Tracking
{
    public enum TrackingAudience { Administrator, Driver, Passenger }
    public enum TrackingAvailability { Ready, Unavailable }
    public enum TrackingUnavailableReason { None, MissingCoordinates, InvalidCoordinates, UnusableGeometry, InvalidTimeline, NoAssignment, TicketRefunded, Cancelled }

    // Repository evidence is never serialized to the browser.
    public sealed class TrackingTimeline
    {
        public long TripId { get; set; }
        public long RouteId { get; set; }
        public string TripCode { get; set; }
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public DateTime ServiceDate { get; set; }
        public TimeSpan DepartureTime { get; set; }
        public DateTime ExpectedFinishLocal { get; set; }
        public TripStatus Status { get; set; }
        public DateTime? ActualStartUtc { get; set; }
        public DateTime? ActualCompletionUtc { get; set; }
        public bool HasCurrentAssignment { get; set; }
        public string FleetNumber { get; set; }
        public string DriverName { get; set; }
        public string EmployeeNumber { get; set; }
        public bool RequiresReview { get; set; }
        public bool HasCriticalDefect { get; set; }
        public TicketStatus? TicketStatus { get; set; }
        public IList<TrackingStop> Stops { get; set; } = new List<TrackingStop>();
        public IList<TrackingPause> Pauses { get; set; } = new List<TrackingPause>();
    }

    public sealed class TrackingStop
    {
        public int Order { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public int? EstimatedMinutesFromOrigin { get; set; }
    }

    public sealed class TrackingPause
    {
        public bool IsInTrip { get; set; }
        public bool IsCannotProceed { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime? EndedUtc { get; set; }
        public int? EstimatedDelayMinutes { get; set; }
    }

    public sealed class TrackingQuery
    {
        public DateTime? ServiceDate { get; set; }
        public bool IncludeCompleted { get; set; }
    }

    public class TrackingSnapshot
    {
        public string TripCode { get; set; }
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public string Origin { get; set; }
        public string Destination { get; set; }
        public string TripStatus { get; set; }
        public string FleetNumber { get; set; }
        public string ScheduledDepartureLocal { get; set; }
        public string ExpectedFinishLocal { get; set; }
        public string ActualStartLocal { get; set; }
        public string ActualCompletionLocal { get; set; }
        public string LastCalculatedLocalTime { get; set; }
        public TrackingAvailability Availability { get; set; }
        public TrackingUnavailableReason UnavailableReason { get; set; }
        public string AvailabilityMessage { get; set; }
        public int MissingCoordinateCount { get; set; }
        public double? CurrentLatitude { get; set; }
        public double? CurrentLongitude { get; set; }
        public double? ProgressPercent { get; set; }
        public string CurrentStop { get; set; }
        public string NextStop { get; set; }
        public bool IsMovementFrozen { get; set; }
        public bool IsOverrunning { get; set; }
        public bool CanPoll { get; set; }
        public string SafeStatus { get; set; }
        public IList<TrackingStop> Stops { get; set; } = new List<TrackingStop>();
    }

    public sealed class AdminTrackingSnapshot : TrackingSnapshot
    {
        public string DriverName { get; set; }
        public string EmployeeNumber { get; set; }
        public bool RequiresAttention { get; set; }
        public string AttentionMessage { get; set; }
    }

    public sealed class DriverTrackingSnapshot : TrackingSnapshot { }
    public sealed class PassengerTrackingSnapshot : TrackingSnapshot { }

    public sealed class AdminTrackingListItem
    {
        public long TripId { get; set; }
        public AdminTrackingSnapshot Snapshot { get; set; }
    }
}
