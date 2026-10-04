using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Identifiers;
using ForteMove.Data.Internal;
using ForteMove.Models.Fleet;
using ForteMove.Models.Scheduling;

namespace ForteMove.Data.Repositories
{
    public sealed class SqlSchedulingRepository : ISchedulingRepository
    {
        private const string ReferenceCodeLockResource = "ForteMove.SchedulingReferenceCodes";
        private readonly string connectionString;

        public SqlSchedulingRepository(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("A SQL Server connection string is required.", "connectionString");
            this.connectionString = connectionString;
        }

        public int GetNextScheduleCodeSequence()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = NextScheduleSequenceSql(string.Empty);
                connection.Open();
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        public SchedulingCreationOptions GetCreationOptions()
        {
            SchedulingCreationOptions options = new SchedulingCreationOptions();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT r.RouteId, r.RouteCode, r.RouteName, r.EstimatedDurationMinutes, r.RowVersion,
       origin_stop.StopName, destination_stop.StopName
FROM dbo.Routes AS r
OUTER APPLY
(
    SELECT TOP (1) s.StopName
    FROM dbo.RouteStops AS rs
    INNER JOIN dbo.Stops AS s ON s.StopId = rs.StopId
    WHERE rs.RouteId = r.RouteId
    ORDER BY rs.StopOrder
) AS origin_stop
OUTER APPLY
(
    SELECT TOP (1) s.StopName
    FROM dbo.RouteStops AS rs
    INNER JOIN dbo.Stops AS s ON s.StopId = rs.StopId
    WHERE rs.RouteId = r.RouteId
    ORDER BY rs.StopOrder DESC
) AS destination_stop
WHERE r.IsActive = 1
ORDER BY r.RouteName, r.RouteCode;";
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            options.Routes.Add(new ScheduleRouteOption
                            {
                                RouteId = reader.GetInt64(0),
                                RouteCode = reader.GetString(1),
                                RouteName = reader.GetString(2),
                                EstimatedDurationMinutes = reader.GetInt32(3),
                                RowVersion = (byte[])reader.GetValue(4),
                                OriginName = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                                DestinationName = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
                            });
                        }
                    }
                }

                options.BusCategories = ReadBusCategories(connection, null);
            }
            return options;
        }

        public ScheduleCreationResult CreateSchedule(
            ScheduleCreationAggregate aggregate,
            long actorUserAccountId)
        {
            if (aggregate == null) throw new ArgumentNullException("aggregate");
            if (actorUserAccountId <= 0) throw new ArgumentOutOfRangeException("actorUserAccountId");

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                    {
                        AcquireReferenceCodeLock(connection, transaction);
                        ValidateRoute(connection, transaction, aggregate.RouteId, aggregate.RouteRowVersion);
                        ValidateCategory(connection, transaction, aggregate.PreferredBusCategoryId);

                        int scheduleSequence = GetInt64ScalarAsInt(
                            connection,
                            transaction,
                            NextScheduleSequenceSql("WITH (UPDLOCK, HOLDLOCK)"));
                        long tripSequence = GetLongScalar(
                            connection,
                            transaction,
                            NextTripSequenceSql("WITH (UPDLOCK, HOLDLOCK)"));
                        string scheduleCode = IdentifierCodePolicy.FormatScheduleCode(scheduleSequence);
                        DateTime occurredUtc = DateTime.UtcNow;

                        long scheduleId = InsertSchedule(
                            connection, transaction, scheduleCode, aggregate.RouteId, actorUserAccountId);
                        long versionId = InsertVersion(
                            connection, transaction, scheduleId, aggregate.RouteId, 1,
                            aggregate.EffectiveStartDate, aggregate.EffectiveEndDate,
                            aggregate.PreferredBusCategoryId, aggregate.ExpectedCapacity,
                            actorUserAccountId);
                        InsertPattern(connection, transaction, versionId, aggregate.OperatingDays, aggregate.DepartureTimes);

                        int generatedCount = 0;
                        foreach (ScheduleOccurrence occurrence in aggregate.Occurrences)
                        {
                            InsertTrip(
                                connection,
                                transaction,
                                IdentifierCodePolicy.FormatTripCode(tripSequence++),
                                versionId,
                                aggregate.RouteId,
                                occurrence,
                                actorUserAccountId);
                            generatedCount++;
                        }

                        SqlAuditWriter.Write(
                            connection, transaction, actorUserAccountId,
                            "ScheduleCreated", "RouteSchedule",
                            scheduleId.ToString(CultureInfo.InvariantCulture),
                            string.Format(CultureInfo.InvariantCulture,
                                "ScheduleCode={0};Version=1;RouteId={1}", scheduleCode, aggregate.RouteId),
                            null, occurredUtc);
                        WriteTripsGeneratedAudit(
                            connection, transaction, actorUserAccountId, versionId,
                            scheduleCode, generatedCount, aggregate.Occurrences, occurredUtc);

                        transaction.Commit();
                        return new ScheduleCreationResult
                        {
                            RouteScheduleId = scheduleId,
                            ScheduleCode = scheduleCode,
                            GeneratedTripCount = generatedCount
                        };
                    }
                }
            }
            catch (SqlException exception)
            {
                if (exception.Number == 2601 || exception.Number == 2627)
                    throw new SchedulingConflictException(
                        "That Route already has a Trip at one or more selected departure times.");
                throw;
            }
        }

        public IList<ScheduleListItem> GetScheduleList(
            ScheduleQuery query,
            DateTime operationalNow)
        {
            List<ScheduleListItem> items = new List<ScheduleListItem>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
WITH CurrentSchedules AS
(
    SELECT rs.RouteScheduleId, rs.ScheduleCode, rs.IsActive,
           r.RouteCode, r.RouteName,
           rsv.RouteScheduleVersionId, rsv.EffectiveStartDate, rsv.EffectiveEndDate,
           CASE
               WHEN rs.IsActive = 0 THEN N'Inactive'
               WHEN @Today < rsv.EffectiveStartDate THEN N'Upcoming'
               WHEN @Today > rsv.EffectiveEndDate THEN N'Ended'
               ELSE N'Active'
           END AS StatusCode
    FROM dbo.RouteSchedules AS rs
    INNER JOIN dbo.Routes AS r ON r.RouteId = rs.RouteId
    INNER JOIN dbo.RouteScheduleVersions AS rsv
        ON rsv.RouteScheduleId = rs.RouteScheduleId
       AND rsv.SupersededFromDate IS NULL
)
SELECT cs.RouteScheduleId, cs.ScheduleCode, cs.RouteCode, cs.RouteName,
       cs.EffectiveStartDate, cs.EffectiveEndDate, cs.StatusCode,
       (SELECT STRING_AGG(CONVERT(VARCHAR(1), sod.DayOfWeek), ',') WITHIN GROUP (ORDER BY sod.DayOfWeek)
          FROM dbo.ScheduleOperatingDays AS sod
         WHERE sod.RouteScheduleVersionId = cs.RouteScheduleVersionId),
       (SELECT STRING_AGG(CONVERT(CHAR(5), sdt.DepartureTime, 108), ', ') WITHIN GROUP (ORDER BY sdt.DepartureTime)
          FROM dbo.ScheduleDepartureTimes AS sdt
         WHERE sdt.RouteScheduleVersionId = cs.RouteScheduleVersionId),
       (SELECT COUNT_BIG(*)
          FROM dbo.Trips AS t
          INNER JOIN dbo.RouteScheduleVersions AS all_versions
              ON all_versions.RouteScheduleVersionId = t.RouteScheduleVersionId
         WHERE all_versions.RouteScheduleId = cs.RouteScheduleId)
FROM CurrentSchedules AS cs
WHERE (@Search = N'' OR cs.ScheduleCode LIKE @Pattern OR cs.RouteCode LIKE @Pattern OR cs.RouteName LIKE @Pattern)
  AND (@Status IS NULL OR cs.StatusCode = @Status)
ORDER BY cs.ScheduleCode;";
                AddNVarChar(command, "@Search", 100, query.SearchTerm ?? string.Empty);
                AddNVarChar(command, "@Pattern", 202, "%" + EscapeLike(query.SearchTerm ?? string.Empty) + "%");
                AddDate(command, "@Today", operationalNow.Date);
                AddNullableNVarChar(command, "@Status", 20,
                    query.Status.HasValue ? query.Status.Value.ToString() : null);
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new ScheduleListItem
                        {
                            RouteScheduleId = reader.GetInt64(0),
                            ScheduleCode = reader.GetString(1),
                            RouteCode = reader.GetString(2),
                            RouteName = reader.GetString(3),
                            EffectiveStartDate = reader.GetDateTime(4),
                            EffectiveEndDate = reader.GetDateTime(5),
                            Status = ParseScheduleStatus(reader.GetString(6)),
                            OperatingDaysDisplay = FormatDays(reader.IsDBNull(7) ? null : reader.GetString(7)),
                            DepartureTimesDisplay = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                            GeneratedTripCount = reader.GetInt64(9)
                        });
                    }
                }
            }
            return items;
        }

        public ScheduleDetails GetScheduleDetails(long routeScheduleId, DateTime operationalNow)
        {
            ScheduleDetails details = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT rs.RouteScheduleId, rs.ScheduleCode, rs.RouteId, rs.IsActive,
       r.RouteCode, r.RouteName,
       origin_stop.StopName, destination_stop.StopName,
       rsv.RouteScheduleVersionId, rsv.VersionNumber,
       rsv.EffectiveStartDate, rsv.EffectiveEndDate,
       bc.DisplayName, rsv.ExpectedCapacity, rsv.CreatedUtc,
       (SELECT STRING_AGG(CONVERT(VARCHAR(1), sod.DayOfWeek), ',') WITHIN GROUP (ORDER BY sod.DayOfWeek) FROM dbo.ScheduleOperatingDays AS sod WHERE sod.RouteScheduleVersionId = rsv.RouteScheduleVersionId),
       (SELECT STRING_AGG(CONVERT(CHAR(5), sdt.DepartureTime, 108), ', ') WITHIN GROUP (ORDER BY sdt.DepartureTime) FROM dbo.ScheduleDepartureTimes AS sdt WHERE sdt.RouteScheduleVersionId = rsv.RouteScheduleVersionId),
       (SELECT COUNT_BIG(*) FROM dbo.Trips AS t INNER JOIN dbo.RouteScheduleVersions AS av ON av.RouteScheduleVersionId = t.RouteScheduleVersionId WHERE av.RouteScheduleId = rs.RouteScheduleId)
FROM dbo.RouteSchedules AS rs
INNER JOIN dbo.Routes AS r ON r.RouteId = rs.RouteId
INNER JOIN dbo.RouteScheduleVersions AS rsv ON rsv.RouteScheduleId = rs.RouteScheduleId AND rsv.SupersededFromDate IS NULL
LEFT JOIN dbo.BusCategories AS bc ON bc.BusCategoryId = rsv.PreferredBusCategoryId
OUTER APPLY (SELECT TOP (1) s.StopName FROM dbo.RouteStops AS x INNER JOIN dbo.Stops AS s ON s.StopId=x.StopId WHERE x.RouteId=r.RouteId ORDER BY x.StopOrder) AS origin_stop
OUTER APPLY (SELECT TOP (1) s.StopName FROM dbo.RouteStops AS x INNER JOIN dbo.Stops AS s ON s.StopId=x.StopId WHERE x.RouteId=r.RouteId ORDER BY x.StopOrder DESC) AS destination_stop
WHERE rs.RouteScheduleId = @RouteScheduleId;";
                    AddBigInt(command, "@RouteScheduleId", routeScheduleId);
                    using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (!reader.Read()) return null;
                        bool active = reader.GetBoolean(3);
                        DateTime start = reader.GetDateTime(10);
                        DateTime end = reader.GetDateTime(11);
                        details = new ScheduleDetails
                        {
                            RouteScheduleId = reader.GetInt64(0),
                            ScheduleCode = reader.GetString(1),
                            RouteId = reader.GetInt64(2),
                            IsActive = active,
                            RouteCode = reader.GetString(4),
                            RouteName = reader.GetString(5),
                            OriginName = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                            DestinationName = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                            Status = DeriveStatus(active, start, end, operationalNow.Date),
                            GeneratedTripCount = reader.GetInt64(17),
                            CurrentVersion = new ScheduleVersionDetails
                            {
                                RouteScheduleVersionId = reader.GetInt64(8),
                                VersionNumber = reader.GetInt32(9),
                                EffectiveStartDate = start,
                                EffectiveEndDate = end,
                                PreferredBusCategoryName = reader.IsDBNull(12) ? null : reader.GetString(12),
                                ExpectedCapacity = reader.IsDBNull(13) ? (int?)null : reader.GetInt32(13),
                                CreatedUtc = reader.GetDateTime(14),
                                OperatingDaysDisplay = FormatDays(reader.IsDBNull(15) ? null : reader.GetString(15)),
                                DepartureTimesDisplay = reader.IsDBNull(16) ? string.Empty : reader.GetString(16)
                            }
                        };
                    }
                }

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT rsv.RouteScheduleVersionId, rsv.VersionNumber, rsv.EffectiveStartDate,
       rsv.EffectiveEndDate, rsv.SupersededFromDate, bc.DisplayName,
       rsv.ExpectedCapacity, rsv.CreatedUtc,
       (SELECT STRING_AGG(CONVERT(VARCHAR(1), sod.DayOfWeek), ',') WITHIN GROUP (ORDER BY sod.DayOfWeek) FROM dbo.ScheduleOperatingDays AS sod WHERE sod.RouteScheduleVersionId=rsv.RouteScheduleVersionId),
       (SELECT STRING_AGG(CONVERT(CHAR(5), sdt.DepartureTime, 108), ', ') WITHIN GROUP (ORDER BY sdt.DepartureTime) FROM dbo.ScheduleDepartureTimes AS sdt WHERE sdt.RouteScheduleVersionId=rsv.RouteScheduleVersionId)
