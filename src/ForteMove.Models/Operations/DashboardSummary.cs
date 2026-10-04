namespace ForteMove.Models.Operations
{
    public sealed class DashboardSummary
    {
        public long TotalFleet { get; set; }

        public long OperationalFleet { get; set; }

        public long OutOfServiceFleet { get; set; }

        public long UnderMaintenanceFleet { get; set; }

        public long RetiredFleet { get; set; }

        public long ActiveRoutes { get; set; }

        public long ActiveSchedules { get; set; }

        public long TodaysTrips { get; set; }

        public long UnassignedTrips { get; set; }

        public long AvailableDrivers { get; set; }

        public long ScheduledTrips { get; set; }
    }
}
