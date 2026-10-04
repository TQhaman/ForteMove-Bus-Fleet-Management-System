using System;
using System.Collections.Generic;
using ForteMove.Models.Fleet;

namespace ForteMove.Models.Scheduling
{
    public enum OperatingDay : byte
    {
        Sunday = 0,
        Monday = 1,
        Tuesday = 2,
        Wednesday = 3,
        Thursday = 4,
        Friday = 5,
        Saturday = 6
    }

    public enum TripStatus
    {
        Unassigned,
        Scheduled,
        Ready,
        InProgress,
        Delayed,
        Completed,
        Cancelled
    }

    public enum ScheduleStatus
    {
        Upcoming,
        Active,
        Ended,
        Inactive
    }

    public sealed class ScheduleRouteOption
    {
        public long RouteId { get; set; }
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public string OriginName { get; set; }
        public string DestinationName { get; set; }
        public int EstimatedDurationMinutes { get; set; }
        public byte[] RowVersion { get; set; }
    }

    public sealed class SchedulingCreationOptions
    {
        public SchedulingCreationOptions()
        {
            Routes = new List<ScheduleRouteOption>();
            BusCategories = new List<LookupOption>();
        }

        public string SuggestedScheduleCode { get; set; }
        public IList<ScheduleRouteOption> Routes { get; set; }
        public IList<LookupOption> BusCategories { get; set; }
    }

    public sealed class CreateScheduleRequest
    {
        public CreateScheduleRequest()
        {
            OperatingDays = new List<OperatingDay>();
            DepartureTimes = new List<TimeSpan>();
        }

        public long? RouteId { get; set; }
        public IList<OperatingDay> OperatingDays { get; set; }
        public IList<TimeSpan> DepartureTimes { get; set; }
        public DateTime? EffectiveStartDate { get; set; }
        public DateTime? EffectiveEndDate { get; set; }
        public int? PreferredBusCategoryId { get; set; }
        public int? ExpectedCapacity { get; set; }
        public string PreviewRequestFingerprint { get; set; }
        public string PreviewOccurrenceSignature { get; set; }
    }

    public sealed class ScheduleOccurrence
    {
        public DateTime ServiceDate { get; set; }
        public TimeSpan ScheduledDepartureTime { get; set; }
        public DateTime ExpectedFinishLocal { get; set; }
        public int EstimatedDurationMinutesSnapshot { get; set; }

        public DateTime ScheduledDepartureLocal
        {
            get { return ServiceDate.Date.Add(ScheduledDepartureTime); }
        }
    }

    public sealed class SchedulePreview
    {
        public SchedulePreview()
        {
            Occurrences = new List<ScheduleOccurrence>();
        }

        public DateTime EvaluatedAtLocal { get; set; }
        public int TripCount { get; set; }
        public DateTime? FirstDepartureLocal { get; set; }
        public DateTime? LastDepartureLocal { get; set; }
        public string RequestFingerprint { get; set; }
        public string OccurrenceSignature { get; set; }
        public IList<ScheduleOccurrence> Occurrences { get; set; }
    }

    public sealed class ScheduleCreationAggregate
    {
        public ScheduleCreationAggregate()
        {
            OperatingDays = new List<OperatingDay>();
            DepartureTimes = new List<TimeSpan>();
            Occurrences = new List<ScheduleOccurrence>();
        }

        public long RouteId { get; set; }
        public byte[] RouteRowVersion { get; set; }
        public DateTime EffectiveStartDate { get; set; }
        public DateTime EffectiveEndDate { get; set; }
        public int? PreferredBusCategoryId { get; set; }
        public int? ExpectedCapacity { get; set; }
        public IList<OperatingDay> OperatingDays { get; set; }
        public IList<TimeSpan> DepartureTimes { get; set; }
        public IList<ScheduleOccurrence> Occurrences { get; set; }
    }

    public sealed class ScheduleCreationResult
    {
        public long RouteScheduleId { get; set; }
        public string ScheduleCode { get; set; }
        public int GeneratedTripCount { get; set; }
    }

    public sealed class ScheduleQuery
    {
        public string SearchTerm { get; set; }
        public ScheduleStatus? Status { get; set; }
    }

    public sealed class ScheduleListItem
    {
        public long RouteScheduleId { get; set; }
        public string ScheduleCode { get; set; }
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public string OperatingDaysDisplay { get; set; }
        public string DepartureTimesDisplay { get; set; }
        public DateTime EffectiveStartDate { get; set; }
        public DateTime EffectiveEndDate { get; set; }
        public long GeneratedTripCount { get; set; }
        public ScheduleStatus Status { get; set; }
    }