FROM dbo.RouteScheduleVersions AS rsv
LEFT JOIN dbo.BusCategories AS bc ON bc.BusCategoryId=rsv.PreferredBusCategoryId
WHERE rsv.RouteScheduleId=@RouteScheduleId AND rsv.SupersededFromDate IS NOT NULL
ORDER BY rsv.VersionNumber DESC;";
                    AddBigInt(command, "@RouteScheduleId", routeScheduleId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            details.History.Add(new ScheduleVersionDetails
                            {
                                RouteScheduleVersionId = reader.GetInt64(0),
                                VersionNumber = reader.GetInt32(1),
                                EffectiveStartDate = reader.GetDateTime(2),
                                EffectiveEndDate = reader.GetDateTime(3),
                                SupersededFromDate = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                                PreferredBusCategoryName = reader.IsDBNull(5) ? null : reader.GetString(5),
                                ExpectedCapacity = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                                CreatedUtc = reader.GetDateTime(7),
                                OperatingDaysDisplay = FormatDays(reader.IsDBNull(8) ? null : reader.GetString(8)),
                                DepartureTimesDisplay = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
                            });
                        }
                    }
                }
            }
            return details;
        }

        public IList<TripListItem> GetTripList(TripQuery query)
        {
            List<TripListItem> items = new List<TripListItem>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT t.TripId, t.TripCode, t.ServiceDate, t.ScheduledDepartureTime,
       t.ExpectedFinishLocal, r.RouteCode, r.RouteName,
       origin_stop.StopName, destination_stop.StopName,
       t.TripStatus, t.RequiresReview
FROM dbo.Trips AS t
INNER JOIN dbo.Routes AS r ON r.RouteId=t.RouteId
OUTER APPLY (SELECT TOP (1) s.StopName FROM dbo.RouteStops AS x INNER JOIN dbo.Stops AS s ON s.StopId=x.StopId WHERE x.RouteId=r.RouteId ORDER BY x.StopOrder) AS origin_stop
OUTER APPLY (SELECT TOP (1) s.StopName FROM dbo.RouteStops AS x INNER JOIN dbo.Stops AS s ON s.StopId=x.StopId WHERE x.RouteId=r.RouteId ORDER BY x.StopOrder DESC) AS destination_stop
WHERE (@ServiceDate IS NULL OR t.ServiceDate=@ServiceDate)
  AND (@RouteId IS NULL OR t.RouteId=@RouteId)
  AND (@Status IS NULL OR t.TripStatus=@Status)
ORDER BY t.ServiceDate, t.ScheduledDepartureTime, r.RouteCode;";
                AddNullableDate(command, "@ServiceDate", query.ServiceDate);
                AddNullableBigInt(command, "@RouteId", query.RouteId);
                AddNullableNVarChar(command, "@Status", 30,
                    query.Status.HasValue ? query.Status.Value.ToString() : null);
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new TripListItem
                        {
                            TripId = reader.GetInt64(0),
                            TripCode = reader.GetString(1),
                            ServiceDate = reader.GetDateTime(2),
                            ScheduledDepartureTime = reader.GetTimeSpan(3),
                            ExpectedFinishLocal = reader.GetDateTime(4),
                            RouteCode = reader.GetString(5),
                            RouteName = reader.GetString(6),
                            OriginName = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                            DestinationName = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                            Status = (TripStatus)Enum.Parse(typeof(TripStatus), reader.GetString(9), false),
                            RequiresReview = reader.GetBoolean(10)
                        });
                    }
                }
            }
            return items;
        }

        public ScheduleChangeOptions GetChangeOptions(long routeScheduleId, DateTime? fromDate)
        {
            ScheduleChangeOptions options = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT rs.RouteScheduleId, rs.ScheduleCode, rs.RouteId, rs.RowVersion,
       r.RouteCode, r.RouteName, r.IsActive, r.EstimatedDurationMinutes,
       rsv.RouteScheduleVersionId, rsv.VersionNumber, rsv.EffectiveStartDate,
       rsv.EffectiveEndDate, rsv.PreferredBusCategoryId, rsv.ExpectedCapacity,
       rsv.RowVersion
FROM dbo.RouteSchedules AS rs
INNER JOIN dbo.Routes AS r ON r.RouteId=rs.RouteId
INNER JOIN dbo.RouteScheduleVersions AS rsv ON rsv.RouteScheduleId=rs.RouteScheduleId AND rsv.SupersededFromDate IS NULL
WHERE rs.RouteScheduleId=@RouteScheduleId AND rs.IsActive=1;";
                    AddBigInt(command, "@RouteScheduleId", routeScheduleId);
                    using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (!reader.Read()) return null;
                        options = new ScheduleChangeOptions
                        {
                            RouteScheduleId = reader.GetInt64(0),
                            ScheduleCode = reader.GetString(1),
                            RouteId = reader.GetInt64(2),
                            ScheduleRowVersion = (byte[])reader.GetValue(3),
                            RouteCode = reader.GetString(4),
                            RouteName = reader.GetString(5),
                            RouteIsActive = reader.GetBoolean(6),
                            EstimatedDurationMinutes = reader.GetInt32(7),
                            RouteScheduleVersionId = reader.GetInt64(8),
                            VersionNumber = reader.GetInt32(9),
                            EffectiveStartDate = reader.GetDateTime(10),
                            EffectiveEndDate = reader.GetDateTime(11),
                            PreferredBusCategoryId = reader.IsDBNull(12) ? (int?)null : reader.GetInt32(12),
                            ExpectedCapacity = reader.IsDBNull(13) ? (int?)null : reader.GetInt32(13),
                            VersionRowVersion = (byte[])reader.GetValue(14)
                        };
                    }
                }

                options.OperatingDays = ReadDays(connection, null, options.RouteScheduleVersionId);
                options.DepartureTimes = ReadTimes(connection, null, options.RouteScheduleVersionId);
                options.BusCategories = ReadBusCategories(connection, null);
                if (fromDate.HasValue)
                    options.ExistingTripsFromCutover = ReadExistingTrips(
                        connection, null, options.RouteScheduleId, fromDate.Value.Date, false);
            }
            return options;
        }

        public ScheduleChangeResult ApplyScheduleChange(
            ScheduleChangeAggregate aggregate,
            long actorUserAccountId)
        {
            if (aggregate == null) throw new ArgumentNullException("aggregate");
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                    {
                        AcquireReferenceCodeLock(connection, transaction);
                        ValidateCurrentVersion(connection, transaction, aggregate);
                        ValidateCategory(connection, transaction, aggregate.PreferredBusCategoryId);

                        IList<ExistingScheduleTrip> existing = ReadExistingTrips(
                            connection, transaction, aggregate.RouteScheduleId,
                            aggregate.ChangeEffectiveDate, true);
                        int safeCount = existing.Count(item => item.IsUntouched);
                        int protectedCount = existing.Count - safeCount;
                        if (safeCount != aggregate.ExpectedFutureTripsToReplace ||
                            protectedCount != aggregate.ExpectedProtectedTrips)
                        {
                            throw new SchedulingConcurrencyException(
                                "Trip activity changed after the preview. Review the schedule change again.");
                        }

                        MarkProtectedTrips(connection, transaction, aggregate.RouteScheduleId,
                            aggregate.ChangeEffectiveDate, actorUserAccountId);
                        int deleted = DeleteUntouchedTrips(connection, transaction,
                            aggregate.RouteScheduleId, aggregate.ChangeEffectiveDate);
                        SupersedeVersion(connection, transaction, aggregate, actorUserAccountId);
                        long newVersionId = InsertVersion(
                            connection, transaction, aggregate.RouteScheduleId, aggregate.RouteId,
                            aggregate.NewVersionNumber, aggregate.ChangeEffectiveDate,
                            aggregate.EffectiveEndDate, aggregate.PreferredBusCategoryId,
                            aggregate.ExpectedCapacity, actorUserAccountId);
                        InsertPattern(connection, transaction, newVersionId,
                            aggregate.OperatingDays, aggregate.DepartureTimes);

                        long tripSequence = GetLongScalar(connection, transaction,
                            NextTripSequenceSql("WITH (UPDLOCK, HOLDLOCK)"));
                        int generated = 0;
                        foreach (ScheduleOccurrence occurrence in aggregate.Occurrences)
                        {
                            ExistingOccurrence collision = FindOccurrence(
                                connection, transaction, aggregate.RouteId,
                                occurrence.ServiceDate, occurrence.ScheduledDepartureTime);
                            if (collision != null)
                            {
                                if (collision.RouteScheduleId == aggregate.RouteScheduleId &&
                                    collision.RequiresReview)
                                    continue;
                                throw new SchedulingConflictException(
                                    "Another Schedule already provides this Route at one or more revised departure times.");
                            }

                            InsertTrip(connection, transaction,
                                IdentifierCodePolicy.FormatTripCode(tripSequence++),
                                newVersionId, aggregate.RouteId, occurrence, actorUserAccountId);
                            generated++;
                        }

                        UpdateSchedule(connection, transaction, aggregate.RouteScheduleId, actorUserAccountId);
                        string scheduleCode = GetScheduleCode(
                            connection, transaction, aggregate.RouteScheduleId);
                        DateTime occurredUtc = DateTime.UtcNow;
                        SqlAuditWriter.Write(
                            connection, transaction, actorUserAccountId,
                            "ScheduleChanged", "RouteSchedule",
                            aggregate.RouteScheduleId.ToString(CultureInfo.InvariantCulture),
                            string.Format(CultureInfo.InvariantCulture,
                                "ScheduleCode={0};Version={1};Cutover={2:yyyy-MM-dd};Replaced={3};Protected={4}",
                                scheduleCode, aggregate.NewVersionNumber,
                                aggregate.ChangeEffectiveDate, deleted, protectedCount),
                            null, occurredUtc);
                        WriteTripsGeneratedAudit(connection, transaction, actorUserAccountId,
                            newVersionId, scheduleCode, generated,
                            aggregate.Occurrences, occurredUtc);

                        transaction.Commit();
                        return new ScheduleChangeResult
                        {
                            RouteScheduleId = aggregate.RouteScheduleId,
                            VersionNumber = aggregate.NewVersionNumber,
                            ReplacedTripCount = deleted,
                            GeneratedTripCount = generated,
                            ProtectedTripCount = protectedCount
                        };
                    }
                }
            }
            catch (SqlException exception)
            {
                if (exception.Number == 2601 || exception.Number == 2627)
                    throw new SchedulingConflictException(
                        "Another Schedule already provides this Route at one or more revised departure times.");
                throw;
            }
        }

        private static long InsertSchedule(SqlConnection connection, SqlTransaction transaction,
            string code, long routeId, long actorId)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
