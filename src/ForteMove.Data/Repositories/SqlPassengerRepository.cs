using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Identifiers;
using ForteMove.Business.Services;
using ForteMove.Data.Internal;
using ForteMove.Models.Drivers;
using ForteMove.Models.Passengers;
using ForteMove.Models.Scheduling;

namespace ForteMove.Data.Repositories
{
    public sealed class SqlPassengerRepository : IPassengerRepository
    {
        private readonly string connectionString;

        private const string CandidateSelect = @"
SELECT t.TripId,t.TripCode,t.RouteId,r.RouteCode,r.RouteName,
       origin_stop.StopName,destination_stop.StopName,t.ServiceDate,t.ScheduledDepartureTime,
       t.ExpectedFinishLocal,t.TripStatus,r.DefaultFare,r.IsActive,route_schedule.IsActive,
       t.RequiresReview,
       CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.TripExecutions execution WHERE execution.TripId=t.TripId) THEN 1 ELSE 0 END),
       open_delay.EstimatedDelayMinutes,current_assignment.TripAssignmentId,
       current_assignment.DriverProfileId,current_assignment.BusId,
       ISNULL(driver_account.IsActive,0),ISNULL(driver_role.IsActive,0),staff.EmploymentStatus,
       driver.AvailabilityStatus,driver.DateOfBirth,driver.LicenceCode,
       driver.LicenceExpiryDate,driver.PrdpExpiryDate,bus.BaseOperationalState,
       bus.GrossVehicleMassKg,ISNULL(bus.PassengerCapacity,0),bus.LicenceExpiryDate,
       bus.RoadworthyExpiryDate,bus.InsuranceExpiryDate,
       CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.TripCannotProceedReports exception WHERE exception.TripId=t.TripId AND exception.ResolvedUtc IS NULL) THEN 1 ELSE 0 END),
       CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.BusDefectReports defect WHERE defect.BusId=current_assignment.BusId AND defect.Severity=N'Critical' AND defect.DefectStatus<>N'Resolved') THEN 1 ELSE 0 END),
       CONVERT(bit,CASE WHEN current_assignment.TripAssignmentId IS NOT NULL AND
       (
           EXISTS(SELECT 1 FROM dbo.TripExecutions active_execution
                  WHERE active_execution.TripId<>t.TripId AND active_execution.ActualCompletionUtc IS NULL
                    AND (active_execution.DriverProfileId=current_assignment.DriverProfileId OR active_execution.BusId=current_assignment.BusId))
           OR EXISTS
           (
               SELECT 1 FROM dbo.TripAssignments other_assignment
               INNER JOIN dbo.Trips other_trip ON other_trip.TripId=other_assignment.TripId
               WHERE other_assignment.IsCurrent=1 AND other_assignment.TripId<>t.TripId
                 AND other_trip.TripStatus NOT IN(N'Completed',N'Cancelled')
                 AND (other_assignment.DriverProfileId=current_assignment.DriverProfileId OR other_assignment.BusId=current_assignment.BusId)
                 AND DATEADD(SECOND,DATEDIFF(SECOND,CONVERT(time(0),'00:00'),other_trip.ScheduledDepartureTime),CONVERT(datetime2(0),other_trip.ServiceDate)) < DATEADD(minute,15,t.ExpectedFinishLocal)
                 AND DATEADD(minute,15,other_trip.ExpectedFinishLocal) > DATEADD(SECOND,DATEDIFF(SECOND,CONVERT(time(0),'00:00'),t.ScheduledDepartureTime),CONVERT(datetime2(0),t.ServiceDate))
           )
       ) THEN 1 ELSE 0 END),
       ISNULL(ticket_count.PurchasedTicketCount,0),t.RowVersion,r.RowVersion,
       schedule_version.ExpectedCapacity
