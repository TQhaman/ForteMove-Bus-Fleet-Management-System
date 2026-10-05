using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Identifiers;
using ForteMove.Data.Internal;
using ForteMove.Models.Routing;

namespace ForteMove.Data.Repositories
{
    public sealed partial class SqlRouteRepository : IRouteRepository
    {
        private const int MaximumSearchLength = 100;
        private const string ReferenceCodeLockResource = "ForteMove.NetworkReferenceCodes";

        private const string GetNextRouteCodeSequenceSql = @"
SELECT ISNULL(MAX(TRY_CONVERT(INT, SUBSTRING(RouteCode, 5, 16))), 0) + 1
FROM dbo.Routes
WHERE RouteCode LIKE N'FM-R%'
  AND LEN(RouteCode) > 4
  AND SUBSTRING(RouteCode, 5, 16) NOT LIKE N'%[^0-9]%'
  AND TRY_CONVERT(INT, SUBSTRING(RouteCode, 5, 16)) IS NOT NULL;";

        private const string GetNextRouteCodeSequenceForUpdateSql = @"
SELECT ISNULL(MAX(TRY_CONVERT(INT, SUBSTRING(RouteCode, 5, 16))), 0) + 1
FROM dbo.Routes WITH (UPDLOCK, HOLDLOCK)
WHERE RouteCode LIKE N'FM-R%'
  AND LEN(RouteCode) > 4
  AND SUBSTRING(RouteCode, 5, 16) NOT LIKE N'%[^0-9]%'
  AND TRY_CONVERT(INT, SUBSTRING(RouteCode, 5, 16)) IS NOT NULL;";

        private const string GetNextStopCodeSequenceSql = @"
SELECT ISNULL(MAX(TRY_CONVERT(INT, SUBSTRING(StopCode, 4, 17))), 0) + 1
FROM dbo.Stops
WHERE StopCode LIKE N'ST-%'
  AND LEN(StopCode) > 3
  AND SUBSTRING(StopCode, 4, 17) NOT LIKE N'%[^0-9]%'
  AND TRY_CONVERT(INT, SUBSTRING(StopCode, 4, 17)) IS NOT NULL;";

        private const string GetNextStopCodeSequenceForUpdateSql = @"
SELECT ISNULL(MAX(TRY_CONVERT(INT, SUBSTRING(StopCode, 4, 17))), 0) + 1
FROM dbo.Stops WITH (UPDLOCK, HOLDLOCK)
WHERE StopCode LIKE N'ST-%'
  AND LEN(StopCode) > 3
  AND SUBSTRING(StopCode, 4, 17) NOT LIKE N'%[^0-9]%'
  AND TRY_CONVERT(INT, SUBSTRING(StopCode, 4, 17)) IS NOT NULL;";

        private const string GetActiveStopsSql = @"
SELECT
    StopId,
    StopCode,
    StopName,
    Area
FROM dbo.Stops
WHERE IsActive = 1
ORDER BY StopName, Area, StopCode;";

        private const string ValidateActiveStopSql = @"
SELECT StopId
FROM dbo.Stops WITH (UPDLOCK, HOLDLOCK)
WHERE StopId = @StopId
  AND IsActive = 1;";

        private const string InsertStopSql = @"
INSERT dbo.Stops
(
    StopCode,
    StopName,
    NormalizedStopName,
    Area,
    NormalizedArea,
    Latitude,
    Longitude,
    IsActive,
    CreatedByUserAccountId,
    UpdatedByUserAccountId
)
OUTPUT inserted.StopId
VALUES
(
    @StopCode,
    @StopName,
    @NormalizedStopName,
    @Area,
    @NormalizedArea,
    @Latitude,
    @Longitude,
    @IsActive,
    @ActorUserAccountId,
    @ActorUserAccountId
);";

