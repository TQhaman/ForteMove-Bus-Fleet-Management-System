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
        )) AS UnassignedTrips
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
                        UnassignedTrips = reader.GetInt64(8)
                    };
                }
            }
        }
    }
}