    public sealed class ScheduleVersionDetails
    {
        public long RouteScheduleVersionId { get; set; }
        public int VersionNumber { get; set; }
        public DateTime EffectiveStartDate { get; set; }
        public DateTime EffectiveEndDate { get; set; }
        public DateTime? SupersededFromDate { get; set; }
        public string OperatingDaysDisplay { get; set; }
        public string DepartureTimesDisplay { get; set; }
        public string PreferredBusCategoryName { get; set; }
        public int? ExpectedCapacity { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public sealed class ScheduleDetails
    {
        public ScheduleDetails()
        {
            History = new List<ScheduleVersionDetails>();
        }

        public long RouteScheduleId { get; set; }
        public string ScheduleCode { get; set; }
        public long RouteId { get; set; }
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public string OriginName { get; set; }
        public string DestinationName { get; set; }
        public bool IsActive { get; set; }
        public ScheduleStatus Status { get; set; }
        public long GeneratedTripCount { get; set; }
        public ScheduleVersionDetails CurrentVersion { get; set; }
        public IList<ScheduleVersionDetails> History { get; set; }
    }

    public sealed class TripQuery
    {
        public DateTime? ServiceDate { get; set; }
        public long? RouteId { get; set; }
        public TripStatus? Status { get; set; }
    }

    public sealed class TripListItem
    {
        public long TripId { get; set; }
        public string TripCode { get; set; }
        public DateTime ServiceDate { get; set; }
        public TimeSpan ScheduledDepartureTime { get; set; }
        public DateTime ExpectedFinishLocal { get; set; }
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public string OriginName { get; set; }
        public string DestinationName { get; set; }
        public TripStatus Status { get; set; }
        public bool RequiresReview { get; set; }
        public string AssignedDriverName { get; set; }
        public string AssignedEmployeeNumber { get; set; }
        public string AssignedFleetNumber { get; set; }
    }

    public sealed class ExistingScheduleTrip
    {
        public long TripId { get; set; }
        public long RouteScheduleVersionId { get; set; }
        public DateTime ServiceDate { get; set; }
        public TimeSpan ScheduledDepartureTime { get; set; }
        public TripStatus Status { get; set; }
        public bool RequiresReview { get; set; }
        public DateTime? OperationallyTouchedUtc { get; set; }

        public bool HasAssignmentHistory { get; set; }

        public bool IsUntouched
        {
            get
            {
                return Status == TripStatus.Unassigned &&
                    !RequiresReview &&
                    !OperationallyTouchedUtc.HasValue &&
                    !HasAssignmentHistory;
            }
        }
    }

    public sealed class ScheduleChangeOptions
    {
        public ScheduleChangeOptions()
        {
            OperatingDays = new List<OperatingDay>();
            DepartureTimes = new List<TimeSpan>();
            ExistingTripsFromCutover = new List<ExistingScheduleTrip>();
            BusCategories = new List<LookupOption>();
        }

        public long RouteScheduleId { get; set; }
        public long RouteScheduleVersionId { get; set; }
        public string ScheduleCode { get; set; }
        public long RouteId { get; set; }
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public bool RouteIsActive { get; set; }
        public int EstimatedDurationMinutes { get; set; }
        public DateTime EffectiveStartDate { get; set; }
        public DateTime EffectiveEndDate { get; set; }
        public int VersionNumber { get; set; }
        public int? PreferredBusCategoryId { get; set; }
        public int? ExpectedCapacity { get; set; }
        public IList<OperatingDay> OperatingDays { get; set; }
        public IList<TimeSpan> DepartureTimes { get; set; }
        public IList<ExistingScheduleTrip> ExistingTripsFromCutover { get; set; }
        public IList<LookupOption> BusCategories { get; set; }
        public byte[] ScheduleRowVersion { get; set; }
        public byte[] VersionRowVersion { get; set; }
    }

    public sealed class ChangeScheduleRequest
    {
        public ChangeScheduleRequest()
        {
            OperatingDays = new List<OperatingDay>();
            DepartureTimes = new List<TimeSpan>();
        }

        public long RouteScheduleId { get; set; }
        public long RouteScheduleVersionId { get; set; }
        public DateTime? ChangeEffectiveDate { get; set; }
        public DateTime? EffectiveEndDate { get; set; }
        public int? PreferredBusCategoryId { get; set; }
        public int? ExpectedCapacity { get; set; }
        public IList<OperatingDay> OperatingDays { get; set; }
        public IList<TimeSpan> DepartureTimes { get; set; }
        public byte[] ScheduleRowVersion { get; set; }
        public byte[] VersionRowVersion { get; set; }
        public string PreviewRequestFingerprint { get; set; }
        public string PreviewOccurrenceSignature { get; set; }
    }

    public sealed class ScheduleChangeImpact
    {
        public DateTime EvaluatedAtLocal { get; set; }
        public int FutureTripsToReplace { get; set; }
        public int NewTripsToGenerate { get; set; }
        public int ProtectedTripsRequiringReview { get; set; }
        public int HistoricalTripsAffected { get; set; }
        public string RequestFingerprint { get; set; }
        public string OccurrenceSignature { get; set; }
    }

    public sealed class ScheduleChangeAggregate
    {
        public ScheduleChangeAggregate()
        {
            OperatingDays = new List<OperatingDay>();
            DepartureTimes = new List<TimeSpan>();
            Occurrences = new List<ScheduleOccurrence>();
        }

        public long RouteScheduleId { get; set; }
        public long CurrentVersionId { get; set; }
        public long RouteId { get; set; }
        public int EstimatedDurationMinutes { get; set; }
        public byte[] ScheduleRowVersion { get; set; }
        public byte[] VersionRowVersion { get; set; }
        public int NewVersionNumber { get; set; }
        public DateTime ChangeEffectiveDate { get; set; }
        public DateTime EffectiveEndDate { get; set; }
        public int? PreferredBusCategoryId { get; set; }
        public int? ExpectedCapacity { get; set; }
        public IList<OperatingDay> OperatingDays { get; set; }
        public IList<TimeSpan> DepartureTimes { get; set; }
        public IList<ScheduleOccurrence> Occurrences { get; set; }
        public int ExpectedFutureTripsToReplace { get; set; }
        public int ExpectedProtectedTrips { get; set; }
    }

    public sealed class ScheduleChangeResult
    {
        public long RouteScheduleId { get; set; }
        public int VersionNumber { get; set; }
        public int ReplacedTripCount { get; set; }
        public int GeneratedTripCount { get; set; }
        public int ProtectedTripCount { get; set; }
    }
}