FROM dbo.Trips AS t
INNER JOIN dbo.Routes AS r ON r.RouteId=t.RouteId
INNER JOIN dbo.RouteScheduleVersions AS schedule_version ON schedule_version.RouteScheduleVersionId=t.RouteScheduleVersionId
INNER JOIN dbo.RouteSchedules AS route_schedule ON route_schedule.RouteScheduleId=schedule_version.RouteScheduleId
OUTER APPLY(SELECT TOP(1) stop.StopName FROM dbo.RouteStops route_stop INNER JOIN dbo.Stops stop ON stop.StopId=route_stop.StopId WHERE route_stop.RouteId=t.RouteId ORDER BY route_stop.StopOrder) AS origin_stop
OUTER APPLY(SELECT TOP(1) stop.StopName FROM dbo.RouteStops route_stop INNER JOIN dbo.Stops stop ON stop.StopId=route_stop.StopId WHERE route_stop.RouteId=t.RouteId ORDER BY route_stop.StopOrder DESC) AS destination_stop
OUTER APPLY(SELECT TOP(1) assignment.TripAssignmentId,assignment.DriverProfileId,assignment.BusId FROM dbo.TripAssignments assignment WHERE assignment.TripId=t.TripId AND assignment.IsCurrent=1) AS current_assignment
LEFT JOIN dbo.DriverProfiles AS driver ON driver.DriverProfileId=current_assignment.DriverProfileId
LEFT JOIN dbo.StaffProfiles AS staff ON staff.StaffProfileId=driver.StaffProfileId
LEFT JOIN dbo.UserAccounts AS driver_account ON driver_account.UserAccountId=staff.UserAccountId
LEFT JOIN dbo.Roles AS driver_role ON driver_role.RoleId=driver_account.RoleId AND driver_role.RoleCode=N'Driver'
LEFT JOIN dbo.Buses AS bus ON bus.BusId=current_assignment.BusId
OUTER APPLY(SELECT TOP(1) delay.EstimatedDelayMinutes FROM dbo.TripDelayEvents delay WHERE delay.TripId=t.TripId AND delay.EndedUtc IS NULL ORDER BY delay.ReportedUtc DESC) AS open_delay
OUTER APPLY(SELECT COUNT_BIG(*) PurchasedTicketCount FROM dbo.Tickets ticket WHERE ticket.TripId=t.TripId AND ticket.TicketStatus=N'Purchased') AS ticket_count";

        public SqlPassengerRepository(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("A SQL Server connection string is required.", "connectionString");
            this.connectionString = connectionString;
        }

        public PassengerRegistrationResult RegisterPassenger(PassengerRegistrationAggregate aggregate, DateTime utcNow)
        {
            if (aggregate == null) throw new ArgumentNullException("aggregate");
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                    {
                        int roleId;
                        using (SqlCommand command = CreateCommand(connection, transaction, "SELECT RoleId FROM dbo.Roles WITH(UPDLOCK,HOLDLOCK) WHERE RoleCode=N'Passenger' AND IsActive=1;"))
                        {
                            object value = command.ExecuteScalar();
                            if (value == null || value == DBNull.Value) throw new PassengerPersistenceException("Passenger registration is currently unavailable.", string.Empty, null);
                            roleId = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                        }

                        long userId;
                        using (SqlCommand command = CreateCommand(connection, transaction, @"
INSERT dbo.UserAccounts(RoleId,Email,NormalizedEmail,PasswordAlgorithm,PasswordHash,PasswordSalt,
                         PasswordIterations,IsActive,MustChangePassword,FailedLoginCount,CreatedUtc,UpdatedUtc)
OUTPUT inserted.UserAccountId
VALUES(@RoleId,@Email,@NormalizedEmail,@Algorithm,@Hash,@Salt,@Iterations,1,0,0,@Utc,@Utc);"))
                        {
                            command.Parameters.Add("@RoleId", SqlDbType.Int).Value = roleId;
                            AddString(command, "@Email", 254, aggregate.Email);
                            AddString(command, "@NormalizedEmail", 254, aggregate.NormalizedEmail);
                            AddString(command, "@Algorithm", 50, aggregate.PasswordHash.Algorithm);
                            command.Parameters.Add("@Hash", SqlDbType.VarBinary, 64).Value = aggregate.PasswordHash.Hash;
                            command.Parameters.Add("@Salt", SqlDbType.VarBinary, 64).Value = aggregate.PasswordHash.Salt;
                            command.Parameters.Add("@Iterations", SqlDbType.Int).Value = aggregate.PasswordHash.Iterations;
                            AddUtc(command, "@Utc", utcNow);
                            userId = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                        }

                        long profileId;
                        using (SqlCommand command = CreateCommand(connection, transaction, @"
INSERT dbo.PassengerProfiles(UserAccountId,FirstName,LastName,PhoneNumber,CreatedUtc,UpdatedUtc)
OUTPUT inserted.PassengerProfileId VALUES(@UserId,@FirstName,@LastName,@Phone,@Utc,@Utc);"))
                        {
                            command.Parameters.Add("@UserId", SqlDbType.BigInt).Value = userId;
                            AddString(command, "@FirstName", 100, aggregate.FirstName);
                            AddString(command, "@LastName", 100, aggregate.LastName);
                            AddNullableString(command, "@Phone", 30, aggregate.PhoneNumber);
                            AddUtc(command, "@Utc", utcNow);
                            profileId = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                        }

                        long walletId;
                        using (SqlCommand command = CreateCommand(connection, transaction, @"
INSERT dbo.PassengerWallets(PassengerProfileId,CurrentBalance,CreatedUtc,UpdatedUtc)
OUTPUT inserted.PassengerWalletId VALUES(@ProfileId,0,@Utc,@Utc);"))
                        {
                            command.Parameters.Add("@ProfileId", SqlDbType.BigInt).Value = profileId;
                            AddUtc(command, "@Utc", utcNow);
                            walletId = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                        }

                        SqlAuditWriter.Write(connection, transaction, userId, "PassengerRegistered", "PassengerProfile", profileId.ToString(CultureInfo.InvariantCulture), null, aggregate.ClientIpAddress, utcNow);
                        transaction.Commit();
                        return new PassengerRegistrationResult { UserAccountId = userId, PassengerProfileId = profileId, PassengerWalletId = walletId };
                    }
                }
            }
            catch (PassengerPersistenceException) { throw; }
            catch (SqlException exception)
            {
                if (exception.Number == 2601 || exception.Number == 2627)
                    throw new PassengerPersistenceException("An account with this email address already exists.", "Email", exception);
                throw new PassengerPersistenceException("The Passenger account could not be created. Nothing was saved.", string.Empty, exception);
            }
        }

        public PassengerAccountDetails GetAccount(long userAccountId)
        {
            const string sql = @"
SELECT p.PassengerProfileId,p.UserAccountId,p.FirstName,p.LastName,u.Email,p.PhoneNumber,u.IsActive,p.RowVersion
FROM dbo.PassengerProfiles p INNER JOIN dbo.UserAccounts u ON u.UserAccountId=p.UserAccountId
INNER JOIN dbo.Roles r ON r.RoleId=u.RoleId AND r.RoleCode=N'Passenger' AND r.IsActive=1
WHERE p.UserAccountId=@UserId;";
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@UserId", SqlDbType.BigInt).Value = userAccountId; connection.Open();
                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read()) return null;
                    return new PassengerAccountDetails
                    {
                        PassengerProfileId = reader.GetInt64(0), UserAccountId = reader.GetInt64(1), FirstName = reader.GetString(2), LastName = reader.GetString(3),
                        Email = reader.GetString(4), PhoneNumber = reader.IsDBNull(5) ? null : reader.GetString(5), AccountIsActive = reader.GetBoolean(6), ProfileRowVersion = (byte[])reader.GetValue(7)
                    };
                }
            }
        }

        public PassengerWalletDetails GetWallet(long userAccountId, int recentTransactionCount)
        {
            PassengerWalletDetails result = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(@"
SELECT w.PassengerWalletId,w.CurrentBalance,w.RowVersion
FROM dbo.PassengerWallets w INNER JOIN dbo.PassengerProfiles p ON p.PassengerProfileId=w.PassengerProfileId
INNER JOIN dbo.UserAccounts u ON u.UserAccountId=p.UserAccountId
INNER JOIN dbo.Roles r ON r.RoleId=u.RoleId AND r.RoleCode=N'Passenger' AND r.IsActive=1
WHERE p.UserAccountId=@UserId AND u.IsActive=1;", connection))
                {
                    command.Parameters.Add("@UserId", SqlDbType.BigInt).Value = userAccountId;
                    using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (!reader.Read()) return null;
                        result = new PassengerWalletDetails { PassengerWalletId = reader.GetInt64(0), CurrentBalance = reader.GetDecimal(1), RowVersion = (byte[])reader.GetValue(2) };
                    }
                }
                if (recentTransactionCount > 0)
                {
                    using (SqlCommand command = new SqlCommand(@"
SELECT TOP(@Count) x.WalletTransactionId,x.WalletTransactionCode,x.TransactionType,x.Amount,
       x.BalanceBefore,x.BalanceAfter,x.TicketId,t.TicketCode,r.RouteName,x.OccurredUtc
FROM dbo.WalletTransactions x
LEFT JOIN dbo.Tickets t ON t.TicketId=x.TicketId
LEFT JOIN dbo.Trips trip ON trip.TripId=t.TripId
LEFT JOIN dbo.Routes r ON r.RouteId=trip.RouteId
WHERE x.PassengerWalletId=@WalletId
ORDER BY x.OccurredUtc DESC,x.WalletTransactionId DESC;", connection))
                    {
                        command.Parameters.Add("@Count", SqlDbType.Int).Value = recentTransactionCount;
                        command.Parameters.Add("@WalletId", SqlDbType.BigInt).Value = result.PassengerWalletId;
                        using (SqlDataReader reader = command.ExecuteReader()) while (reader.Read()) result.RecentTransactions.Add(ReadWalletTransaction(reader));
                    }
                }
            }
            return result;
        }

        public IList<JourneyRouteOption> GetJourneyRoutes()
        {
            List<JourneyRouteOption> rows = new List<JourneyRouteOption>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand("SELECT RouteId,RouteCode,RouteName FROM dbo.Routes WHERE IsActive=1 ORDER BY RouteCode;", connection))
            {
                connection.Open(); using (SqlDataReader reader = command.ExecuteReader()) while (reader.Read()) rows.Add(new JourneyRouteOption { RouteId = reader.GetInt64(0), RouteCode = reader.GetString(1), RouteName = reader.GetString(2) });
            }
            return rows;
        }

        public IList<PassengerJourneyCandidate> FindJourneyCandidates(JourneyQuery query)
        {
            List<PassengerJourneyCandidate> rows = new List<PassengerJourneyCandidate>();
            string sql = CandidateSelect + @"
WHERE t.ServiceDate=@ServiceDate AND (@RouteId IS NULL OR t.RouteId=@RouteId)
  AND (@Search IS NULL OR r.RouteCode LIKE @Search ESCAPE N'~' OR r.RouteName LIKE @Search ESCAPE N'~'
       OR origin_stop.StopName LIKE @Search ESCAPE N'~' OR destination_stop.StopName LIKE @Search ESCAPE N'~')
ORDER BY t.ScheduledDepartureTime,r.RouteCode,t.TripCode;";
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@ServiceDate", SqlDbType.Date).Value = query.ServiceDate.Value.Date;
                command.Parameters.Add("@RouteId", SqlDbType.BigInt).Value = (object)query.RouteId ?? DBNull.Value;
                AddNullableString(command, "@Search", 220, query.Search == null ? null : "%" + EscapeLike(query.Search) + "%");
                connection.Open(); using (SqlDataReader reader = command.ExecuteReader()) while (reader.Read()) rows.Add(ReadCandidate(reader));
            }
            return rows;
        }

        public PassengerJourneyCandidate GetJourneyCandidate(long tripId)
        {
            string sql = CandidateSelect + " WHERE t.TripId=@TripId;";
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@TripId", SqlDbType.BigInt).Value = tripId; connection.Open();
                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow)) return reader.Read() ? ReadCandidate(reader) : null;
            }
        }

        public TopUpResult TopUpWallet(long userAccountId, TopUpRequest request, DateTime utcNow)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open(); using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                    {
                        SqlPassengerCommerce.AcquireLock(connection, transaction);
                        TopUpResult existing = ReadExistingTopUp(connection, transaction, userAccountId, request.OperationToken);
                        if (existing != null) { transaction.Commit(); existing.WasAlreadyProcessed = true; return existing; }

                        long walletId; decimal balance; byte[] rowVersion;
                        using (SqlCommand command = CreateCommand(connection, transaction, @"
SELECT w.PassengerWalletId,w.CurrentBalance,w.RowVersion
FROM dbo.PassengerWallets w WITH(UPDLOCK,HOLDLOCK)
INNER JOIN dbo.PassengerProfiles p ON p.PassengerProfileId=w.PassengerProfileId
INNER JOIN dbo.UserAccounts u WITH(UPDLOCK,HOLDLOCK) ON u.UserAccountId=p.UserAccountId
INNER JOIN dbo.Roles r ON r.RoleId=u.RoleId AND r.RoleCode=N'Passenger' AND r.IsActive=1
WHERE p.UserAccountId=@UserId AND u.IsActive=1;"))
                        {
                            command.Parameters.Add("@UserId", SqlDbType.BigInt).Value = userAccountId;
                            using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                            {
                                if (!reader.Read()) throw new PassengerPersistenceException("The Passenger wallet is unavailable.", string.Empty, null);
                                walletId = reader.GetInt64(0); balance = reader.GetDecimal(1); rowVersion = (byte[])reader.GetValue(2);
                            }
                        }
                        if (!BytesEqual(rowVersion, request.WalletRowVersion) || request.Amount.Value > PassengerService.MaximumSupportedBalance - balance)
                            throw new PassengerPersistenceException("Your wallet balance changed or the top-up exceeds the supported balance. Review it again.", "Preview", null);
                        decimal after = balance + request.Amount.Value;
                        string code = IdentifierCodePolicy.FormatWalletTransactionCode(SqlPassengerCommerce.GetNextWalletTransactionSequence(connection, transaction));
                        using (SqlCommand command = CreateCommand(connection, transaction, @"
UPDATE dbo.PassengerWallets SET CurrentBalance=@After,UpdatedUtc=@Utc
WHERE PassengerWalletId=@WalletId AND RowVersion=@RowVersion;
IF @@ROWCOUNT<>1 THROW 51055,'The Passenger wallet changed.',1;
INSERT dbo.WalletTransactions(WalletTransactionCode,PassengerWalletId,TransactionType,Amount,
 BalanceBefore,BalanceAfter,TicketId,InitiatedByUserAccountId,OperationToken,OccurredUtc)
VALUES(@Code,@WalletId,N'SimulatedTopUp',@Amount,@Before,@After,NULL,@UserId,@Token,@Utc);"))
                        {
                            AddMoney(command, "@After", after); AddUtc(command, "@Utc", utcNow); command.Parameters.Add("@WalletId", SqlDbType.BigInt).Value = walletId;
                            AddTimestamp(command, "@RowVersion", rowVersion); AddString(command, "@Code", 20, code); AddMoney(command, "@Amount", request.Amount.Value);
                            AddMoney(command, "@Before", balance); command.Parameters.Add("@UserId", SqlDbType.BigInt).Value = userAccountId;
                            command.Parameters.Add("@Token", SqlDbType.UniqueIdentifier).Value = request.OperationToken; command.ExecuteNonQuery();
                        }
                        SqlAuditWriter.Write(connection, transaction, userAccountId, "WalletTopUp", "WalletTransaction", code, "Amount=" + request.Amount.Value.ToString("0.00", CultureInfo.InvariantCulture), request.ClientIpAddress, utcNow);
                        transaction.Commit(); return new TopUpResult { WalletTransactionCode = code, BalanceAfter = after };
                    }
                }
            }
            catch (PassengerPersistenceException) { throw; }
            catch (SqlException exception)
            {
                if (exception.Number == 2601 || exception.Number == 2627)
                    throw new PassengerPersistenceException("This top-up was already processed. Refresh your wallet.", "Preview", exception);
                throw new PassengerPersistenceException("The wallet could not be topped up. Nothing was changed.", string.Empty, exception);
            }
        }

        public TicketPurchaseResult PurchaseTicket(long userAccountId, PurchaseTicketRequest request, DateTime operationalNow, DateTime utcNow)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open(); using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                    {
                        AcquireAssignmentLock(connection, transaction); SqlPassengerCommerce.AcquireLock(connection, transaction);
                        TicketPurchaseResult existing = ReadExistingPurchase(connection, transaction, userAccountId, request.OperationToken);
                        if (existing != null) { transaction.Commit(); existing.WasAlreadyProcessed = true; return existing; }

                        long profileId; long walletId; decimal balance; byte[] walletVersion;
                        ReadLockedPassengerWallet(connection, transaction, userAccountId, out profileId, out walletId, out balance, out walletVersion);
                        if (!BytesEqual(walletVersion, request.WalletRowVersion)) throw new PassengerPersistenceException("Your wallet balance changed. Review the purchase again.", "Preview", null);

                        decimal fare; int capacity; int sold;
                        ReadAndValidateLockedSale(connection, transaction, request, operationalNow, out fare, out capacity, out sold);
                        using (SqlCommand command = CreateCommand(connection, transaction, "SELECT COUNT(*) FROM dbo.Tickets WITH(UPDLOCK,HOLDLOCK) WHERE PassengerProfileId=@ProfileId AND TripId=@TripId AND TicketStatus=N'Purchased';"))
                        {
                            command.Parameters.Add("@ProfileId", SqlDbType.BigInt).Value = profileId; command.Parameters.Add("@TripId", SqlDbType.BigInt).Value = request.TripId;
                            if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0) throw new PassengerPersistenceException("You already have a Ticket for this journey.", string.Empty, null);
                        }
                        if (sold >= capacity) throw new PassengerPersistenceException("This service is now fully booked.", string.Empty, null);
                        if (balance < fare)
                        {
                            decimal shortfall = fare - balance;
                            throw new PassengerPersistenceException(
                                "Insufficient wallet balance. Add R" + shortfall.ToString("0.00", CultureInfo.InvariantCulture) + " to purchase this journey.",
                                "Wallet",
                                null);
                        }

                        long ticketSequence = SqlPassengerCommerce.GetNextTicketSequence(connection, transaction);
                        long transactionSequence = SqlPassengerCommerce.GetNextWalletTransactionSequence(connection, transaction);
                        string ticketCode = IdentifierCodePolicy.FormatTicketCode(ticketSequence);
                        string transactionCode = IdentifierCodePolicy.FormatWalletTransactionCode(transactionSequence);
                        decimal after = balance - fare; long ticketId;
                        using (SqlCommand command = CreateCommand(connection, transaction, @"
INSERT dbo.Tickets(TicketCode,PassengerProfileId,TripId,FareAmount,TicketStatus,PurchasedUtc,UpdatedUtc)
OUTPUT inserted.TicketId VALUES(@TicketCode,@ProfileId,@TripId,@Fare,N'Purchased',@Utc,@Utc);"))
                        {
                            AddString(command, "@TicketCode", 20, ticketCode); command.Parameters.Add("@ProfileId", SqlDbType.BigInt).Value = profileId;
                            command.Parameters.Add("@TripId", SqlDbType.BigInt).Value = request.TripId; AddMoney(command, "@Fare", fare); AddUtc(command, "@Utc", utcNow);
                            ticketId = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                        }
                        using (SqlCommand command = CreateCommand(connection, transaction, @"
UPDATE dbo.PassengerWallets SET CurrentBalance=@After,UpdatedUtc=@Utc
WHERE PassengerWalletId=@WalletId AND RowVersion=@WalletRowVersion;
IF @@ROWCOUNT<>1 THROW 51056,'The Passenger wallet changed.',1;
INSERT dbo.WalletTransactions(WalletTransactionCode,PassengerWalletId,TransactionType,Amount,
 BalanceBefore,BalanceAfter,TicketId,InitiatedByUserAccountId,OperationToken,OccurredUtc)
VALUES(@TransactionCode,@WalletId,N'TicketPurchase',@Fare,@Before,@After,@TicketId,@UserId,@Token,@Utc);
UPDATE dbo.Trips
SET OperationallyTouchedUtc=COALESCE(OperationallyTouchedUtc,@Utc),
    OperationallyTouchedByUserAccountId=COALESCE(OperationallyTouchedByUserAccountId,@UserId),
    UpdatedUtc=@Utc,UpdatedByUserAccountId=@UserId
WHERE TripId=@TripId;"))
                        {
                            AddMoney(command, "@After", after); AddUtc(command, "@Utc", utcNow); command.Parameters.Add("@WalletId", SqlDbType.BigInt).Value = walletId;
                            AddTimestamp(command, "@WalletRowVersion", walletVersion); AddString(command, "@TransactionCode", 20, transactionCode);
                            AddMoney(command, "@Fare", fare); AddMoney(command, "@Before", balance); command.Parameters.Add("@TicketId", SqlDbType.BigInt).Value = ticketId;
                            command.Parameters.Add("@UserId", SqlDbType.BigInt).Value = userAccountId; command.Parameters.Add("@Token", SqlDbType.UniqueIdentifier).Value = request.OperationToken;
                            command.Parameters.Add("@TripId", SqlDbType.BigInt).Value = request.TripId; command.ExecuteNonQuery();
                        }
                        SqlAuditWriter.Write(connection, transaction, userAccountId, "TicketPurchased", "Ticket", ticketId.ToString(CultureInfo.InvariantCulture), "TicketCode=" + ticketCode + ";Fare=" + fare.ToString("0.00", CultureInfo.InvariantCulture), request.ClientIpAddress, utcNow);
                        transaction.Commit();
                        return new TicketPurchaseResult { TicketId = ticketId, TicketCode = ticketCode, WalletTransactionCode = transactionCode, FareAmount = fare, WalletBalanceAfter = after };
                    }
                }
            }
            catch (PassengerPersistenceException) { throw; }
            catch (SqlException exception)
            {
                if (exception.Number == 2601 || exception.Number == 2627) throw new PassengerPersistenceException("You already have a Ticket for this journey, or this purchase was already processed.", string.Empty, exception);
                throw new PassengerPersistenceException("The Ticket could not be purchased. No fare was deducted.", string.Empty, exception);
            }
        }

        public TopUpResult GetCompletedTopUp(long userAccountId, Guid operationToken)
        {
            if (userAccountId <= 0 || operationToken == Guid.Empty) return null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                return ReadExistingTopUp(connection, null, userAccountId, operationToken);
            }
        }

        public TicketPurchaseResult GetCompletedPurchase(long userAccountId, Guid operationToken)
        {
            if (userAccountId <= 0 || operationToken == Guid.Empty) return null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                return ReadExistingPurchase(connection, null, userAccountId, operationToken);
            }
        }

        public IList<TicketListItem> GetTickets(long userAccountId, TicketListMode mode, DateTime operationalNow)
        {
            List<TicketListItem> rows = new List<TicketListItem>();
            string sql = TicketSelect + @"
WHERE p.UserAccountId=@UserId AND
 ((@Upcoming=1 AND ticket.TicketStatus=N'Purchased' AND trip.TripStatus<>N'Completed' AND DATEADD(SECOND,DATEDIFF(SECOND,CONVERT(time(0),'00:00'),trip.ScheduledDepartureTime),CONVERT(datetime2(0),trip.ServiceDate))>@Now)
  OR (@Upcoming=0 AND (ticket.TicketStatus=N'Refunded' OR trip.TripStatus=N'Completed' OR DATEADD(SECOND,DATEDIFF(SECOND,CONVERT(time(0),'00:00'),trip.ScheduledDepartureTime),CONVERT(datetime2(0),trip.ServiceDate))<=@Now)))
ORDER BY CASE WHEN @Upcoming=1 THEN trip.ServiceDate END ASC,CASE WHEN @Upcoming=1 THEN trip.ScheduledDepartureTime END ASC,
         CASE WHEN @Upcoming=0 THEN ticket.PurchasedUtc END DESC;";
            using (SqlConnection connection = new SqlConnection(connectionString)) using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@UserId", SqlDbType.BigInt).Value = userAccountId; command.Parameters.Add("@Upcoming", SqlDbType.Bit).Value = mode == TicketListMode.Upcoming;
                AddLocal(command, "@Now", operationalNow); connection.Open(); using (SqlDataReader reader = command.ExecuteReader()) while (reader.Read()) rows.Add(ReadTicket(reader));
            }
            return rows;
        }

        public TicketDetails GetTicketDetails(long userAccountId, long ticketId)
        {
            string sql = TicketSelect + " WHERE p.UserAccountId=@UserId AND ticket.TicketId=@TicketId;";
            using (SqlConnection connection = new SqlConnection(connectionString)) using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@UserId", SqlDbType.BigInt).Value = userAccountId; command.Parameters.Add("@TicketId", SqlDbType.BigInt).Value = ticketId; connection.Open();
                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow)) return reader.Read() ? new TicketDetails { Ticket = ReadTicket(reader), RefundReason = reader.IsDBNull(17) ? null : reader.GetString(17) } : null;
            }
        }

        public PassengerHomeDetails GetHome(long userAccountId, DateTime operationalNow)
        {
            PassengerAccountDetails account = GetAccount(userAccountId); PassengerWalletDetails wallet = GetWallet(userAccountId, 5);
            if (account == null || wallet == null) return null;
            IList<TicketListItem> upcoming = GetTickets(userAccountId, TicketListMode.Upcoming, operationalNow);
            return new PassengerHomeDetails
            {
                DisplayName = (account.FirstName + " " + account.LastName).Trim(), WalletBalance = wallet.CurrentBalance,
                NextTicket = upcoming.Count == 0 ? null : upcoming[0], RecentTransactions = wallet.RecentTransactions
            };
        }

        private const string TicketSelect = @"