INSERT dbo.RouteSchedules (ScheduleCode, RouteId, IsActive, CreatedByUserAccountId, UpdatedByUserAccountId)
OUTPUT INSERTED.RouteScheduleId
VALUES (@Code, @RouteId, 1, @ActorId, @ActorId);"))
            {
                AddNVarChar(command, "@Code", 20, code);
                AddBigInt(command, "@RouteId", routeId);
                AddBigInt(command, "@ActorId", actorId);
                return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private static long InsertVersion(SqlConnection connection, SqlTransaction transaction,
            long scheduleId, long routeId, int version, DateTime start, DateTime end,
            int? categoryId, int? capacity, long actorId)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
INSERT dbo.RouteScheduleVersions
(RouteScheduleId, RouteId, VersionNumber, EffectiveStartDate, EffectiveEndDate,
 PreferredBusCategoryId, ExpectedCapacity, CreatedByUserAccountId, UpdatedByUserAccountId)
OUTPUT INSERTED.RouteScheduleVersionId
VALUES
(@ScheduleId, @RouteId, @Version, @Start, @End, @CategoryId, @Capacity, @ActorId, @ActorId);"))
            {
                AddBigInt(command, "@ScheduleId", scheduleId);
                AddBigInt(command, "@RouteId", routeId);
                AddInt(command, "@Version", version);
                AddDate(command, "@Start", start);
                AddDate(command, "@End", end);
                AddNullableInt(command, "@CategoryId", categoryId);
                AddNullableInt(command, "@Capacity", capacity);
                AddBigInt(command, "@ActorId", actorId);
                return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private static void InsertPattern(SqlConnection connection, SqlTransaction transaction,
            long versionId, IEnumerable<OperatingDay> days, IEnumerable<TimeSpan> times)
        {
            foreach (OperatingDay day in days)
            {
                using (SqlCommand command = CreateCommand(connection, transaction,
                    "INSERT dbo.ScheduleOperatingDays (RouteScheduleVersionId, DayOfWeek) VALUES (@VersionId, @Day);"))
                {
                    AddBigInt(command, "@VersionId", versionId);
                    SqlParameter parameter = command.Parameters.Add("@Day", SqlDbType.TinyInt);
                    parameter.Value = (byte)day;
                    command.ExecuteNonQuery();
                }
            }
            foreach (TimeSpan time in times)
            {
                using (SqlCommand command = CreateCommand(connection, transaction,
                    "INSERT dbo.ScheduleDepartureTimes (RouteScheduleVersionId, DepartureTime) VALUES (@VersionId, @Time);"))
                {
                    AddBigInt(command, "@VersionId", versionId);
                    SqlParameter parameter = command.Parameters.Add("@Time", SqlDbType.Time);
                    parameter.Value = time;
                    command.ExecuteNonQuery();
                }
            }
        }

        private static void InsertTrip(SqlConnection connection, SqlTransaction transaction,
            string code, long versionId, long routeId, ScheduleOccurrence occurrence, long actorId)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
INSERT dbo.Trips
(TripCode, RouteScheduleVersionId, RouteId, ServiceDate, ScheduledDepartureTime,
 ExpectedFinishLocal, EstimatedDurationMinutesSnapshot, TripStatus, RequiresReview,
 CreatedByUserAccountId, UpdatedByUserAccountId)
VALUES
(@Code, @VersionId, @RouteId, @ServiceDate, @DepartureTime,
 @ExpectedFinish, @Duration, N'Unassigned', 0, @ActorId, @ActorId);"))
            {
                AddNVarChar(command, "@Code", 20, code);
                AddBigInt(command, "@VersionId", versionId);
                AddBigInt(command, "@RouteId", routeId);
                AddDate(command, "@ServiceDate", occurrence.ServiceDate);
                SqlParameter time = command.Parameters.Add("@DepartureTime", SqlDbType.Time);
                time.Value = occurrence.ScheduledDepartureTime;
                SqlParameter finish = command.Parameters.Add("@ExpectedFinish", SqlDbType.DateTime2);
                finish.Scale = 0;
                finish.Value = occurrence.ExpectedFinishLocal;
                AddInt(command, "@Duration", occurrence.EstimatedDurationMinutesSnapshot);
                AddBigInt(command, "@ActorId", actorId);
                command.ExecuteNonQuery();
            }
        }

        private static void ValidateRoute(SqlConnection connection, SqlTransaction transaction,
            long routeId, byte[] expectedRowVersion)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
SELECT COUNT(*) FROM dbo.Routes WITH (UPDLOCK, HOLDLOCK)
WHERE RouteId=@RouteId AND IsActive=1 AND RowVersion=@RowVersion;"))
            {
                AddBigInt(command, "@RouteId", routeId);
                SqlParameter rowVersion = command.Parameters.Add("@RowVersion", SqlDbType.Binary, 8);
                rowVersion.Value = (object)expectedRowVersion ?? DBNull.Value;
                if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
                    throw new SchedulingReferenceException(
                        "The selected Route is no longer active or its duration changed. Review the schedule again.");
            }
        }

        private static void ValidateCategory(SqlConnection connection, SqlTransaction transaction,
            int? categoryId)
        {
            if (!categoryId.HasValue) return;
            using (SqlCommand command = CreateCommand(connection, transaction, @"
SELECT COUNT(*) FROM dbo.BusCategories WITH (UPDLOCK, HOLDLOCK)
WHERE BusCategoryId=@CategoryId AND IsActive=1;"))
            {
                AddInt(command, "@CategoryId", categoryId.Value);
                if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
                    throw new SchedulingReferenceException("The preferred bus category is no longer active.");
            }
        }

        private static void ValidateCurrentVersion(SqlConnection connection, SqlTransaction transaction,
            ScheduleChangeAggregate aggregate)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
SELECT COUNT(*)
FROM dbo.RouteSchedules AS rs WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.RouteScheduleVersions AS rsv WITH (UPDLOCK, HOLDLOCK)
    ON rsv.RouteScheduleId=rs.RouteScheduleId
INNER JOIN dbo.Routes AS r WITH (UPDLOCK, HOLDLOCK)
    ON r.RouteId=rs.RouteId
WHERE rs.RouteScheduleId=@ScheduleId AND rs.RouteId=@RouteId AND rs.IsActive=1
  AND r.IsActive=1 AND r.EstimatedDurationMinutes=@Duration
  AND rs.RowVersion=@ScheduleRowVersion
  AND rsv.RouteScheduleVersionId=@VersionId
  AND rsv.RowVersion=@VersionRowVersion
  AND rsv.SupersededFromDate IS NULL;"))
            {
                AddBigInt(command, "@ScheduleId", aggregate.RouteScheduleId);
                AddBigInt(command, "@RouteId", aggregate.RouteId);
                AddInt(command, "@Duration", aggregate.EstimatedDurationMinutes);
                AddBigInt(command, "@VersionId", aggregate.CurrentVersionId);
                SqlParameter scheduleVersion = command.Parameters.Add("@ScheduleRowVersion", SqlDbType.Binary, 8);
                scheduleVersion.Value = (object)aggregate.ScheduleRowVersion ?? DBNull.Value;
                SqlParameter versionVersion = command.Parameters.Add("@VersionRowVersion", SqlDbType.Binary, 8);
                versionVersion.Value = (object)aggregate.VersionRowVersion ?? DBNull.Value;
                if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
                    throw new SchedulingConcurrencyException(
                        "The schedule changed after the preview. Reload it before continuing.");
            }
        }

        private static IList<ExistingScheduleTrip> ReadExistingTrips(SqlConnection connection,
            SqlTransaction transaction, long scheduleId, DateTime fromDate, bool lockRows)
        {
            List<ExistingScheduleTrip> items = new List<ExistingScheduleTrip>();
            string hint = lockRows ? " WITH (UPDLOCK, HOLDLOCK)" : string.Empty;
            using (SqlCommand command = CreateCommand(connection, transaction, @"
SELECT t.TripId, t.RouteScheduleVersionId, t.ServiceDate, t.ScheduledDepartureTime,
       t.TripStatus, t.RequiresReview, t.OperationallyTouchedUtc
FROM dbo.Trips AS t" + hint + @"
INNER JOIN dbo.RouteScheduleVersions AS rsv
    ON rsv.RouteScheduleVersionId=t.RouteScheduleVersionId
WHERE rsv.RouteScheduleId=@ScheduleId AND t.ServiceDate>=@FromDate
ORDER BY t.ServiceDate, t.ScheduledDepartureTime;"))
            {
                AddBigInt(command, "@ScheduleId", scheduleId);
                AddDate(command, "@FromDate", fromDate);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new ExistingScheduleTrip
                        {
                            TripId = reader.GetInt64(0),
                            RouteScheduleVersionId = reader.GetInt64(1),
                            ServiceDate = reader.GetDateTime(2),
                            ScheduledDepartureTime = reader.GetTimeSpan(3),
                            Status = (TripStatus)Enum.Parse(typeof(TripStatus), reader.GetString(4), false),
                            RequiresReview = reader.GetBoolean(5),
                            OperationallyTouchedUtc = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6)
                        });
                    }
                }
            }
            return items;
        }

        private static void MarkProtectedTrips(SqlConnection connection, SqlTransaction transaction,
            long scheduleId, DateTime fromDate, long actorId)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
