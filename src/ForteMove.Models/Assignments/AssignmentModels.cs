using System;
using System.Collections.Generic;
using ForteMove.Models.Drivers;
using ForteMove.Models.Scheduling;

namespace ForteMove.Models.Assignments
{
    public enum AssignmentDecisionType
    {
        RecommendationAccepted,
        AlternativeSelected,
        Change
    }

    public enum AssignmentEndType
    {
        Changed,
        Removed
    }

    public sealed class AssignmentQueueQuery
    {
        public DateTime ServiceDate { get; set; }
    }

    public sealed class AssignmentResourceWindow
    {
        public long TripId { get; set; }
        public DateTime StartLocal { get; set; }
        public DateTime FinishLocal { get; set; }
    }

    public sealed class AssignmentTripCandidate
    {
        public long TripId { get; set; }
        public string TripCode { get; set; }
        public long RouteId { get; set; }
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public DateTime ServiceDate { get; set; }
        public TimeSpan ScheduledDepartureTime { get; set; }
        public DateTime ExpectedFinishLocal { get; set; }
        public int? PreferredBusCategoryId { get; set; }
        public int? ExpectedCapacity { get; set; }
        public bool RequiresReview { get; set; }
        public TripStatus Status { get; set; }
        public byte[] RowVersion { get; set; }

        public DateTime ScheduledDepartureLocal
        {
            get { return ServiceDate.Date.Add(ScheduledDepartureTime); }
        }
    }

    public sealed class AssignmentBusCandidate
    {
        public AssignmentBusCandidate() { Windows = new List<AssignmentResourceWindow>(); }
        public long BusId { get; set; }
        public string FleetNumber { get; set; }
        public int BusCategoryId { get; set; }
        public int PassengerCapacity { get; set; }
        public int? GrossVehicleMassKg { get; set; }
        public string OperationalState { get; set; }
        public DateTime LicenceExpiryDate { get; set; }
        public DateTime RoadworthyExpiryDate { get; set; }
        public DateTime InsuranceExpiryDate { get; set; }
        public byte[] RowVersion { get; set; }
        public IList<AssignmentResourceWindow> Windows { get; set; }
    }

    public sealed class AssignmentDriverCandidate
    {
        public AssignmentDriverCandidate() { Windows = new List<AssignmentResourceWindow>(); }
        public long DriverProfileId { get; set; }
        public string EmployeeNumber { get; set; }
        public string DriverName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public DriverAvailabilityStatus AvailabilityStatus { get; set; }
        public DriverLicenceCode LicenceCode { get; set; }
        public DateTime LicenceExpiryDate { get; set; }
        public DateTime PrdpExpiryDate { get; set; }
        public bool AccountIsActive { get; set; }
        public bool RoleIsActive { get; set; }
        public string EmploymentStatus { get; set; }
        public int RouteFamiliarityCount { get; set; }
        public byte[] UserRowVersion { get; set; }
        public byte[] StaffRowVersion { get; set; }
        public byte[] DriverRowVersion { get; set; }
        public IList<AssignmentResourceWindow> Windows { get; set; }
    }

    public sealed class AssignmentRecommendation
    {
        public AssignmentTripCandidate Trip { get; set; }
        public AssignmentBusCandidate Bus { get; set; }
        public AssignmentDriverCandidate Driver { get; set; }
        public string Explanation { get; set; }
        public string ExclusionReason { get; set; }
        public string Fingerprint { get; set; }
        public bool HasRecommendation { get { return Bus != null && Driver != null; } }
    }

    public sealed class AssignmentQueueResult
    {
        public AssignmentQueueResult()
        {
            Recommendations = new List<AssignmentRecommendation>();
            ReviewTrips = new List<AssignmentTripCandidate>();
        }
        public DateTime EvaluatedAtLocal { get; set; }
        public IList<AssignmentRecommendation> Recommendations { get; set; }
        public IList<AssignmentTripCandidate> ReviewTrips { get; set; }
    }

    public sealed class AssignmentData
    {
        public AssignmentData()
        {
            Trips = new List<AssignmentTripCandidate>();
            Buses = new List<AssignmentBusCandidate>();
            Drivers = new List<AssignmentDriverCandidate>();
        }
        public IList<AssignmentTripCandidate> Trips { get; set; }
        public IList<AssignmentBusCandidate> Buses { get; set; }
        public IList<AssignmentDriverCandidate> Drivers { get; set; }
    }

    public sealed class AssignmentSaveResult
    {
        public int AssignmentCount { get; set; }
    }

    public sealed class ConfirmAssignmentItem
    {
        public long TripId { get; set; }
        public long BusId { get; set; }
        public long DriverProfileId { get; set; }
        public byte[] TripRowVersion { get; set; }
        public byte[] BusRowVersion { get; set; }
        public byte[] DriverRowVersion { get; set; }
        public byte[] UserRowVersion { get; set; }
        public byte[] StaffRowVersion { get; set; }
        public AssignmentDecisionType DecisionType { get; set; }
        public string Reason { get; set; }
        public string Fingerprint { get; set; }
    }

    public sealed class ConfirmAssignmentsRequest
    {
        public ConfirmAssignmentsRequest() { Items = new List<ConfirmAssignmentItem>(); }
        public IList<ConfirmAssignmentItem> Items { get; set; }
    }

    public sealed class AssignmentMutationRequest
    {
        public long TripId { get; set; }
        public long? BusId { get; set; }
        public long? DriverProfileId { get; set; }
        public string Reason { get; set; }
        public byte[] TripRowVersion { get; set; }
        public byte[] AssignmentRowVersion { get; set; }
    }

    public sealed class AssignmentHistoryItem
    {
        public long TripAssignmentId { get; set; }
        public long DriverProfileId { get; set; }
        public long BusId { get; set; }
        public string DriverName { get; set; }
        public string EmployeeNumber { get; set; }
        public string FleetNumber { get; set; }
        public AssignmentDecisionType DecisionType { get; set; }
        public string DecisionReason { get; set; }
        public DateTime AssignedUtc { get; set; }
        public bool IsCurrent { get; set; }
        public AssignmentEndType? EndType { get; set; }
        public string EndReason { get; set; }
        public DateTime? EndedUtc { get; set; }
        public byte[] RowVersion { get; set; }
    }

    public sealed class AssignmentDetails
    {
        public AssignmentDetails() { History = new List<AssignmentHistoryItem>(); }
        public AssignmentTripCandidate Trip { get; set; }
        public AssignmentHistoryItem CurrentAssignment { get; set; }
        public IList<AssignmentHistoryItem> History { get; set; }
    }
}
