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
    (SELECT COUNT_BIG(*) FROM dbo.Routes WHERE IsActive = 1) AS ActiveRoutes
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

        public DashboardSummary GetSummary()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = GetSummarySql;

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
                        ActiveRoutes = reader.GetInt64(4)
                    };
                }
            }
        }
    }
}