UPDATE t
SET RequiresReview=1, UpdatedUtc=SYSUTCDATETIME(), UpdatedByUserAccountId=@ActorId
FROM dbo.Trips AS t
INNER JOIN dbo.RouteScheduleVersions AS rsv ON rsv.RouteScheduleVersionId=t.RouteScheduleVersionId
WHERE rsv.RouteScheduleId=@ScheduleId AND t.ServiceDate>=@FromDate
  AND NOT (t.TripStatus=N'Unassigned' AND t.RequiresReview=0 AND t.OperationallyTouchedUtc IS NULL);"))
            {
                AddBigInt(command, "@ActorId", actorId);
                AddBigInt(command, "@ScheduleId", scheduleId);
                AddDate(command, "@FromDate", fromDate);
                command.ExecuteNonQuery();
            }
        }

        private static int DeleteUntouchedTrips(SqlConnection connection, SqlTransaction transaction,
            long scheduleId, DateTime fromDate)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
DELETE t
FROM dbo.Trips AS t
INNER JOIN dbo.RouteScheduleVersions AS rsv ON rsv.RouteScheduleVersionId=t.RouteScheduleVersionId
WHERE rsv.RouteScheduleId=@ScheduleId AND t.ServiceDate>=@FromDate
  AND t.TripStatus=N'Unassigned' AND t.RequiresReview=0 AND t.OperationallyTouchedUtc IS NULL;"))
            {
                AddBigInt(command, "@ScheduleId", scheduleId);
                AddDate(command, "@FromDate", fromDate);
                return command.ExecuteNonQuery();
            }
        }

        private static void SupersedeVersion(SqlConnection connection, SqlTransaction transaction,
            ScheduleChangeAggregate aggregate, long actorId)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
