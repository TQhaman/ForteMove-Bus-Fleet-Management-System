using System;
using System.Collections.Generic;
using ForteMove.Models.Scheduling;

namespace ForteMove.Models.Operations
{
    public enum DriverTripBucket { Today, Upcoming, History }
    public enum TripDelayPhase { PreStart, InTrip }
    public enum TripDelayEndType { Started, Resumed, Completed, AssignmentChanged, AssignmentRemoved, Cancelled }
    public enum CannotProceedResolutionType { Proceed, AssignmentChanged, AssignmentRemoved, Cancelled }
    public enum DefectCategory { EngineDrivetrain, Brakes, Tyres, Electrical, Doors, Lights, BodyInterior, Other }
    public enum DefectSeverity { Minor, Major, Critical }
    public enum DefectStatus { Open, Reviewed, Resolved }

    public sealed class DriverTripListItem
    {
        public long TripId { get; set; }
        public string TripCode { get; set; }
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public string OriginName { get; set; }
        public string DestinationName { get; set; }
        public DateTime ServiceDate { get; set; }
        public TimeSpan ScheduledDepartureTime { get; set; }
        public DateTime ExpectedFinishLocal { get; set; }
        public string FleetNumber { get; set; }
        public TripStatus Status { get; set; }
        public DateTime? ActualStartUtc { get; set; }
        public DateTime? ActualCompletionUtc { get; set; }
        public bool HasOpenCannotProceed { get; set; }
        public bool HasUnresolvedCriticalDefect { get; set; }
        public bool RequiresReview { get; set; }
        public bool IsOverdueNotStarted { get; set; }
    }

    public sealed class PreTripInspectionDetails
    {
        public long PreTripInspectionId { get; set; }
        public decimal StartOdometerKilometres { get; set; }
        public DateTime ConfirmedUtc { get; set; }
        public DateTime? InvalidatedUtc { get; set; }
        public string InvalidationReason { get; set; }
        public byte[] RowVersion { get; set; }
        public bool IsCurrent { get { return !InvalidatedUtc.HasValue; } }
    }

    public sealed class TripExecutionDetails
    {
        public long TripExecutionId { get; set; }
        public DateTime ActualStartUtc { get; set; }
        public DateTime? ActualCompletionUtc { get; set; }
        public decimal? EndOdometerKilometres { get; set; }
        public string CompletionNote { get; set; }
        public byte[] RowVersion { get; set; }
    }

    public sealed class TripDelayDetails
    {
        public long TripDelayEventId { get; set; }
        public TripDelayPhase Phase { get; set; }
        public string Reason { get; set; }
        public int? EstimatedDelayMinutes { get; set; }
        public DateTime ReportedUtc { get; set; }
        public DateTime? EndedUtc { get; set; }
        public TripDelayEndType? EndType { get; set; }
        public byte[] RowVersion { get; set; }
        public bool IsOpen { get { return !EndedUtc.HasValue; } }
    }

    public sealed class CannotProceedDetails
    {
        public long TripCannotProceedReportId { get; set; }
        public long TripId { get; set; }
        public string TripCode { get; set; }
        public string RouteName { get; set; }
        public string DriverName { get; set; }
        public string EmployeeNumber { get; set; }
        public string FleetNumber { get; set; }
        public TripStatus TripStatus { get; set; }
        public TripDelayPhase Phase { get; set; }
        public string Reason { get; set; }
        public string Note { get; set; }
        public DateTime ReportedUtc { get; set; }
        public CannotProceedResolutionType? ResolutionType { get; set; }
        public string ResolutionNote { get; set; }
        public DateTime? ResolvedUtc { get; set; }
        public byte[] RowVersion { get; set; }
        public byte[] TripRowVersion { get; set; }
        public bool IsOpen { get { return !ResolvedUtc.HasValue; } }
    }

    public sealed class DefectReportDetails
    {
        public long BusDefectReportId { get; set; }
        public string DefectCode { get; set; }
        public long BusId { get; set; }
        public long? TripId { get; set; }
        public string TripCode { get; set; }
        public string FleetNumber { get; set; }
        public string DriverName { get; set; }
        public string EmployeeNumber { get; set; }
        public DefectCategory Category { get; set; }
        public DefectSeverity Severity { get; set; }
        public string Description { get; set; }
        public DefectStatus Status { get; set; }
        public DateTime ReportedUtc { get; set; }
        public string ReviewNote { get; set; }
        public DateTime? ReviewedUtc { get; set; }
        public string ResolutionNote { get; set; }
        public DateTime? ResolvedUtc { get; set; }
        public byte[] RowVersion { get; set; }
        public bool IsBlocking { get { return Severity == DefectSeverity.Critical && Status != DefectStatus.Resolved; } }
    }