SELECT ticket.TicketId,ticket.TicketCode,trip.TripCode,r.RouteCode,r.RouteName,
       origin_stop.StopName,destination_stop.StopName,trip.ServiceDate,trip.ScheduledDepartureTime,
       trip.ExpectedFinishLocal,trip.TripStatus,ticket.TicketStatus,ticket.FareAmount,
       ticket.PurchasedUtc,ticket.RefundedUtc,open_delay.EstimatedDelayMinutes,b.FleetNumber,ticket.RefundReason
FROM dbo.Tickets ticket
INNER JOIN dbo.PassengerProfiles p ON p.PassengerProfileId=ticket.PassengerProfileId
INNER JOIN dbo.Trips trip ON trip.TripId=ticket.TripId
INNER JOIN dbo.Routes r ON r.RouteId=trip.RouteId
OUTER APPLY(SELECT TOP(1) stop.StopName FROM dbo.RouteStops route_stop INNER JOIN dbo.Stops stop ON stop.StopId=route_stop.StopId WHERE route_stop.RouteId=trip.RouteId ORDER BY route_stop.StopOrder) origin_stop
OUTER APPLY(SELECT TOP(1) stop.StopName FROM dbo.RouteStops route_stop INNER JOIN dbo.Stops stop ON stop.StopId=route_stop.StopId WHERE route_stop.RouteId=trip.RouteId ORDER BY route_stop.StopOrder DESC) destination_stop
OUTER APPLY(SELECT TOP(1) assignment.BusId FROM dbo.TripAssignments assignment WHERE assignment.TripId=trip.TripId AND assignment.IsCurrent=1) current_assignment
LEFT JOIN dbo.Buses b ON b.BusId=current_assignment.BusId
OUTER APPLY(SELECT TOP(1) delay.EstimatedDelayMinutes FROM dbo.TripDelayEvents delay WHERE delay.TripId=trip.TripId AND delay.EndedUtc IS NULL ORDER BY delay.ReportedUtc DESC) open_delay";

        private static PassengerJourneyCandidate ReadCandidate(SqlDataReader reader)
        {
            return new PassengerJourneyCandidate
            {
                TripId = reader.GetInt64(0),
                TripCode = reader.GetString(1),
                RouteId = reader.GetInt64(2),
                RouteCode = reader.GetString(3),
                RouteName = reader.GetString(4),
                OriginName = reader.IsDBNull(5) ? null : reader.GetString(5),
                DestinationName = reader.IsDBNull(6) ? null : reader.GetString(6),
                ServiceDate = reader.GetDateTime(7),
                ScheduledDepartureTime = reader.GetTimeSpan(8),
                ExpectedFinishLocal = reader.GetDateTime(9),
                TripStatus = (TripStatus)Enum.Parse(typeof(TripStatus), reader.GetString(10), false),
                DefaultFare = reader.GetDecimal(11),
                RouteIsActive = reader.GetBoolean(12),
                ScheduleIsActive = reader.GetBoolean(13),
                RequiresReview = reader.GetBoolean(14),
                HasExecution = reader.GetBoolean(15),
                EstimatedDelayMinutes = reader.IsDBNull(16) ? (int?)null : reader.GetInt32(16),
                TripAssignmentId = reader.IsDBNull(17) ? (long?)null : reader.GetInt64(17),
                DriverProfileId = reader.IsDBNull(18) ? (long?)null : reader.GetInt64(18),
                BusId = reader.IsDBNull(19) ? (long?)null : reader.GetInt64(19),
                DriverAccountIsActive = reader.GetBoolean(20),
                DriverRoleIsActive = reader.GetBoolean(21),
                DriverEmploymentStatus = reader.IsDBNull(22) ? null : reader.GetString(22),
                DriverAvailability = reader.IsDBNull(23) ? (DriverAvailabilityStatus?)null : (DriverAvailabilityStatus)Enum.Parse(typeof(DriverAvailabilityStatus), reader.GetString(23), false),
                DriverDateOfBirth = reader.IsDBNull(24) ? (DateTime?)null : reader.GetDateTime(24),
                DriverLicenceCode = reader.IsDBNull(25) ? (DriverLicenceCode?)null : (DriverLicenceCode)Enum.Parse(typeof(DriverLicenceCode), reader.GetString(25), false),
                DriverLicenceExpiryDate = reader.IsDBNull(26) ? (DateTime?)null : reader.GetDateTime(26),
                DriverPrdpExpiryDate = reader.IsDBNull(27) ? (DateTime?)null : reader.GetDateTime(27),
                BusOperationalState = reader.IsDBNull(28) ? null : reader.GetString(28),
                BusGrossVehicleMassKg = reader.IsDBNull(29) ? (int?)null : reader.GetInt32(29),
                BusPassengerCapacity = Convert.ToInt32(reader.GetValue(30), CultureInfo.InvariantCulture),
                BusLicenceExpiryDate = reader.IsDBNull(31) ? (DateTime?)null : reader.GetDateTime(31),
                BusRoadworthyExpiryDate = reader.IsDBNull(32) ? (DateTime?)null : reader.GetDateTime(32),
                BusInsuranceExpiryDate = reader.IsDBNull(33) ? (DateTime?)null : reader.GetDateTime(33),
                HasOpenCannotProceed = reader.GetBoolean(34),
                HasUnresolvedCriticalDefect = reader.GetBoolean(35),
                HasOperationalConflict = reader.GetBoolean(36),
                PurchasedTicketCount = Convert.ToInt32(reader.GetInt64(37), CultureInfo.InvariantCulture),
                TripRowVersion = (byte[])reader.GetValue(38),
                RouteRowVersion = (byte[])reader.GetValue(39),
                ScheduleExpectedCapacity = reader.IsDBNull(40) ? (int?)null : reader.GetInt32(40)
            };
        }

        private static WalletTransactionItem ReadWalletTransaction(SqlDataReader reader)
        {
            return new WalletTransactionItem { WalletTransactionId=reader.GetInt64(0),WalletTransactionCode=reader.GetString(1),TransactionType=(WalletTransactionType)Enum.Parse(typeof(WalletTransactionType),reader.GetString(2),false),Amount=reader.GetDecimal(3),BalanceBefore=reader.GetDecimal(4),BalanceAfter=reader.GetDecimal(5),TicketId=reader.IsDBNull(6)?(long?)null:reader.GetInt64(6),TicketCode=reader.IsDBNull(7)?null:reader.GetString(7),RouteName=reader.IsDBNull(8)?null:reader.GetString(8),OccurredUtc=reader.GetDateTime(9) };
        }

        private static TicketListItem ReadTicket(SqlDataReader reader)
        {
            return new TicketListItem { TicketId=reader.GetInt64(0),TicketCode=reader.GetString(1),TripCode=reader.GetString(2),RouteCode=reader.GetString(3),RouteName=reader.GetString(4),OriginName=reader.IsDBNull(5)?null:reader.GetString(5),DestinationName=reader.IsDBNull(6)?null:reader.GetString(6),ServiceDate=reader.GetDateTime(7),ScheduledDepartureTime=reader.GetTimeSpan(8),ExpectedFinishLocal=reader.GetDateTime(9),TripStatus=(TripStatus)Enum.Parse(typeof(TripStatus),reader.GetString(10),false),TicketStatus=(TicketStatus)Enum.Parse(typeof(TicketStatus),reader.GetString(11),false),FareAmount=reader.GetDecimal(12),PurchasedUtc=reader.GetDateTime(13),RefundedUtc=reader.IsDBNull(14)?(DateTime?)null:reader.GetDateTime(14),EstimatedDelayMinutes=reader.IsDBNull(15)?(int?)null:reader.GetInt32(15),FleetNumber=reader.IsDBNull(16)?null:reader.GetString(16) };
        }

        private static TopUpResult ReadExistingTopUp(SqlConnection connection, SqlTransaction transaction, long userId, Guid token)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
