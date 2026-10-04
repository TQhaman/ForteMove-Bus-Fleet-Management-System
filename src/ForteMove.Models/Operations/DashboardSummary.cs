namespace ForteMove.Models.Operations
{
    public sealed class DashboardSummary
    {
        public long TotalFleet { get; set; }

        public long OperationalFleet { get; set; }

        public long OutOfServiceFleet { get; set; }

        public long UnderMaintenanceFleet { get; set; }

        public long ActiveRoutes { get; set; }
    }
}