    public sealed class TripStatusHistoryItem
    {
        public TripStatus? FromStatus { get; set; }
        public TripStatus ToStatus { get; set; }
        public string EventType { get; set; }
        public DateTime OccurredUtc { get; set; }
        public string Note { get; set; }
    }

    public sealed class DriverTripDetails
    {
        public DriverTripDetails()
        {
            StopNames = new List<string>();
            DelayHistory = new List<TripDelayDetails>();
            DefectHistory = new List<DefectReportDetails>();
            StatusHistory = new List<TripStatusHistoryItem>();
        }

        public long TripId { get; set; }
        public string TripCode { get; set; }
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public string OriginName { get; set; }
        public string DestinationName { get; set; }
        public IList<string> StopNames { get; set; }
        public DateTime ServiceDate { get; set; }
        public TimeSpan ScheduledDepartureTime { get; set; }
        public DateTime ExpectedFinishLocal { get; set; }
        public TripStatus Status { get; set; }
        public bool RequiresReview { get; set; }
        public long TripAssignmentId { get; set; }
        public long DriverProfileId { get; set; }
        public long BusId { get; set; }
        public string FleetNumber { get; set; }
        public decimal BusOdometerKilometres { get; set; }
        public PreTripInspectionDetails CurrentInspection { get; set; }
        public TripExecutionDetails Execution { get; set; }
        public TripDelayDetails OpenDelay { get; set; }
        public CannotProceedDetails OpenCannotProceed { get; set; }
        public bool HasUnresolvedCriticalDefect { get; set; }
        public byte[] TripRowVersion { get; set; }
        public byte[] AssignmentRowVersion { get; set; }
        public byte[] BusRowVersion { get; set; }
        public IList<TripDelayDetails> DelayHistory { get; set; }
        public IList<DefectReportDetails> DefectHistory { get; set; }
        public IList<TripStatusHistoryItem> StatusHistory { get; set; }
        public DateTime ScheduledDepartureLocal { get { return ServiceDate.Date.Add(ScheduledDepartureTime); } }
    }

    public sealed class ConfirmReadinessRequest
    {
        public long TripId { get; set; }
        public bool ExteriorConditionChecked { get; set; }
        public bool TyresSafeChecked { get; set; }
        public bool LightsIndicatorsChecked { get; set; }
        public bool NoCriticalDashboardWarningsChecked { get; set; }
        public bool DoorsOperationalChecked { get; set; }
        public bool EmergencyEquipmentPresentChecked { get; set; }
        public bool NoBlockingNewDefectChecked { get; set; }
        public decimal? StartOdometerKilometres { get; set; }
        public byte[] TripRowVersion { get; set; }
        public byte[] AssignmentRowVersion { get; set; }
    }

    public class TripOperationRequest
    {
        public long TripId { get; set; }
        public byte[] TripRowVersion { get; set; }
        public byte[] AssignmentRowVersion { get; set; }
        public byte[] RelatedRowVersion { get; set; }
    }

    public sealed class ReportDelayRequest : TripOperationRequest
    {
        public string Reason { get; set; }
        public int? EstimatedDelayMinutes { get; set; }
    }

    public sealed class ReportCannotProceedRequest : TripOperationRequest
    {
        public string Reason { get; set; }
        public string Note { get; set; }
    }

    public sealed class ReportDefectRequest : TripOperationRequest
    {
        public DefectCategory? Category { get; set; }
        public DefectSeverity? Severity { get; set; }
        public string Description { get; set; }
    }

    public sealed class CompleteTripRequest : TripOperationRequest
    {
        public decimal? EndOdometerKilometres { get; set; }
        public string CompletionNote { get; set; }
        public byte[] BusRowVersion { get; set; }
    }

    public sealed class ResolveCannotProceedRequest
    {
        public long TripCannotProceedReportId { get; set; }
        public string ResolutionNote { get; set; }
        public byte[] ReportRowVersion { get; set; }
        public byte[] TripRowVersion { get; set; }
    }

    public sealed class CancelTripRequest
    {
        public long TripId { get; set; }
        public string Reason { get; set; }
        public byte[] TripRowVersion { get; set; }
        public byte[] AssignmentRowVersion { get; set; }
    }

    public sealed class UpdateDefectRequest
    {
        public long BusDefectReportId { get; set; }
        public string Note { get; set; }
        public byte[] RowVersion { get; set; }
    }

    public sealed class ExceptionQuery
    {
        public bool? OpenOnly { get; set; }
    }

    public sealed class DefectQuery
    {
        public DefectStatus? Status { get; set; }
        public DefectSeverity? Severity { get; set; }
    }

    public sealed class DefectReportResult
    {
        public long BusDefectReportId { get; set; }
        public string DefectCode { get; set; }
    }
}