        private const string InsertRouteSql = @"
INSERT dbo.Routes
(
    RouteCode,
    RouteName,
    EstimatedDistanceKm,
    EstimatedDurationMinutes,
    DefaultFare,
    IsActive,
    CreatedByUserAccountId,
    UpdatedByUserAccountId
)
OUTPUT inserted.RouteId
VALUES
(
    @RouteCode,
    @RouteName,
    @EstimatedDistanceKm,
    @EstimatedDurationMinutes,
    @DefaultFare,
    @IsActive,
    @ActorUserAccountId,
    @ActorUserAccountId
);";

        private const string InsertRouteStopSql = @"
INSERT dbo.RouteStops
(
    RouteId,
    StopId,
    StopOrder,
    EstimatedMinutesFromOrigin
)
VALUES
(
    @RouteId,
    @StopId,
    @StopOrder,
    @EstimatedMinutesFromOrigin
);";

        private const string GetRouteListSql = @"
SELECT
    r.RouteId,
    r.RouteCode,
    r.RouteName,
    originStop.StopName AS OriginStopName,
    destinationStop.StopName AS DestinationStopName,
    (SELECT COUNT(*) FROM dbo.RouteStops AS routeStopCount WHERE routeStopCount.RouteId = r.RouteId) AS StopCount,
    r.EstimatedDistanceKm,
    r.EstimatedDurationMinutes,
    r.DefaultFare,
    r.IsActive
FROM dbo.Routes AS r
OUTER APPLY
(
    SELECT TOP (1) s.StopName
    FROM dbo.RouteStops AS rs
    INNER JOIN dbo.Stops AS s ON s.StopId = rs.StopId
    WHERE rs.RouteId = r.RouteId
    ORDER BY rs.StopOrder, rs.RouteStopId
) AS originStop
OUTER APPLY
(
    SELECT TOP (1) s.StopName
    FROM dbo.RouteStops AS rs
    INNER JOIN dbo.Stops AS s ON s.StopId = rs.StopId
    WHERE rs.RouteId = r.RouteId
    ORDER BY rs.StopOrder DESC, rs.RouteStopId DESC
) AS destinationStop
WHERE
    (
        @SearchPattern IS NULL
        OR r.RouteCode LIKE @SearchPattern ESCAPE N'~'
        OR r.RouteName LIKE @SearchPattern ESCAPE N'~'
        OR originStop.StopName LIKE @SearchPattern ESCAPE N'~'
        OR destinationStop.StopName LIKE @SearchPattern ESCAPE N'~'
    )
    AND (@IsActive IS NULL OR r.IsActive = @IsActive)
ORDER BY r.RouteCode, r.RouteId;";

        private const string GetRouteDetailsSql = @"
SELECT
    r.RouteId,
    r.RouteCode,
    r.RouteName,
    originStop.StopName AS OriginStopName,
    destinationStop.StopName AS DestinationStopName,
    r.EstimatedDistanceKm,
    r.EstimatedDurationMinutes,
    r.DefaultFare,
    r.IsActive
FROM dbo.Routes AS r
OUTER APPLY
(
    SELECT TOP (1) s.StopName
    FROM dbo.RouteStops AS rs
    INNER JOIN dbo.Stops AS s ON s.StopId = rs.StopId
    WHERE rs.RouteId = r.RouteId
    ORDER BY rs.StopOrder, rs.RouteStopId
) AS originStop
OUTER APPLY
(
    SELECT TOP (1) s.StopName
    FROM dbo.RouteStops AS rs
    INNER JOIN dbo.Stops AS s ON s.StopId = rs.StopId
    WHERE rs.RouteId = r.RouteId
    ORDER BY rs.StopOrder DESC, rs.RouteStopId DESC
) AS destinationStop
WHERE r.RouteId = @RouteId;

SELECT
    rs.StopId,
    s.StopCode,
    s.StopName,
    s.Area,
    rs.StopOrder,
    rs.EstimatedMinutesFromOrigin,
    s.Latitude,
    s.Longitude
FROM dbo.RouteStops AS rs
INNER JOIN dbo.Stops AS s ON s.StopId = rs.StopId
WHERE rs.RouteId = @RouteId
ORDER BY rs.StopOrder, rs.RouteStopId;";

