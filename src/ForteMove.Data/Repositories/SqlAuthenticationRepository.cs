using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using ForteMove.Business.Contracts;
using ForteMove.Data.Internal;
using ForteMove.Models.Security;

namespace ForteMove.Data.Repositories
{
    public sealed class SqlAuthenticationRepository : IAuthenticationRepository
    {
        private const string FindByNormalizedEmailSql = @"
SELECT TOP (1)
    ua.UserAccountId,
    ua.NormalizedEmail,
    ua.PasswordAlgorithm,
    ua.PasswordHash,
    ua.PasswordSalt,
    ua.PasswordIterations,
    ua.IsActive,
    r.IsActive AS IsRoleActive,
    r.RoleCode,
    ua.FailedLoginCount,
    ua.LockoutEndUtc
FROM dbo.UserAccounts AS ua
INNER JOIN dbo.Roles AS r
    ON r.RoleId = ua.RoleId
WHERE ua.NormalizedEmail = @NormalizedEmail;";

        private const string GetPrincipalContextSql = @"
SELECT
    ua.UserAccountId,
    ua.Email,
    sp.FirstName,
    sp.LastName,
    r.RoleCode,
    r.DisplayName AS RoleDisplayName,
    ua.IsActive,
    r.IsActive AS IsRoleActive,
    ua.MustChangePassword
FROM dbo.UserAccounts AS ua
INNER JOIN dbo.Roles AS r
    ON r.RoleId = ua.RoleId
LEFT JOIN dbo.StaffProfiles AS sp
    ON sp.UserAccountId = ua.UserAccountId
WHERE ua.UserAccountId = @UserAccountId;";

        private const string RecordFailedLoginSql = @"
UPDATE dbo.UserAccounts WITH (UPDLOCK, ROWLOCK)
SET
    FailedLoginCount =
        CASE
            WHEN LockoutEndUtc IS NOT NULL AND LockoutEndUtc > @AttemptedAtUtc
                THEN FailedLoginCount
            WHEN LockoutEndUtc IS NOT NULL AND LockoutEndUtc <= @AttemptedAtUtc
                THEN 1
            ELSE FailedLoginCount + 1
        END,
    LockoutEndUtc =
        CASE
            WHEN LockoutEndUtc IS NOT NULL AND LockoutEndUtc > @AttemptedAtUtc
                THEN LockoutEndUtc
            WHEN
                CASE
                    WHEN LockoutEndUtc IS NOT NULL AND LockoutEndUtc <= @AttemptedAtUtc
                        THEN 1
                    ELSE FailedLoginCount + 1
                END >= @FailureThreshold
                THEN @NewLockoutEndUtc
            ELSE NULL
        END,
    UpdatedUtc = @AttemptedAtUtc
OUTPUT
    inserted.FailedLoginCount,
    inserted.LockoutEndUtc
WHERE UserAccountId = @UserAccountId;";

        private const string RecordSuccessfulLoginSql = @"
UPDATE dbo.UserAccounts
SET
    FailedLoginCount = 0,
    LockoutEndUtc = NULL,
    LastLoginUtc = @LoggedInAtUtc,
    UpdatedUtc = @LoggedInAtUtc
WHERE UserAccountId = @UserAccountId;";

        private readonly string connectionString;

        public SqlAuthenticationRepository(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException(
                    "A SQL Server connection string is required.",
                    "connectionString");
            }

            this.connectionString = connectionString;
        }

