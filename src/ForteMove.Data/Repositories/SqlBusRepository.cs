using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Services;
using ForteMove.Data.Internal;
using ForteMove.Models.Fleet;

namespace ForteMove.Data.Repositories
{
    public sealed class SqlBusRepository : IBusRepository
    {
        private const int MaximumSearchLength = 100;

        private const string GetActiveBusCategoriesSql = @"
SELECT
    BusCategoryId,
    CategoryCode,
    DisplayName
FROM dbo.BusCategories
WHERE IsActive = 1
ORDER BY DisplayName, BusCategoryId;";

        private const string GetActivePropulsionTypesSql = @"
SELECT
    PropulsionTypeId,
    PropulsionCode,
    DisplayName
FROM dbo.PropulsionTypes
WHERE IsActive = 1
ORDER BY DisplayName, PropulsionTypeId;";

        private const string GetNextFleetNumberSequenceSql = @"
SELECT ISNULL(MAX(TRY_CONVERT(INT, SUBSTRING(FleetNumber, 4, 27))), 0) + 1
FROM dbo.Buses
WHERE FleetNumber LIKE N'FM-%'
  AND LEN(FleetNumber) > 3
  AND SUBSTRING(FleetNumber, 4, 27) NOT LIKE N'%[^0-9]%'
  AND TRY_CONVERT(INT, SUBSTRING(FleetNumber, 4, 27)) IS NOT NULL;";

        private const string RegisterBusSql = @"
INSERT dbo.Buses
(
    BusCategoryId,
    PropulsionTypeId,
    FleetNumber,
    RegistrationNumber,
    Vin,
    Make,
    Model,
    ManufactureYear,
    PassengerCapacity,
    GrossVehicleMassKg,
    FuelTankCapacityLitres,
    BatteryCapacityKwh,
    OdometerKilometres,
    LicenceExpiryDate,
    RoadworthyExpiryDate,
    InsuranceExpiryDate,
    BaseOperationalState,
    CreatedByUserAccountId,
    UpdatedByUserAccountId
)
OUTPUT inserted.BusId
VALUES
(
    @BusCategoryId,
    @PropulsionTypeId,
    @FleetNumber,
    @RegistrationNumber,
    @Vin,
    @Make,
    @Model,
    @ManufactureYear,
    @PassengerCapacity,
    @GrossVehicleMassKg,
    @FuelTankCapacityLitres,
    @BatteryCapacityKwh,
    @OdometerKilometres,
    @LicenceExpiryDate,
    @RoadworthyExpiryDate,
    @InsuranceExpiryDate,
    @BaseOperationalState,
    @ActorUserAccountId,
    @ActorUserAccountId
);";

        private const string GetFleetListSql = @"
SELECT
    b.BusId,
    b.FleetNumber,
    b.RegistrationNumber,
    b.Vin,
    b.Make,
    b.Model,
    b.ManufactureYear,
    bc.DisplayName AS CategoryName,
    b.PassengerCapacity,
    b.GrossVehicleMassKg,
    pt.DisplayName AS PropulsionName,
    b.OdometerKilometres,
    b.LicenceExpiryDate,
    b.RoadworthyExpiryDate,
    b.InsuranceExpiryDate,
    b.BaseOperationalState
FROM dbo.Buses AS b
INNER JOIN dbo.BusCategories AS bc
    ON bc.BusCategoryId = b.BusCategoryId
INNER JOIN dbo.PropulsionTypes AS pt
    ON pt.PropulsionTypeId = b.PropulsionTypeId
WHERE
    (
        @SearchPattern IS NULL
        OR b.FleetNumber LIKE @SearchPattern ESCAPE N'~'
        OR b.RegistrationNumber LIKE @SearchPattern ESCAPE N'~'
        OR b.Vin LIKE @SearchPattern ESCAPE N'~'
        OR b.Make LIKE @SearchPattern ESCAPE N'~'
        OR b.Model LIKE @SearchPattern ESCAPE N'~'
    )
    AND
    (
        @BaseOperationalState IS NULL
        OR b.BaseOperationalState = @BaseOperationalState
    )
ORDER BY b.FleetNumber, b.BusId;";