SELECT x.WalletTransactionCode,x.BalanceAfter FROM dbo.WalletTransactions x
INNER JOIN dbo.PassengerWallets w ON w.PassengerWalletId=x.PassengerWalletId
INNER JOIN dbo.PassengerProfiles p ON p.PassengerProfileId=w.PassengerProfileId
WHERE p.UserAccountId=@UserId AND x.OperationToken=@Token AND x.TransactionType=N'SimulatedTopUp';"))
            { command.Parameters.Add("@UserId",SqlDbType.BigInt).Value=userId;command.Parameters.Add("@Token",SqlDbType.UniqueIdentifier).Value=token;using(SqlDataReader reader=command.ExecuteReader(CommandBehavior.SingleRow))return reader.Read()?new TopUpResult{WalletTransactionCode=reader.GetString(0),BalanceAfter=reader.GetDecimal(1)}:null; }
        }

        private static TicketPurchaseResult ReadExistingPurchase(SqlConnection connection, SqlTransaction transaction, long userId, Guid token)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
SELECT t.TicketId,t.TicketCode,x.WalletTransactionCode,t.FareAmount,x.BalanceAfter
FROM dbo.WalletTransactions x INNER JOIN dbo.Tickets t ON t.TicketId=x.TicketId
INNER JOIN dbo.PassengerProfiles p ON p.PassengerProfileId=t.PassengerProfileId
WHERE p.UserAccountId=@UserId AND x.OperationToken=@Token AND x.TransactionType=N'TicketPurchase';"))
            { command.Parameters.Add("@UserId",SqlDbType.BigInt).Value=userId;command.Parameters.Add("@Token",SqlDbType.UniqueIdentifier).Value=token;using(SqlDataReader reader=command.ExecuteReader(CommandBehavior.SingleRow))return reader.Read()?new TicketPurchaseResult{TicketId=reader.GetInt64(0),TicketCode=reader.GetString(1),WalletTransactionCode=reader.GetString(2),FareAmount=reader.GetDecimal(3),WalletBalanceAfter=reader.GetDecimal(4)}:null; }
        }

        private static void ReadLockedPassengerWallet(SqlConnection connection, SqlTransaction transaction, long userId, out long profileId, out long walletId, out decimal balance, out byte[] rowVersion)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