UPDATE dbo.RouteScheduleVersions
SET SupersededFromDate=@Cutover, UpdatedUtc=SYSUTCDATETIME(), UpdatedByUserAccountId=@ActorId
WHERE RouteScheduleVersionId=@VersionId AND SupersededFromDate IS NULL;
IF @@ROWCOUNT <> 1 THROW 51010, 'The current schedule version could not be superseded.', 1;"))
            {
                AddDate(command, "@Cutover", aggregate.ChangeEffectiveDate);
                AddBigInt(command, "@ActorId", actorId);
                AddBigInt(command, "@VersionId", aggregate.CurrentVersionId);
                command.ExecuteNonQuery();
            }
        }

        private static void UpdateSchedule(SqlConnection connection, SqlTransaction transaction,
            long scheduleId, long actorId)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
UPDATE dbo.RouteSchedules
SET UpdatedUtc=SYSUTCDATETIME(), UpdatedByUserAccountId=@ActorId
WHERE RouteScheduleId=@ScheduleId;"))
            {
                AddBigInt(command, "@ActorId", actorId);
                AddBigInt(command, "@ScheduleId", scheduleId);
                command.ExecuteNonQuery();
            }
        }

        private static ExistingOccurrence FindOccurrence(SqlConnection connection, SqlTransaction transaction,
            long routeId, DateTime date, TimeSpan time)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