        public UserCredentialRecord FindByNormalizedEmail(string normalizedEmail)
        {
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                throw new ArgumentException(
                    "A normalized email address is required.",
                    "normalizedEmail");
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = FindByNormalizedEmailSql;

                SqlParameter emailParameter = command.Parameters.Add(
                    "@NormalizedEmail",
                    SqlDbType.NVarChar,
                    254);
                emailParameter.Value = normalizedEmail;

                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return new UserCredentialRecord
                    {
                        UserAccountId = reader.GetInt64(0),
                        NormalizedEmail = reader.GetString(1),
                        PasswordAlgorithm = reader.GetString(2),
                        PasswordHash = (byte[])reader.GetValue(3),
                        PasswordSalt = (byte[])reader.GetValue(4),
                        PasswordIterations = reader.GetInt32(5),
                        IsActive = reader.GetBoolean(6),
                        IsRoleActive = reader.GetBoolean(7),
                        Role = ParseRoleCode(reader.GetString(8)),
                        FailedLoginCount = reader.GetInt32(9),
                        LockoutEndUtc = reader.IsDBNull(10)
                            ? (DateTime?)null
                            : reader.GetDateTime(10)
                    };
                }
            }
        }

        public UserCredentialRecord GetCredential(long userAccountId)
        {
            EnsureValidUserAccountId(userAccountId);
            const string sql = @"
SELECT ua.UserAccountId, ua.NormalizedEmail, ua.PasswordAlgorithm, ua.PasswordHash,
       ua.PasswordSalt, ua.PasswordIterations, ua.IsActive, r.IsActive, r.RoleCode,
       ua.FailedLoginCount, ua.LockoutEndUtc
FROM dbo.UserAccounts AS ua
INNER JOIN dbo.Roles AS r ON r.RoleId=ua.RoleId
WHERE ua.UserAccountId=@UserAccountId;";
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.Parameters.Add("@UserAccountId", SqlDbType.BigInt).Value = userAccountId;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read()) return null;
                    return new UserCredentialRecord
                    {
                        UserAccountId = reader.GetInt64(0),
                        NormalizedEmail = reader.GetString(1),
                        PasswordAlgorithm = reader.GetString(2),
                        PasswordHash = (byte[])reader.GetValue(3),
                        PasswordSalt = (byte[])reader.GetValue(4),
                        PasswordIterations = reader.GetInt32(5),
                        IsActive = reader.GetBoolean(6),
                        IsRoleActive = reader.GetBoolean(7),
                        Role = ParseRoleCode(reader.GetString(8)),
                        FailedLoginCount = reader.GetInt32(9),
                        LockoutEndUtc = reader.IsDBNull(10) ? (DateTime?)null : reader.GetDateTime(10)
                    };
                }
            }
        }

        public void ChangePassword(long userAccountId, PasswordHash passwordHash, DateTime changedAtUtc, string clientIpAddress)
        {
            EnsureValidUserAccountId(userAccountId);
            if (passwordHash == null) throw new ArgumentNullException("passwordHash");
            EnsureValidClientIpAddress(clientIpAddress);
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"
UPDATE dbo.UserAccounts WITH (UPDLOCK, ROWLOCK)
SET PasswordAlgorithm=@Algorithm, PasswordHash=@Hash, PasswordSalt=@Salt,
    PasswordIterations=@Iterations, MustChangePassword=0, FailedLoginCount=0,
    LockoutEndUtc=NULL, UpdatedUtc=@ChangedUtc
WHERE UserAccountId=@UserAccountId AND IsActive=1;";
                        command.Parameters.Add("@Algorithm", SqlDbType.NVarChar, 50).Value = passwordHash.Algorithm;
                        command.Parameters.Add("@Hash", SqlDbType.VarBinary, 64).Value = passwordHash.Hash;
                        command.Parameters.Add("@Salt", SqlDbType.VarBinary, 64).Value = passwordHash.Salt;
                        command.Parameters.Add("@Iterations", SqlDbType.Int).Value = passwordHash.Iterations;
                        SqlParameter changed = command.Parameters.Add("@ChangedUtc", SqlDbType.DateTime2); changed.Scale = 0; changed.Value = changedAtUtc;
                        command.Parameters.Add("@UserAccountId", SqlDbType.BigInt).Value = userAccountId;
                        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("The account is no longer available.");
                    }
                    SqlAuditWriter.Write(connection, transaction, userAccountId, "PasswordChanged", "UserAccount",
                        userAccountId.ToString(CultureInfo.InvariantCulture), null, clientIpAddress, changedAtUtc);
                    transaction.Commit();
                }
            }
        }

        public PrincipalContext GetPrincipalContext(long userAccountId)
        {
            if (userAccountId <= 0)
            {
                return null;
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = GetPrincipalContextSql;

                SqlParameter userParameter = command.Parameters.Add(
                    "@UserAccountId",
                    SqlDbType.BigInt);
                userParameter.Value = userAccountId;

                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    string email = reader.GetString(1);
                    string firstName = reader.IsDBNull(2) ? null : reader.GetString(2);
                    string lastName = reader.IsDBNull(3) ? null : reader.GetString(3);

                    return new PrincipalContext
                    {
                        UserAccountId = reader.GetInt64(0),
                        Email = email,
                        DisplayName = BuildDisplayName(firstName, lastName, email),
                        Role = ParseRoleCode(reader.GetString(4)),
                        RoleDisplayName = reader.GetString(5),
                        IsActive = reader.GetBoolean(6),
                        IsRoleActive = reader.GetBoolean(7),
                        MustChangePassword = reader.GetBoolean(8)
                    };
                }
            }
        }

        public LoginFailureState RecordFailedLogin(
            long userAccountId,
            DateTime attemptedAtUtc,
            int failureThreshold,
            TimeSpan lockoutDuration,
            string clientIpAddress)
        {
            EnsureValidUserAccountId(userAccountId);
            EnsureValidClientIpAddress(clientIpAddress);
            if (failureThreshold <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    "failureThreshold",
                    "The failure threshold must be greater than zero.");
            }

            if (lockoutDuration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    "lockoutDuration",
                    "The lockout duration must be greater than zero.");
            }

            DateTime newLockoutEndUtc;
            try
            {
                newLockoutEndUtc = attemptedAtUtc.Add(lockoutDuration);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new ArgumentOutOfRangeException(
                    "lockoutDuration",
                    lockoutDuration,
                    "The lockout duration produces a date outside the supported range.");
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    LoginFailureState state = UpdateFailedLoginState(
                        connection,
                        transaction,
                        userAccountId,
                        attemptedAtUtc,
                        failureThreshold,
                        newLockoutEndUtc);

                    string detail = string.Format(
                        CultureInfo.InvariantCulture,
                        "FailedLoginCount={0};IsLockedOut={1}",
                        state.FailedLoginCount,
                        state.IsLockedOut ? "true" : "false");

                    SqlAuditWriter.Write(
                        connection,
                        transaction,
                        null,
                        "LoginFailed",
                        "UserAccount",
                        userAccountId.ToString(CultureInfo.InvariantCulture),
                        detail,
                        clientIpAddress,
                        attemptedAtUtc);

                    transaction.Commit();
                    return state;
                }
            }
        }

        public void RecordSuccessfulLogin(
            long userAccountId,
            DateTime loggedInAtUtc,
            string clientIpAddress)
        {
            EnsureValidUserAccountId(userAccountId);
            EnsureValidClientIpAddress(clientIpAddress);

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandType = CommandType.Text;
                        command.CommandText = RecordSuccessfulLoginSql;

                        SqlParameter userParameter = command.Parameters.Add(
                            "@UserAccountId",
                            SqlDbType.BigInt);
                        userParameter.Value = userAccountId;

                        SqlParameter loginParameter = command.Parameters.Add(
                            "@LoggedInAtUtc",
                            SqlDbType.DateTime2);
                        loginParameter.Scale = 0;
                        loginParameter.Value = loggedInAtUtc;

                        if (command.ExecuteNonQuery() != 1)
                        {
                            throw new InvalidOperationException(
                                "The user account no longer exists.");
                        }
                    }

                    SqlAuditWriter.Write(
                        connection,
                        transaction,
                        userAccountId,
                        "LoginSucceeded",
                        "UserAccount",
                        userAccountId.ToString(CultureInfo.InvariantCulture),
                        null,
                        clientIpAddress,
                        loggedInAtUtc);

                    transaction.Commit();
                }
            }
        }

        public void RecordLogout(
            long userAccountId,
            DateTime loggedOutAtUtc,
            string clientIpAddress)
        {
            EnsureValidUserAccountId(userAccountId);
            EnsureValidClientIpAddress(clientIpAddress);

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    SqlAuditWriter.Write(
                        connection,
                        transaction,
                        userAccountId,
                        "Logout",
                        "UserAccount",
                        userAccountId.ToString(CultureInfo.InvariantCulture),
                        null,
                        clientIpAddress,
                        loggedOutAtUtc);

                    transaction.Commit();
                }
            }
        }

        private static LoginFailureState UpdateFailedLoginState(
            SqlConnection connection,
            SqlTransaction transaction,
            long userAccountId,
            DateTime attemptedAtUtc,
            int failureThreshold,
            DateTime newLockoutEndUtc)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.Text;
                command.CommandText = RecordFailedLoginSql;

                SqlParameter userParameter = command.Parameters.Add(
                    "@UserAccountId",
                    SqlDbType.BigInt);
                userParameter.Value = userAccountId;

                SqlParameter attemptedParameter = command.Parameters.Add(
                    "@AttemptedAtUtc",
                    SqlDbType.DateTime2);
                attemptedParameter.Scale = 0;
                attemptedParameter.Value = attemptedAtUtc;

                SqlParameter thresholdParameter = command.Parameters.Add(
                    "@FailureThreshold",
                    SqlDbType.Int);
                thresholdParameter.Value = failureThreshold;

                SqlParameter lockoutEndParameter = command.Parameters.Add(
                    "@NewLockoutEndUtc",
                    SqlDbType.DateTime2);
                lockoutEndParameter.Scale = 0;
                lockoutEndParameter.Value = newLockoutEndUtc;

                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        throw new InvalidOperationException(
                            "The user account no longer exists.");
                    }

                    int failedLoginCount = reader.GetInt32(0);
                    DateTime? lockoutEndUtc = reader.IsDBNull(1)
                        ? (DateTime?)null
                        : reader.GetDateTime(1);

                    return new LoginFailureState
                    {
                        FailedLoginCount = failedLoginCount,
                        LockoutEndUtc = lockoutEndUtc,
                        IsLockedOut = lockoutEndUtc.HasValue &&
                            lockoutEndUtc.Value > attemptedAtUtc
                    };
                }
            }
        }

        private static RoleCode ParseRoleCode(string roleCode)
        {
            if (string.Equals(
                roleCode,
                "TransportAdministrator",
                StringComparison.Ordinal))
            {
                return RoleCode.TransportAdministrator;
            }

            if (string.Equals(roleCode, "Driver", StringComparison.Ordinal))
            {
                return RoleCode.Driver;
            }

            if (string.Equals(roleCode, "Passenger", StringComparison.Ordinal))
            {
                return RoleCode.Passenger;
            }

            return RoleCode.Unknown;
        }

        private static string BuildDisplayName(
            string firstName,
            string lastName,
            string fallbackEmail)
        {
            string displayName = string.Format(
                CultureInfo.CurrentCulture,
                "{0} {1}",
                firstName,
                lastName).Trim();

            return displayName.Length == 0 ? fallbackEmail : displayName;
        }

        private static void EnsureValidUserAccountId(long userAccountId)
        {
            if (userAccountId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    "userAccountId",
                    "The user account identifier must be greater than zero.");
            }
        }

        private static void EnsureValidClientIpAddress(string clientIpAddress)
        {
            if (clientIpAddress != null && clientIpAddress.Length > 45)
            {
                throw new ArgumentException(
                    "The client IP address cannot exceed 45 characters.",
                    "clientIpAddress");
            }
        }
    }
}
