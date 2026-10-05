using System;
using System.Collections.Generic;
using ForteMove.Models.Fleet;
namespace ForteMove.Models.Maintenance
{
    public class MaintenancePlan
    {
        public long MaintenancePlanId { get; set; }
        public string PlanCode { get; set; }
        public long BusId { get; set; }
        public string FleetNumber { get; set; }
        public string ServiceName { get; set; }
        public int? IntervalDays { get; set; }
        public decimal? IntervalKilometres { get; set; }
        public DateTime? LastServiceDate { get; set; }
        public decimal? LastServiceOdometer { get; set; }
        public DateTime? NextDueDate { get; set; }
        public decimal? NextDueOdometer { get; set; }
        public long? LastCompletedWorkOrderId { get; set; }
        public bool BlocksOperationWhenOverdue { get; set; }
        public bool IsActive { get; set; }
        public decimal CurrentOdometer { get; set; }
        public byte[] RowVersion { get; set; }
        public MaintenanceDueState DueState { get; set; }
        public string DueReason { get; set; }
        public bool BlocksOperation { get; set; }
    }
    public class RepairProvider
    {
        public long RepairProviderId { get; set; }
        public string ProviderCode { get; set; }
        public string ProviderName { get; set; }
        public string AreaDescription { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public byte[] RowVersion { get; set; }
    }
    public sealed class MaintenanceWorkOrder : CreateWorkOrderRequest
    {
        public long MaintenanceWorkOrderId { get; set; }
        public string WorkOrderCode { get; set; }
        public string FleetNumber { get; set; }
        public string ProviderCodeSnapshot { get; set; }
        public string ProviderNameSnapshot { get; set; }
        public MaintenanceOrderStatus Status { get; set; }
        public DateTime OpenedUtc { get; set; }
        public DateTime? StartedUtc { get; set; }
        public DateTime? CompletedUtc { get; set; }
        public DateTime? CancelledUtc { get; set; }
        public string CancellationReason { get; set; }
        public decimal? ServiceOdometer { get; set; }
        public string WorkPerformed { get; set; }
        public string CompletionNote { get; set; }
        public string ExternalReference { get; set; }
        public decimal? CompletedCost { get; set; }
        public byte[] RowVersion { get; set; }
        public byte[] BusRowVersion { get; set; }
        public byte[] PlanRowVersion { get; set; }
        public decimal CurrentOdometer { get; set; }
        public string BusStatus { get; set; }
        public string DefectCode { get; set; }
        public string DefectStatus { get; set; }
        public string PlanCode { get; set; }
    }
    public sealed class MaintenanceQuery
    {
        public long? BusId { get; set; }
        public string Search { get; set; }
        public MaintenanceOrderStatus? Status { get; set; }
        public MaintenanceDueState? DueState { get; set; }
    }
    public sealed class MaintenanceAttention
    {
        public long BusId { get; set; }
        public string FleetNumber { get; set; }
        public string Kind { get; set; }
        public string Reference { get; set; }
        public string Reason { get; set; }
        public bool Blocking { get; set; }
        public long? PlanId { get; set; }
        public long? DefectId { get; set; }
    }
    public sealed class MaintenanceAffectedTrip
    {
        public long TripId { get; set; }
        public long AssignmentId { get; set; }
        public string TripCode { get; set; }
        public DateTime DepartureLocal { get; set; }
        public int PurchasedTickets { get; set; }
        public byte[] TripRowVersion { get; set; }
        public byte[] AssignmentRowVersion { get; set; }
    }
    public sealed class MaintenanceStartReview
    {
        public MaintenanceStartReview() { Trips = new List<MaintenanceAffectedTrip>(); Blockers = new List<string>(); }
        public MaintenanceWorkOrder Order { get; set; }
        public IList<MaintenanceAffectedTrip> Trips { get; set; }
        public IList<string> Blockers { get; set; }
        public int PendingFuelRequests { get; set; }
        public int ActiveFuelVouchers { get; set; }
        public string FuelAuthorizationSignature { get; set; }
        public string Fingerprint { get; set; }
    }
    public sealed class MaintenanceHistoryEntry
    {
        public string Reference { get; set; }
        public DateTime OccurredUtc { get; set; }
        public string Description { get; set; }
    }
    public sealed class MaintenanceOverview
    {
        public long DuePlans { get; set; }
        public long OverduePlans { get; set; }
        public long BlockingBuses { get; set; }
        public long OpenOrders { get; set; }
        public long InProgressOrders { get; set; }
    }
}