        private readonly string connectionString;

        public SqlBusRepository(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException(
                    "A SQL Server connection string is required.",
                    "connectionString");
            }

            this.connectionString = connectionString;
        }

        public IList<LookupOption> GetActiveBusCategories()
        {
            return GetLookupOptions(GetActiveBusCategoriesSql);
        }

        public IList<LookupOption> GetActivePropulsionTypes()
        {
            return GetLookupOptions(GetActivePropulsionTypesSql);
        }

        public int GetNextFleetNumberSequence()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = GetNextFleetNumberSequenceSql;

                connection.Open();
                object result = command.ExecuteScalar();
                return Convert.ToInt32(result, CultureInfo.InvariantCulture);
            }
        }

        public long RegisterBus(Bus bus, long actorUserAccountId)
        {
            if (bus == null)
            {
                throw new ArgumentNullException("bus");
            }

            if (actorUserAccountId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    "actorUserAccountId",
                    "The actor user account identifier must be greater than zero.");
            }

            string operationalState = ToDatabaseOperationalState(bus.BaseOperationalState);

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        long busId = InsertBus(
                            connection,
                            transaction,
                            bus,
                            actorUserAccountId,
                            operationalState);

                        string detail = string.Format(
                            CultureInfo.InvariantCulture,
                            "FleetNumber={0}",
                            bus.FleetNumber);

                        SqlAuditWriter.Write(
                            connection,
                            transaction,
                            actorUserAccountId,
                            "BusRegistered",
                            "Bus",
                            busId.ToString(CultureInfo.InvariantCulture),
                            detail,
                            null,
                            DateTime.UtcNow);

                        transaction.Commit();
                        return busId;
                    }
                }
            }
            catch (SqlException exception)
            {
                if (!IsDuplicateKeyViolation(exception))
                {
                    throw;
                }

                DuplicateBusField field = GetDuplicateField(exception.Message);
                throw new DuplicateBusException(
                    field,
                    GetDuplicateMessage(field),
                    exception);
            }
        }

        public IList<BusListItem> GetFleetList(FleetQuery query)
        {
            string searchTerm = null;
            BusOperationalState? requestedState = null;
            if (query != null)
            {
                searchTerm = string.IsNullOrWhiteSpace(query.SearchTerm)
                    ? null
                    : query.SearchTerm.Trim();
                requestedState = query.BaseOperationalState;
            }

            if (searchTerm != null && searchTerm.Length > MaximumSearchLength)
            {
                throw new ArgumentException(
                    "The fleet search term cannot exceed 100 characters.",
                    "query");
            }

            string state = requestedState.HasValue
                ? ToDatabaseOperationalState(requestedState.Value)
                : null;
            string searchPattern = searchTerm == null
                ? null
                : "%" + EscapeLikeValue(searchTerm) + "%";

            IList<BusListItem> fleet = new List<BusListItem>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = GetFleetListSql;

                SqlParameter searchParameter = command.Parameters.Add(
                    "@SearchPattern",
                    SqlDbType.NVarChar,
                    202);
                searchParameter.Value = (object)searchPattern ?? DBNull.Value;

                SqlParameter stateParameter = command.Parameters.Add(
                    "@BaseOperationalState",
                    SqlDbType.NVarChar,
                    30);
                stateParameter.Value = (object)state ?? DBNull.Value;

                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        fleet.Add(new BusListItem
                        {
                            BusId = reader.GetInt64(0),
                            FleetNumber = reader.GetString(1),
                            RegistrationNumber = reader.GetString(2),
                            Vin = reader.GetString(3),
                            Make = reader.GetString(4),
                            Model = reader.GetString(5),
                            ManufactureYear = reader.GetInt16(6),
                            CategoryName = reader.GetString(7),
                            PassengerCapacity = reader.GetInt16(8),
                            GrossVehicleMassKg = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9),
                            RequiredLicenceCode = BusService.GetRequiredLicenceCode(reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9)),
                            PropulsionName = reader.GetString(10),
                            OdometerKilometres = reader.GetDecimal(11),
                            LicenceExpiryDate = reader.GetDateTime(12),
                            RoadworthyExpiryDate = reader.GetDateTime(13),
                            InsuranceExpiryDate = reader.GetDateTime(14),
                            BaseOperationalState = ParseOperationalState(reader.GetString(15))
                        });
                    }
                }
            }

            return fleet;
        }

        public BusDetails GetBusDetails(long busId)
        {
            const string sql = @"
SELECT b.BusId,b.BusCategoryId,bc.DisplayName,b.FleetNumber,b.RegistrationNumber,b.Vin,b.Make,b.Model,b.ManufactureYear,
b.PassengerCapacity,b.GrossVehicleMassKg,b.OdometerKilometres,b.LicenceExpiryDate,b.RoadworthyExpiryDate,b.InsuranceExpiryDate,b.BaseOperationalState,b.RowVersion
FROM dbo.Buses b INNER JOIN dbo.BusCategories bc ON bc.BusCategoryId=b.BusCategoryId WHERE b.BusId=@BusId;";
            using(SqlConnection connection=new SqlConnection(connectionString))
            using(SqlCommand command=new SqlCommand(sql,connection))
            {
                command.Parameters.Add("@BusId",SqlDbType.BigInt).Value=busId; connection.Open();
                using(SqlDataReader reader=command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if(!reader.Read()) return null;
                    return new BusDetails{BusId=reader.GetInt64(0),BusCategoryId=reader.GetInt32(1),CategoryName=reader.GetString(2),FleetNumber=reader.GetString(3),RegistrationNumber=reader.GetString(4),Vin=reader.GetString(5),Make=reader.GetString(6),Model=reader.GetString(7),ManufactureYear=reader.GetInt16(8),PassengerCapacity=reader.GetInt16(9),GrossVehicleMassKg=reader.IsDBNull(10)?(int?)null:reader.GetInt32(10),OdometerKilometres=reader.GetDecimal(11),LicenceExpiryDate=reader.GetDateTime(12),RoadworthyExpiryDate=reader.GetDateTime(13),InsuranceExpiryDate=reader.GetDateTime(14),BaseOperationalState=ParseOperationalState(reader.GetString(15)),RowVersion=(byte[])reader.GetValue(16)};
                }
            }
        }

        public void UpdateBusEligibility(BusDetails bus,long actorUserAccountId)
        {
            const string sql=@"UPDATE dbo.Buses SET BusCategoryId=@CategoryId,PassengerCapacity=@Capacity,GrossVehicleMassKg=@Gvm,
LicenceExpiryDate=@Licence,RoadworthyExpiryDate=@Roadworthy,InsuranceExpiryDate=@Insurance,BaseOperationalState=@State,
UpdatedByUserAccountId=@Actor,UpdatedUtc=SYSUTCDATETIME()
WHERE BusId=@BusId AND RowVersion=@RowVersion;";
            using(SqlConnection connection=new SqlConnection(connectionString)){connection.Open();using(SqlTransaction transaction=connection.BeginTransaction(IsolationLevel.Serializable))
            {int? previousGvm;using(SqlCommand read=new SqlCommand("SELECT GrossVehicleMassKg FROM dbo.Buses WITH(UPDLOCK,HOLDLOCK) WHERE BusId=@BusId AND RowVersion=@RowVersion;",connection,transaction)){read.Parameters.Add("@BusId",SqlDbType.BigInt).Value=bus.BusId;read.Parameters.Add("@RowVersion",SqlDbType.Timestamp,8).Value=bus.RowVersion;object value=read.ExecuteScalar();if(value==null)throw new InvalidOperationException("This bus was changed by another user. Refresh and try again.");previousGvm=value==DBNull.Value?(int?)null:Convert.ToInt32(value,CultureInfo.InvariantCulture);}
            using(SqlCommand command=new SqlCommand(sql,connection,transaction)){command.Parameters.Add("@CategoryId",SqlDbType.Int).Value=bus.BusCategoryId;command.Parameters.Add("@Capacity",SqlDbType.SmallInt).Value=bus.PassengerCapacity;command.Parameters.Add("@Gvm",SqlDbType.Int).Value=(object)bus.GrossVehicleMassKg??DBNull.Value;AddDateParameter(command,"@Licence",bus.LicenceExpiryDate);AddDateParameter(command,"@Roadworthy",bus.RoadworthyExpiryDate);AddDateParameter(command,"@Insurance",bus.InsuranceExpiryDate);AddRequiredStringParameter(command,"@State",30,ToDatabaseOperationalState(bus.BaseOperationalState));command.Parameters.Add("@Actor",SqlDbType.BigInt).Value=actorUserAccountId;command.Parameters.Add("@BusId",SqlDbType.BigInt).Value=bus.BusId;command.Parameters.Add("@RowVersion",SqlDbType.Timestamp,8).Value=bus.RowVersion;if(command.ExecuteNonQuery()!=1)throw new InvalidOperationException("This bus was changed by another user. Refresh and try again.");}
            SqlAuditWriter.Write(connection,transaction,actorUserAccountId,"BusUpdated","Bus",bus.BusId.ToString(CultureInfo.InvariantCulture),"Fields=Category,Capacity,Compliance,Status",null,DateTime.UtcNow);if(previousGvm!=bus.GrossVehicleMassKg)SqlAuditWriter.Write(connection,transaction,actorUserAccountId,"BusGvmUpdated","Bus",bus.BusId.ToString(CultureInfo.InvariantCulture),"Field=GrossVehicleMassKg",null,DateTime.UtcNow);transaction.Commit();}}
        }

        private IList<LookupOption> GetLookupOptions(string commandText)
        {
            IList<LookupOption> options = new List<LookupOption>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = commandText;

                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        options.Add(new LookupOption
                        {
                            Id = reader.GetInt32(0),
                            Code = reader.GetString(1),
                            DisplayName = reader.GetString(2)
                        });
                    }
                }
            }

            return options;
        }

        private static long InsertBus(
            SqlConnection connection,
            SqlTransaction transaction,
            Bus bus,
            long actorUserAccountId,
            string operationalState)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.Text;
                command.CommandText = RegisterBusSql;

                SqlParameter categoryParameter = command.Parameters.Add(
                    "@BusCategoryId",
                    SqlDbType.Int);
                categoryParameter.Value = bus.BusCategoryId;

                SqlParameter propulsionParameter = command.Parameters.Add(
                    "@PropulsionTypeId",
                    SqlDbType.Int);
                propulsionParameter.Value = bus.PropulsionTypeId;

                AddRequiredStringParameter(
                    command,
                    "@FleetNumber",
                    30,
                    bus.FleetNumber);
                AddRequiredStringParameter(
                    command,
                    "@RegistrationNumber",
                    30,
                    bus.RegistrationNumber);
                AddRequiredStringParameter(
                    command,
                    "@Vin",
                    50,
                    bus.Vin);
                AddRequiredStringParameter(command, "@Make", 100, bus.Make);
                AddRequiredStringParameter(command, "@Model", 100, bus.Model);

                SqlParameter yearParameter = command.Parameters.Add(
                    "@ManufactureYear",
                    SqlDbType.SmallInt);
                yearParameter.Value = bus.ManufactureYear;

                SqlParameter capacityParameter = command.Parameters.Add(
                    "@PassengerCapacity",
                    SqlDbType.SmallInt);
                capacityParameter.Value = bus.PassengerCapacity;

                SqlParameter gvmParameter = command.Parameters.Add(
                    "@GrossVehicleMassKg",
                    SqlDbType.Int);
                gvmParameter.Value = (object)bus.GrossVehicleMassKg ?? DBNull.Value;

                AddDecimalParameter(
                    command,
                    "@FuelTankCapacityLitres",
                    10,
                    2,
                    bus.FuelTankCapacityLitres);
                AddDecimalParameter(
                    command,
                    "@BatteryCapacityKwh",
                    10,
                    2,
                    bus.BatteryCapacityKwh);
                AddDecimalParameter(
                    command,
                    "@OdometerKilometres",
                    12,
                    1,
                    bus.OdometerKilometres);

                AddDateParameter(
                    command,
                    "@LicenceExpiryDate",
                    bus.LicenceExpiryDate);
                AddDateParameter(
                    command,
                    "@RoadworthyExpiryDate",
                    bus.RoadworthyExpiryDate);
                AddDateParameter(
                    command,
                    "@InsuranceExpiryDate",
                    bus.InsuranceExpiryDate);
                AddRequiredStringParameter(
                    command,
                    "@BaseOperationalState",
                    30,
                    operationalState);

                SqlParameter actorParameter = command.Parameters.Add(
                    "@ActorUserAccountId",
                    SqlDbType.BigInt);
                actorParameter.Value = actorUserAccountId;

                object result = command.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    throw new InvalidOperationException(
                        "The database did not return the registered bus identifier.");
                }

                return Convert.ToInt64(result, CultureInfo.InvariantCulture);
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

        private static void AddDateParameter(
            SqlCommand command,
            string parameterName,
            DateTime value)
        {
            SqlParameter parameter = command.Parameters.Add(
                parameterName,
                SqlDbType.Date);
            parameter.Value = value.Date;
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

        private static DuplicateBusField GetDuplicateField(string message)
        {
            if (ContainsIgnoreCase(message, "UQ_Buses_FleetNumber"))
            {
                return DuplicateBusField.FleetNumber;
            }

            if (ContainsIgnoreCase(message, "UQ_Buses_RegistrationNumber"))
            {
                return DuplicateBusField.RegistrationNumber;
            }

            if (ContainsIgnoreCase(message, "UQ_Buses_Vin"))
            {
                return DuplicateBusField.Vin;
            }

            return DuplicateBusField.Unknown;
        }

        private static string GetDuplicateMessage(DuplicateBusField field)
        {
            switch (field)
            {
                case DuplicateBusField.FleetNumber:
                    return "A bus with this fleet number already exists.";
                case DuplicateBusField.RegistrationNumber:
                    return "A bus with this registration number already exists.";
                case DuplicateBusField.Vin:
                    return "A bus with this VIN already exists.";
                default:
                    return "A bus with one or more of these identifiers already exists.";
            }
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return source != null &&
                source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ToDatabaseOperationalState(BusOperationalState state)
        {
            switch (state)
            {
                case BusOperationalState.Operational:
                    return "Operational";
                case BusOperationalState.OutOfService:
                    return "OutOfService";
                case BusOperationalState.UnderMaintenance:
                    return "UnderMaintenance";
                case BusOperationalState.Retired:
                    return "Retired";
                default:
                    throw new ArgumentOutOfRangeException(
                        "state",
                        state,
                        "The bus operational state is not supported.");
            }
        }

        private static BusOperationalState ParseOperationalState(string state)
        {
            if (string.Equals(state, "Operational", StringComparison.Ordinal))
            {
                return BusOperationalState.Operational;
            }

            if (string.Equals(state, "OutOfService", StringComparison.Ordinal))
            {
                return BusOperationalState.OutOfService;
            }

            if (string.Equals(state, "UnderMaintenance", StringComparison.Ordinal))
            {
                return BusOperationalState.UnderMaintenance;
            }

            if (string.Equals(state, "Retired", StringComparison.Ordinal))
            {
                return BusOperationalState.Retired;
            }

            throw new DataException(
                "The database contains an unsupported bus operational state.");
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