SELECT p.PassengerProfileId,w.PassengerWalletId,w.CurrentBalance,w.RowVersion
FROM dbo.PassengerProfiles p WITH(UPDLOCK,HOLDLOCK)
INNER JOIN dbo.UserAccounts u WITH(UPDLOCK,HOLDLOCK) ON u.UserAccountId=p.UserAccountId
INNER JOIN dbo.Roles r ON r.RoleId=u.RoleId AND r.RoleCode=N'Passenger' AND r.IsActive=1
INNER JOIN dbo.PassengerWallets w WITH(UPDLOCK,HOLDLOCK) ON w.PassengerProfileId=p.PassengerProfileId
WHERE p.UserAccountId=@UserId AND u.IsActive=1;"))
            { command.Parameters.Add("@UserId",SqlDbType.BigInt).Value=userId;using(SqlDataReader reader=command.ExecuteReader(CommandBehavior.SingleRow)){if(!reader.Read())throw new PassengerPersistenceException("The Passenger wallet is unavailable.",string.Empty,null);profileId=reader.GetInt64(0);walletId=reader.GetInt64(1);balance=reader.GetDecimal(2);rowVersion=(byte[])reader.GetValue(3);} }
        }

        private static void ReadAndValidateLockedSale(SqlConnection connection, SqlTransaction transaction, PurchaseTicketRequest request, DateTime now, out decimal fare, out int capacity, out int sold)
        {
            const string sql=@"
SELECT r.DefaultFare,b.PassengerCapacity,CONVERT(int,(SELECT COUNT_BIG(*) FROM dbo.Tickets ticket WITH(UPDLOCK,HOLDLOCK) WHERE ticket.TripId=t.TripId AND ticket.TicketStatus=N'Purchased'))
FROM dbo.Trips t WITH(UPDLOCK,HOLDLOCK)
INNER JOIN dbo.Routes r WITH(UPDLOCK,HOLDLOCK) ON r.RouteId=t.RouteId
INNER JOIN dbo.RouteScheduleVersions rsv ON rsv.RouteScheduleVersionId=t.RouteScheduleVersionId
INNER JOIN dbo.RouteSchedules rs ON rs.RouteScheduleId=rsv.RouteScheduleId
INNER JOIN dbo.TripAssignments ta WITH(UPDLOCK,HOLDLOCK) ON ta.TripId=t.TripId AND ta.IsCurrent=1
INNER JOIN dbo.DriverProfiles dp WITH(UPDLOCK,HOLDLOCK) ON dp.DriverProfileId=ta.DriverProfileId
INNER JOIN dbo.StaffProfiles sp WITH(UPDLOCK,HOLDLOCK) ON sp.StaffProfileId=dp.StaffProfileId
INNER JOIN dbo.UserAccounts ua WITH(UPDLOCK,HOLDLOCK) ON ua.UserAccountId=sp.UserAccountId
INNER JOIN dbo.Roles role ON role.RoleId=ua.RoleId AND role.RoleCode=N'Driver'
INNER JOIN dbo.Buses b WITH(UPDLOCK,HOLDLOCK) ON b.BusId=ta.BusId
WHERE t.TripId=@TripId AND t.RowVersion=@TripRv AND r.RowVersion=@RouteRv AND r.DefaultFare=@Fare
  AND r.IsActive=1 AND rs.IsActive=1 AND t.TripStatus IN(N'Scheduled',N'Ready',N'Delayed') AND t.RequiresReview=0
  AND DATEADD(SECOND,DATEDIFF(SECOND,CONVERT(time(0),'00:00'),t.ScheduledDepartureTime),CONVERT(datetime2(0),t.ServiceDate))>@Now
  AND NOT EXISTS(SELECT 1 FROM dbo.TripExecutions execution WHERE execution.TripId=t.TripId)
  AND ua.IsActive=1 AND role.IsActive=1 AND sp.EmploymentStatus=N'Active' AND dp.AvailabilityStatus=N'Available'
  AND DATEADD(year,21,dp.DateOfBirth)<=t.ServiceDate AND dp.LicenceExpiryDate>=CONVERT(date,t.ExpectedFinishLocal) AND dp.PrdpExpiryDate>=CONVERT(date,t.ExpectedFinishLocal)
  AND b.BaseOperationalState=N'Operational' AND b.GrossVehicleMassKg IS NOT NULL
  AND b.LicenceExpiryDate>=CONVERT(date,t.ExpectedFinishLocal) AND b.RoadworthyExpiryDate>=CONVERT(date,t.ExpectedFinishLocal) AND b.InsuranceExpiryDate>=CONVERT(date,t.ExpectedFinishLocal)
  AND ((b.GrossVehicleMassKg<=3500) OR (b.GrossVehicleMassKg<=16000 AND dp.LicenceCode IN(N'C1',N'C',N'EC1',N'EC')) OR (b.GrossVehicleMassKg>16000 AND dp.LicenceCode IN(N'C',N'EC')))
  AND (rsv.ExpectedCapacity IS NULL OR b.PassengerCapacity>=rsv.ExpectedCapacity)
  AND NOT EXISTS(SELECT 1 FROM dbo.TripCannotProceedReports exception WHERE exception.TripId=t.TripId AND exception.ResolvedUtc IS NULL)
  AND NOT EXISTS(SELECT 1 FROM dbo.BusDefectReports defect WHERE defect.BusId=b.BusId AND defect.Severity=N'Critical' AND defect.DefectStatus<>N'Resolved')
  AND NOT EXISTS(SELECT 1 FROM dbo.TripExecutions active_execution WHERE active_execution.TripId<>t.TripId AND active_execution.ActualCompletionUtc IS NULL AND (active_execution.DriverProfileId=dp.DriverProfileId OR active_execution.BusId=b.BusId))
  AND NOT EXISTS(SELECT 1 FROM dbo.TripAssignments other_assignment INNER JOIN dbo.Trips other_trip ON other_trip.TripId=other_assignment.TripId
      WHERE other_assignment.IsCurrent=1 AND other_assignment.TripId<>t.TripId AND other_trip.TripStatus NOT IN(N'Completed',N'Cancelled')
        AND (other_assignment.DriverProfileId=dp.DriverProfileId OR other_assignment.BusId=b.BusId)
        AND DATEADD(SECOND,DATEDIFF(SECOND,CONVERT(time(0),'00:00'),other_trip.ScheduledDepartureTime),CONVERT(datetime2(0),other_trip.ServiceDate))<DATEADD(minute,15,t.ExpectedFinishLocal)
        AND DATEADD(minute,15,other_trip.ExpectedFinishLocal)>DATEADD(SECOND,DATEDIFF(SECOND,CONVERT(time(0),'00:00'),t.ScheduledDepartureTime),CONVERT(datetime2(0),t.ServiceDate)));";
            using(SqlCommand command=CreateCommand(connection,transaction,sql))
            { command.Parameters.Add("@TripId",SqlDbType.BigInt).Value=request.TripId;AddTimestamp(command,"@TripRv",request.TripRowVersion);AddTimestamp(command,"@RouteRv",request.RouteRowVersion);AddMoney(command,"@Fare",request.ReviewedFare);AddLocal(command,"@Now",now);using(SqlDataReader reader=command.ExecuteReader(CommandBehavior.SingleRow)){if(!reader.Read())throw new PassengerPersistenceException("This journey is no longer available for purchase. Refresh it and try again.",string.Empty,null);fare=reader.GetDecimal(0);capacity=reader.GetInt16(1);sold=reader.GetInt32(2);} }
        }

        private static void AcquireAssignmentLock(SqlConnection connection, SqlTransaction transaction)
        {
            using(SqlCommand command=CreateCommand(connection,transaction,"DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource=N'ForteMove.Assignments',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000;IF @r<0 THROW 51057,'Could not acquire the assignment lock.',1;"))command.ExecuteNonQuery();
        }

        private static SqlCommand CreateCommand(SqlConnection connection, SqlTransaction transaction, string sql) { SqlCommand command=connection.CreateCommand();command.Transaction=transaction;command.CommandText=sql;return command; }
        private static void AddString(SqlCommand command,string name,int size,string value){command.Parameters.Add(name,SqlDbType.NVarChar,size).Value=value;}
        private static void AddNullableString(SqlCommand command,string name,int size,string value){command.Parameters.Add(name,SqlDbType.NVarChar,size).Value=(object)value??DBNull.Value;}
        private static void AddMoney(SqlCommand command,string name,decimal value){SqlParameter parameter=command.Parameters.Add(name,SqlDbType.Decimal);parameter.Precision=12;parameter.Scale=2;parameter.Value=value;}
        private static void AddUtc(SqlCommand command,string name,DateTime value){SqlParameter parameter=command.Parameters.Add(name,SqlDbType.DateTime2);parameter.Scale=0;parameter.Value=value;}
        private static void AddLocal(SqlCommand command,string name,DateTime value){SqlParameter parameter=command.Parameters.Add(name,SqlDbType.DateTime2);parameter.Scale=0;parameter.Value=value;}
        private static void AddTimestamp(SqlCommand command,string name,byte[] value){command.Parameters.Add(name,SqlDbType.Timestamp,8).Value=(object)value??DBNull.Value;}
        private static bool BytesEqual(byte[] left,byte[] right){if(left==null||right==null||left.Length!=right.Length)return false;int difference=0;for(int i=0;i<left.Length;i++)difference|=left[i]^right[i];return difference==0;}
        private static string EscapeLike(string value){return value.Replace("~","~~").Replace("%","~%").Replace("_","~_").Replace("[","~[");}
    }
}