        private readonly string connectionString;

        public SqlRouteRepository(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException(
                    "A SQL Server connection string is required.",
                    "connectionString");
            }

            this.connectionString = connectionString;
        }

        public int GetNextRouteCodeSequence()
        {
            return GetNextSequence(GetNextRouteCodeSequenceSql);
        }

        public int GetNextStopCodeSequence()
        {
            return GetNextSequence(GetNextStopCodeSequenceSql);
        }

        public IList<StopOption> GetActiveStops()
        {
            IList<StopOption> stops = new List<StopOption>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = GetActiveStopsSql;

                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        stops.Add(new StopOption
                        {
                            StopId = reader.GetInt64(0),
                            StopCode = reader.GetString(1),
                            StopName = reader.GetString(2),
                            Area = reader.GetString(3)
                        });
                    }
                }
            }

            return stops;
        }

        public RouteCreationResult CreateRoute(
            RouteCreationAggregate aggregate,
            long actorUserAccountId)
        {
            if (aggregate == null)
            {
                throw new ArgumentNullException("aggregate");
            }

            if (aggregate.Route == null)
            {
                throw new ArgumentException(
                    "The route aggregate must include a route.",
                    "aggregate");
            }

            if (aggregate.Stops == null)
            {
                throw new ArgumentException(
                    "The route aggregate must include its ordered stops.",
                    "aggregate");
            }

            if (actorUserAccountId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    "actorUserAccountId",
                    "The actor user account identifier must be greater than zero.");
            }

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                    {
                        AcquireReferenceCodeLock(connection, transaction);

                        int routeSequence = GetNextSequence(
                            connection,
                            transaction,
                            GetNextRouteCodeSequenceForUpdateSql);
                        int stopSequence = GetNextSequence(
                            connection,
                            transaction,
                            GetNextStopCodeSequenceForUpdateSql);

                        aggregate.Route.RouteCode =
                            IdentifierCodePolicy.FormatRouteCode(routeSequence);

                        DateTime occurredUtc = DateTime.UtcNow;
                        IDictionary<RouteCreationStop, long> resolvedStopIds =
                            new Dictionary<RouteCreationStop, long>();

                        foreach (RouteCreationStop routeStop in aggregate.Stops)
                        {
                            if (routeStop.ExistingStopId.HasValue)
                            {
                                ValidateActiveStop(
                                    connection,
                                    transaction,
                                    routeStop.ExistingStopId.Value);
                                resolvedStopIds.Add(
                                    routeStop,
                                    routeStop.ExistingStopId.Value);
                                continue;
                            }

                            Stop newStop = routeStop.NewStop;
                            if (newStop == null)
                            {
                                throw new DataException(
                                    "A route stop did not contain an existing or new stop.");
                            }

                            newStop.StopCode =
                                IdentifierCodePolicy.FormatStopCode(stopSequence++);
                            long stopId = InsertStop(
                                connection,
                                transaction,
                                newStop,
                                actorUserAccountId);
                            newStop.StopId = stopId;
                            resolvedStopIds.Add(routeStop, stopId);

                            SqlAuditWriter.Write(
                                connection,
                                transaction,
                                actorUserAccountId,
                                "StopCreated",
                                "Stop",
                                stopId.ToString(CultureInfo.InvariantCulture),
                                "StopCode=" + newStop.StopCode,
                                null,
                                occurredUtc);
                        }

                        long routeId = InsertRoute(
                            connection,
                            transaction,
                            aggregate.Route,
                            actorUserAccountId);
                        aggregate.Route.RouteId = routeId;

                        foreach (RouteCreationStop routeStop in aggregate.Stops)
                        {
                            InsertRouteStop(
                                connection,
                                transaction,
                                routeId,
                                resolvedStopIds[routeStop],
                                routeStop.StopOrder,
                                routeStop.EstimatedMinutesFromOrigin);
                        }

                        SqlAuditWriter.Write(
                            connection,
                            transaction,
                            actorUserAccountId,
                            "RouteCreated",
                            "Route",
                            routeId.ToString(CultureInfo.InvariantCulture),
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "RouteCode={0};StopCount={1}",
                                aggregate.Route.RouteCode,
                                aggregate.Stops.Count),
                            null,
                            occurredUtc);

                        transaction.Commit();
                        return new RouteCreationResult
                        {
                            RouteId = routeId,
                            RouteCode = aggregate.Route.RouteCode
                        };
                    }
                }
            }
            catch (SqlException exception)
            {
                if (!IsDuplicateKeyViolation(exception))
                {
                    throw;
                }

                if (ContainsIgnoreCase(exception.Message, "UQ_Routes_RouteCode"))
                {
                    throw new DuplicateRouteException(
                        DuplicateRouteField.RouteCode,
                        "A route with this route code already exists.",
                        exception);
                }

                if (ContainsIgnoreCase(exception.Message, "UQ_Stops_StopCode"))
                {
                    throw new DuplicateStopException(
                        DuplicateStopField.StopCode,
                        "A stop with this stop code already exists.",
                        exception);
                }

                if (ContainsIgnoreCase(exception.Message, "UX_Stops_ActiveNormalizedNameArea"))
                {
                    throw new DuplicateStopException(
                        DuplicateStopField.NameAndArea,
                        "An active stop with this name and area already exists.",
                        exception);
                }

                throw;
            }
        }

        public IList<RouteListItem> GetRouteList(RouteQuery query)
        {
            string searchTerm = null;
            bool? isActive = null;
            if (query != null)
            {
                searchTerm = string.IsNullOrWhiteSpace(query.SearchTerm)
                    ? null
                    : query.SearchTerm.Trim();
                isActive = query.IsActive;
            }

            if (searchTerm != null && searchTerm.Length > MaximumSearchLength)
            {
                throw new ArgumentException(
                    "The route search term cannot exceed 100 characters.",
                    "query");
            }

            string searchPattern = searchTerm == null
                ? null
                : "%" + EscapeLikeValue(searchTerm) + "%";

            IList<RouteListItem> routes = new List<RouteListItem>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = GetRouteListSql;

                SqlParameter searchParameter = command.Parameters.Add(
                    "@SearchPattern",
                    SqlDbType.NVarChar,
                    202);
                searchParameter.Value = (object)searchPattern ?? DBNull.Value;

                SqlParameter activeParameter = command.Parameters.Add(
                    "@IsActive",
                    SqlDbType.Bit);
                activeParameter.Value = isActive.HasValue
                    ? (object)isActive.Value
                    : DBNull.Value;

                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        routes.Add(new RouteListItem
                        {
                            RouteId = reader.GetInt64(0),
                            RouteCode = reader.GetString(1),
                            RouteName = reader.GetString(2),
                            OriginStopName = GetNullableString(reader, 3),
                            DestinationStopName = GetNullableString(reader, 4),
                            StopCount = reader.GetInt32(5),
                            EstimatedDistanceKm = reader.GetDecimal(6),
                            EstimatedDurationMinutes = reader.GetInt32(7),
                            DefaultFare = reader.GetDecimal(8),
                            IsActive = reader.GetBoolean(9)
                        });
                    }
                }
            }

            return routes;
        }

        public RouteDetails GetRouteDetails(long routeId)
        {
            if (routeId <= 0)
            {
                return null;
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = GetRouteDetailsSql;

                SqlParameter routeParameter = command.Parameters.Add(
                    "@RouteId",
                    SqlDbType.BigInt);
                routeParameter.Value = routeId;

                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    RouteDetails details = new RouteDetails
                    {
                        RouteId = reader.GetInt64(0),
                        RouteCode = reader.GetString(1),
                        RouteName = reader.GetString(2),
                        OriginStopName = GetNullableString(reader, 3),
                        DestinationStopName = GetNullableString(reader, 4),
                        EstimatedDistanceKm = reader.GetDecimal(5),
                        EstimatedDurationMinutes = reader.GetInt32(6),
                        DefaultFare = reader.GetDecimal(7),
                        IsActive = reader.GetBoolean(8)
                    };

                    if (reader.NextResult())
                    {
                        while (reader.Read())
                        {
                            details.Stops.Add(new RouteStopDetails
                            {
                                StopId = reader.GetInt64(0),
                                StopCode = reader.GetString(1),
                                StopName = reader.GetString(2),
                                Area = reader.GetString(3),
                                StopOrder = reader.GetInt32(4),
                                EstimatedMinutesFromOrigin = reader.IsDBNull(5)
                                    ? (int?)null
                                    : reader.GetInt32(5),
                                Latitude = reader.IsDBNull(6) ? (decimal?)null : reader.GetDecimal(6),
                                Longitude = reader.IsDBNull(7) ? (decimal?)null : reader.GetDecimal(7)
                            });
                        }
                    }

                    return details;
                }
            }
        }

        private int GetNextSequence(string commandText)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = commandText;
                connection.Open();
                return Convert.ToInt32(
                    command.ExecuteScalar(),
                    CultureInfo.InvariantCulture);
            }
        }

        private static int GetNextSequence(
            SqlConnection connection,
            SqlTransaction transaction,
            string commandText)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.Text;
                command.CommandText = commandText;
                return Convert.ToInt32(
                    command.ExecuteScalar(),
                    CultureInfo.InvariantCulture);
            }
        }

        private static void AcquireReferenceCodeLock(
            SqlConnection connection,
            SqlTransaction transaction)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.Text;
                command.CommandText = @"