SELECT t.TripId, t.RouteScheduleVersionId, rsv.RouteScheduleId, t.RequiresReview
FROM dbo.Trips AS t WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.RouteScheduleVersions AS rsv ON rsv.RouteScheduleVersionId=t.RouteScheduleVersionId
WHERE t.RouteId=@RouteId AND t.ServiceDate=@Date AND t.ScheduledDepartureTime=@Time;"))
            {
                AddBigInt(command, "@RouteId", routeId);
                AddDate(command, "@Date", date);
                SqlParameter parameter = command.Parameters.Add("@Time", SqlDbType.Time);
                parameter.Value = time;
                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    return reader.Read()
                        ? new ExistingOccurrence
                        {
                            TripId = reader.GetInt64(0),
                            RouteScheduleVersionId = reader.GetInt64(1),
                            RouteScheduleId = reader.GetInt64(2),
                            RequiresReview = reader.GetBoolean(3)
                        }
                        : null;
                }
            }
        }

        private static string GetScheduleCode(SqlConnection connection, SqlTransaction transaction, long scheduleId)
        {
            using (SqlCommand command = CreateCommand(connection, transaction,
                "SELECT ScheduleCode FROM dbo.RouteSchedules WHERE RouteScheduleId=@ScheduleId;"))
            {
                AddBigInt(command, "@ScheduleId", scheduleId);
                return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private static void WriteTripsGeneratedAudit(SqlConnection connection, SqlTransaction transaction,
            long actorId, long versionId, string scheduleCode, int count,
            IList<ScheduleOccurrence> occurrences, DateTime occurredUtc)
        {
            string range = count == 0
                ? "None"
                : string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd}..{1:yyyy-MM-dd}",
                    occurrences.First().ServiceDate, occurrences.Last().ServiceDate);
            SqlAuditWriter.Write(
                connection, transaction, actorId, "TripsGenerated", "RouteScheduleVersion",
                versionId.ToString(CultureInfo.InvariantCulture),
                string.Format(CultureInfo.InvariantCulture,
                    "ScheduleCode={0};TripCount={1};ServiceRange={2}", scheduleCode, count, range),
                null, occurredUtc);
        }

        private static IList<LookupOption> ReadBusCategories(SqlConnection connection, SqlTransaction transaction)
        {
            List<LookupOption> items = new List<LookupOption>();
            using (SqlCommand command = CreateCommand(connection, transaction,
                "SELECT BusCategoryId, CategoryCode, DisplayName FROM dbo.BusCategories WHERE IsActive=1 ORDER BY DisplayName, BusCategoryId;"))
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                    items.Add(new LookupOption { Id=reader.GetInt32(0), Code=reader.GetString(1), DisplayName=reader.GetString(2) });
            }
            return items;
        }

        private static IList<OperatingDay> ReadDays(SqlConnection connection, SqlTransaction transaction, long versionId)
        {
            List<OperatingDay> items = new List<OperatingDay>();
            using (SqlCommand command = CreateCommand(connection, transaction,
                "SELECT DayOfWeek FROM dbo.ScheduleOperatingDays WHERE RouteScheduleVersionId=@VersionId ORDER BY DayOfWeek;"))
            {
                AddBigInt(command, "@VersionId", versionId);
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read()) items.Add((OperatingDay)reader.GetByte(0));
            }
            return items;
        }

        private static IList<TimeSpan> ReadTimes(SqlConnection connection, SqlTransaction transaction, long versionId)
        {
            List<TimeSpan> items = new List<TimeSpan>();
            using (SqlCommand command = CreateCommand(connection, transaction,
                "SELECT DepartureTime FROM dbo.ScheduleDepartureTimes WHERE RouteScheduleVersionId=@VersionId ORDER BY DepartureTime;"))
            {
                AddBigInt(command, "@VersionId", versionId);
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read()) items.Add(reader.GetTimeSpan(0));
            }
            return items;
        }

        private static void AcquireReferenceCodeLock(SqlConnection connection, SqlTransaction transaction)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
