using System;
using System.Data;
using System.Data.SqlClient;
using ForteMove.Business.Contracts;
using ForteMove.Models.Operations;

namespace ForteMove.Data.Repositories
{
    public sealed class SqlDashboardRepository : IDashboardRepository
    {
        private const string GetSummarySql = @"
SELECT
    COUNT_BIG(*) AS TotalFleet,
    COALESCE(SUM(CONVERT(BIGINT, CASE WHEN BaseOperationalState = N'Operational' THEN 1 ELSE 0 END)), 0) AS OperationalFleet,
    COALESCE(SUM(CONVERT(BIGINT, CASE WHEN BaseOperationalState = N'OutOfService' THEN 1 ELSE 0 END)), 0) AS OutOfServiceFleet,
    COALESCE(SUM(CONVERT(BIGINT, CASE WHEN BaseOperationalState = N'UnderMaintenance' THEN 1 ELSE 0 END)), 0) AS UnderMaintenanceFleet,
    COALESCE(SUM(CONVERT(BIGINT, CASE WHEN BaseOperationalState = N'Retired' THEN 1 ELSE 0 END)), 0) AS RetiredFleet,
    (SELECT COUNT_BIG(*) FROM dbo.Routes WHERE IsActive = 1) AS ActiveRoutes,
    (SELECT COUNT_BIG(*)
       FROM dbo.RouteSchedules AS rs
       INNER JOIN dbo.RouteScheduleVersions AS rsv
           ON rsv.RouteScheduleId = rs.RouteScheduleId
          AND rsv.SupersededFromDate IS NULL
      WHERE rs.IsActive = 1
        AND @OperationalDate BETWEEN rsv.EffectiveStartDate AND rsv.EffectiveEndDate) AS ActiveSchedules,
    (SELECT COUNT_BIG(*) FROM dbo.Trips WHERE ServiceDate = @OperationalDate) AS TodaysTrips,
    (SELECT COUNT_BIG(*)
       FROM dbo.Trips
      WHERE TripStatus = N'Unassigned'
        AND
        (
            ServiceDate > @OperationalDate
            OR (ServiceDate = @OperationalDate AND ScheduledDepartureTime > @OperationalTime)
        )) AS UnassignedTrips,
    (SELECT COUNT_BIG(*)
       FROM dbo.DriverProfiles AS dp
       INNER JOIN dbo.StaffProfiles AS sp ON sp.StaffProfileId=dp.StaffProfileId
       INNER JOIN dbo.UserAccounts AS ua ON ua.UserAccountId=sp.UserAccountId
       INNER JOIN dbo.Roles AS role ON role.RoleId=ua.RoleId
      WHERE role.RoleCode=N'Driver' AND role.IsActive=1 AND ua.IsActive=1
        AND sp.EmploymentStatus=N'Active' AND dp.AvailabilityStatus=N'Available'
        AND DATEADD(year,21,dp.DateOfBirth)<=@OperationalDate
        AND dp.LicenceExpiryDate>=@OperationalDate AND dp.PrdpExpiryDate>=@OperationalDate) AS AvailableDrivers,
    (SELECT COUNT_BIG(*) FROM dbo.Trips
      WHERE TripStatus=N'Scheduled'
        AND (ServiceDate>@OperationalDate OR (ServiceDate=@OperationalDate AND ScheduledDepartureTime>@OperationalTime))) AS ScheduledTrips,
    (SELECT COUNT_BIG(*) FROM dbo.Trips WHERE TripStatus=N'Ready') AS ReadyTrips,
    (SELECT COUNT_BIG(*) FROM dbo.Trips WHERE TripStatus=N'InProgress') AS InProgressTrips,
    (SELECT COUNT_BIG(*) FROM dbo.Trips WHERE TripStatus=N'Delayed') AS DelayedTrips,
    (SELECT COUNT_BIG(*) FROM dbo.TripCannotProceedReports WHERE ResolvedUtc IS NULL) AS OpenCannotProceed,
    (SELECT COUNT_BIG(*) FROM dbo.BusDefectReports WHERE Severity=N'Critical' AND DefectStatus<>N'Resolved') AS UnresolvedCriticalDefects,
    (SELECT COUNT_BIG(DISTINCT p.BusId) FROM dbo.MaintenancePlans p JOIN dbo.Buses b ON b.BusId=p.BusId WHERE p.IsActive=1 AND ((p.NextDueDate IS NOT NULL AND p.NextDueDate<@OperationalDate) OR (p.NextDueOdometer IS NOT NULL AND p.NextDueOdometer<b.OdometerKilometres))) AS OverdueMaintenanceBuses,
    (SELECT COUNT_BIG(*) FROM dbo.MaintenanceWorkOrders WHERE OrderStatus=N'Open') AS OpenMaintenanceWorkOrders
FROM dbo.Buses;";

        private readonly string connectionString;

        public SqlDashboardRepository(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException(
                    "A SQL Server connection string is required.",
                    "connectionString");
            }

            this.connectionString = connectionString;
        }

        public DashboardSummary GetSummary(DateTime operationalNow)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = GetSummarySql;
                command.Parameters.Add("@OperationalDate", SqlDbType.Date).Value = operationalNow.Date;
                command.Parameters.Add("@OperationalTime", SqlDbType.Time).Value = operationalNow.TimeOfDay;

                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        throw new DataException(
                            "The database did not return the operations dashboard summary.");
                    }

                    return new DashboardSummary
                    {
                        TotalFleet = reader.GetInt64(0),
                        OperationalFleet = reader.GetInt64(1),
                        OutOfServiceFleet = reader.GetInt64(2),
                        UnderMaintenanceFleet = reader.GetInt64(3),
                        RetiredFleet = reader.GetInt64(4),
                        ActiveRoutes = reader.GetInt64(5),
                        ActiveSchedules = reader.GetInt64(6),
                        TodaysTrips = reader.GetInt64(7),
                        UnassignedTrips = reader.GetInt64(8),
                        AvailableDrivers = reader.GetInt64(9),
                        ScheduledTrips = reader.GetInt64(10),
                        ReadyTrips = reader.GetInt64(11),
                        InProgressTrips = reader.GetInt64(12),
                        DelayedTrips = reader.GetInt64(13),
                        OpenCannotProceed = reader.GetInt64(14),
                        UnresolvedCriticalDefects = reader.GetInt64(15),
                        OverdueMaintenanceBuses = reader.GetInt64(16),
                        OpenMaintenanceWorkOrders = reader.GetInt64(17)
                    };
                }
            }
        }
    }
}
