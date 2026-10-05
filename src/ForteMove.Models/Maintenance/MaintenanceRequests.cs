using System;
namespace ForteMove.Models.Maintenance
{
    public sealed class SaveMaintenancePlanRequest : MaintenancePlan
    {
        public bool UseVerifiedBaseline { get; set; }
    }
    public sealed class SaveRepairProviderRequest : RepairProvider { }
    public class CreateWorkOrderRequest
    {
        public long BusId { get; set; }
        public long RepairProviderId { get; set; }
        public MaintenanceType? MaintenanceType { get; set; }
        public MaintenanceTrigger? TriggerType { get; set; }
        public long? BusDefectReportId { get; set; }
        public long? MaintenancePlanId { get; set; }
        public long? TripCannotProceedReportId { get; set; }
        public ComplianceRequirement? ComplianceRequirement { get; set; }
        public string RequestedWork { get; set; }
        public DateTime? TargetDate { get; set; }
        public string DefectReviewNote { get; set; }
        public byte[] DefectRowVersion { get; set; }
        public Guid CreationToken { get; set; }
    }
    public class MaintenanceActionRequest
    {
        public long MaintenanceWorkOrderId { get; set; }
        public byte[] RowVersion { get; set; }
        public string Note { get; set; }
    }
    public sealed class StartMaintenanceRequest : MaintenanceActionRequest { public string Fingerprint { get; set; } }
    public sealed class CompleteMaintenanceRequest : MaintenanceActionRequest
    {
        public byte[] BusRowVersion { get; set; }
        public byte[] PlanRowVersion { get; set; }
        public byte[] DefectRowVersion { get; set; }
        public decimal? ServiceOdometer { get; set; }
        public string WorkPerformed { get; set; }
        public string ExternalReference { get; set; }
        public decimal? CompletedCost { get; set; }
        public bool ResolveLinkedDefect { get; set; }
        public string ResolutionNote { get; set; }
    }
    public sealed class ReturnToServiceRequest
    {
        public long BusId { get; set; }
        public byte[] BusRowVersion { get; set; }
        public string Note { get; set; }
    }
}
