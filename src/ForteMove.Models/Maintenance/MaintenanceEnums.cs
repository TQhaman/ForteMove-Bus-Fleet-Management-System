namespace ForteMove.Models.Maintenance
{
    public enum MaintenanceDueState { NotDue, Due, Overdue }
    public enum MaintenanceType { Preventive, Corrective, Breakdown }
    public enum MaintenanceTrigger { ServiceDate, ServiceOdometer, Defect, Breakdown, ComplianceExpiry, Manual }
    public enum MaintenanceOrderStatus { Open, InProgress, Completed, Cancelled }
    public enum ComplianceRequirement { Licence, Roadworthy, Insurance }
}