DECLARE @Result INT;
EXEC @Result=sys.sp_getapplock @Resource=@Resource, @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
SELECT @Result;"))
            {
                AddNVarChar(command, "@Resource", 255, ReferenceCodeLockResource);
                int result = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                if (result < 0) throw new DataException("The scheduling reference-code lock could not be acquired.");
            }
        }

        private static string NextScheduleSequenceSql(string hint)
        {
            return "SELECT ISNULL(MAX(TRY_CONVERT(INT, SUBSTRING(ScheduleCode,5,16))),0)+1 FROM dbo.RouteSchedules " + hint +
                " WHERE ScheduleCode LIKE N'FM-S%' AND LEN(ScheduleCode)>4 AND SUBSTRING(ScheduleCode,5,16) NOT LIKE N'%[^0-9]%';";
        }

        private static string NextTripSequenceSql(string hint)
        {
            return "SELECT ISNULL(MAX(TRY_CONVERT(BIGINT, SUBSTRING(TripCode,4,17))),0)+1 FROM dbo.Trips " + hint +
                " WHERE TripCode LIKE N'TR-%' AND LEN(TripCode)>3 AND SUBSTRING(TripCode,4,17) NOT LIKE N'%[^0-9]%';";
        }

        private static int GetInt64ScalarAsInt(SqlConnection connection, SqlTransaction transaction, string sql)
        {
            return checked((int)GetLongScalar(connection, transaction, sql));
        }

        private static long GetLongScalar(SqlConnection connection, SqlTransaction transaction, string sql)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, sql))
                return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        private static SqlCommand CreateCommand(SqlConnection connection, SqlTransaction transaction, string sql)
        {
            SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandType = CommandType.Text;
            command.CommandText = sql;
            return command;
        }

        private static void AddBigInt(SqlCommand command, string name, long value)
        {
            command.Parameters.Add(name, SqlDbType.BigInt).Value = value;
        }

        private static void AddNullableBigInt(SqlCommand command, string name, long? value)
        {
            command.Parameters.Add(name, SqlDbType.BigInt).Value = value.HasValue ? (object)value.Value : DBNull.Value;
        }

        private static void AddInt(SqlCommand command, string name, int value)
        {
            command.Parameters.Add(name, SqlDbType.Int).Value = value;
        }

        private static void AddNullableInt(SqlCommand command, string name, int? value)
        {
            command.Parameters.Add(name, SqlDbType.Int).Value = value.HasValue ? (object)value.Value : DBNull.Value;
        }

        private static void AddDate(SqlCommand command, string name, DateTime value)
        {
            command.Parameters.Add(name, SqlDbType.Date).Value = value.Date;
        }

        private static void AddNullableDate(SqlCommand command, string name, DateTime? value)
        {
            command.Parameters.Add(name, SqlDbType.Date).Value = value.HasValue ? (object)value.Value.Date : DBNull.Value;
        }

        private static void AddNVarChar(SqlCommand command, string name, int length, string value)
        {
            command.Parameters.Add(name, SqlDbType.NVarChar, length).Value = value ?? string.Empty;
        }

        private static void AddNullableNVarChar(SqlCommand command, string name, int length, string value)
        {
            command.Parameters.Add(name, SqlDbType.NVarChar, length).Value = value == null ? (object)DBNull.Value : value;
        }

        private static string EscapeLike(string value)
        {
            return (value ?? string.Empty).Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
        }

        private static ScheduleStatus ParseScheduleStatus(string value)
        {
            return (ScheduleStatus)Enum.Parse(typeof(ScheduleStatus), value, false);
        }

        private static ScheduleStatus DeriveStatus(bool active, DateTime start, DateTime end, DateTime today)
        {
            if (!active) return ScheduleStatus.Inactive;
            if (today < start) return ScheduleStatus.Upcoming;
            if (today > end) return ScheduleStatus.Ended;
            return ScheduleStatus.Active;
        }

        private static string FormatDays(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return string.Empty;
            byte[] values = csv.Split(',').Select(item => byte.Parse(item, CultureInfo.InvariantCulture)).ToArray();
            HashSet<byte> set = new HashSet<byte>(values);
            if (set.SetEquals(new byte[] { 1, 2, 3, 4, 5 })) return "Mon-Fri";
            if (set.SetEquals(new byte[] { 0, 1, 2, 3, 4, 5, 6 })) return "Daily";
            string[] names = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            return string.Join(", ", values.OrderBy(item => item == 0 ? 7 : item).Select(item => names[item]));
        }

        private sealed class ExistingOccurrence
        {
            public long TripId { get; set; }
            public long RouteScheduleVersionId { get; set; }
            public long RouteScheduleId { get; set; }
            public bool RequiresReview { get; set; }
        }
    }
}