DECLARE @Result INT;
EXEC @Result = sys.sp_getapplock
    @Resource = @Resource,
    @LockMode = N'Exclusive',
    @LockOwner = N'Transaction',
    @LockTimeout = 15000;
SELECT @Result;";

                SqlParameter resourceParameter = command.Parameters.Add(
                    "@Resource",
                    SqlDbType.NVarChar,
                    255);
                resourceParameter.Value = ReferenceCodeLockResource;

                int result = Convert.ToInt32(
                    command.ExecuteScalar(),
                    CultureInfo.InvariantCulture);
                if (result < 0)
                {
                    throw new DataException(
                        "The route reference-code lock could not be acquired.");
                }
            }
        }

        private static void ValidateActiveStop(
            SqlConnection connection,
            SqlTransaction transaction,
            long stopId)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.Text;
                command.CommandText = ValidateActiveStopSql;

                SqlParameter stopParameter = command.Parameters.Add(
                    "@StopId",
                    SqlDbType.BigInt);
                stopParameter.Value = stopId;

                object result = command.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    throw new UnavailableRouteStopException(stopId);
                }
            }
        }

        private static long InsertStop(
            SqlConnection connection,
            SqlTransaction transaction,
            Stop stop,
            long actorUserAccountId)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.Text;
                command.CommandText = InsertStopSql;

                AddRequiredStringParameter(command, "@StopCode", 20, stop.StopCode);
                AddRequiredStringParameter(command, "@StopName", 150, stop.StopName);
                AddRequiredStringParameter(
                    command,
                    "@NormalizedStopName",
                    150,
                    stop.NormalizedStopName);
                AddRequiredStringParameter(command, "@Area", 150, stop.Area);
                AddRequiredStringParameter(
                    command,
                    "@NormalizedArea",
                    150,
                    stop.NormalizedArea);
                AddDecimalParameter(command, "@Latitude", 9, 6, stop.Latitude);
                AddDecimalParameter(command, "@Longitude", 9, 6, stop.Longitude);

                SqlParameter activeParameter = command.Parameters.Add(
                    "@IsActive",
                    SqlDbType.Bit);
                activeParameter.Value = stop.IsActive;

                SqlParameter actorParameter = command.Parameters.Add(
                    "@ActorUserAccountId",
                    SqlDbType.BigInt);
                actorParameter.Value = actorUserAccountId;

                return GetRequiredInt64(
                    command.ExecuteScalar(),
                    "The database did not return the new stop identifier.");
            }
        }

        private static long InsertRoute(
            SqlConnection connection,
            SqlTransaction transaction,
            Route route,
            long actorUserAccountId)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.Text;
                command.CommandText = InsertRouteSql;

                AddRequiredStringParameter(command, "@RouteCode", 20, route.RouteCode);
                AddRequiredStringParameter(command, "@RouteName", 150, route.RouteName);
                AddDecimalParameter(
                    command,
                    "@EstimatedDistanceKm",
                    8,
                    2,
                    route.EstimatedDistanceKm);

                SqlParameter durationParameter = command.Parameters.Add(
                    "@EstimatedDurationMinutes",
                    SqlDbType.Int);
                durationParameter.Value = route.EstimatedDurationMinutes;

                AddDecimalParameter(
                    command,
                    "@DefaultFare",
                    10,
                    2,
                    route.DefaultFare);

                SqlParameter activeParameter = command.Parameters.Add(
                    "@IsActive",
                    SqlDbType.Bit);
                activeParameter.Value = route.IsActive;

                SqlParameter actorParameter = command.Parameters.Add(
                    "@ActorUserAccountId",
                    SqlDbType.BigInt);
                actorParameter.Value = actorUserAccountId;

                return GetRequiredInt64(
                    command.ExecuteScalar(),
                    "The database did not return the new route identifier.");
            }
        }

        private static void InsertRouteStop(
            SqlConnection connection,
            SqlTransaction transaction,
            long routeId,
            long stopId,
            int stopOrder,
            int? estimatedMinutesFromOrigin)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.Text;
                command.CommandText = InsertRouteStopSql;

                SqlParameter routeParameter = command.Parameters.Add(
                    "@RouteId",
                    SqlDbType.BigInt);
                routeParameter.Value = routeId;

                SqlParameter stopParameter = command.Parameters.Add(
                    "@StopId",
                    SqlDbType.BigInt);
                stopParameter.Value = stopId;

                SqlParameter orderParameter = command.Parameters.Add(
                    "@StopOrder",
                    SqlDbType.Int);
                orderParameter.Value = stopOrder;

                SqlParameter minutesParameter = command.Parameters.Add(
                    "@EstimatedMinutesFromOrigin",
                    SqlDbType.Int);
                minutesParameter.Value = estimatedMinutesFromOrigin.HasValue
                    ? (object)estimatedMinutesFromOrigin.Value
                    : DBNull.Value;

                command.ExecuteNonQuery();
            }
        }

        private static void AddRequiredStringParameter(
            SqlCommand command,
            string parameterName,
            int size,
            string value)
        {
            SqlParameter parameter = command.Parameters.Add(
                parameterName,
                SqlDbType.NVarChar,
                size);
            parameter.Value = (object)value ?? DBNull.Value;
        }

        private static void AddDecimalParameter(
            SqlCommand command,
            string parameterName,
            byte precision,
            byte scale,
            decimal? value)
        {
            SqlParameter parameter = command.Parameters.Add(
                parameterName,
                SqlDbType.Decimal);
            parameter.Precision = precision;
            parameter.Scale = scale;
            parameter.Value = value.HasValue ? (object)value.Value : DBNull.Value;
        }

        private static long GetRequiredInt64(object value, string errorMessage)
        {
            if (value == null || value == DBNull.Value)
            {
                throw new DataException(errorMessage);
            }

            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static string GetNullableString(SqlDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        private static bool IsDuplicateKeyViolation(SqlException exception)
        {
            foreach (SqlError error in exception.Errors)
            {
                if (error.Number == 2601 || error.Number == 2627)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return source != null &&
                source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string EscapeLikeValue(string value)
        {
            return value
                .Replace("~", "~~")
                .Replace("%", "~%")
                .Replace("_", "~_")
                .Replace("[", "~[");
        }
    }
}
