using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Identifiers;
using ForteMove.Data.Internal;
using ForteMove.Models.Drivers;

namespace ForteMove.Data.Repositories
{
    public sealed class SqlDriverRepository : IDriverRepository
    {
        private readonly string connectionString;

        public SqlDriverRepository(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("A connection string is required.", "connectionString");
            this.connectionString = connectionString;
        }

        public int GetNextEmployeeNumberSequence()
        {
            const string sql = @"SELECT ISNULL(MAX(TRY_CONVERT(INT, SUBSTRING(EmployeeNumber,5,46))),0)+1
FROM dbo.StaffProfiles WHERE EmployeeNumber LIKE N'DRV-%'
AND SUBSTRING(EmployeeNumber,5,46) NOT LIKE N'%[^0-9]%';";
            using (SqlConnection c = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(sql, c))
            { c.Open(); return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture); }
        }

        public long CreateDriver(DriverPersistenceRecord driver, long actorUserAccountId)
        {
            if (driver == null) throw new ArgumentNullException("driver");
            try
            {
                using (SqlConnection c = new SqlConnection(connectionString))
                {
                    c.Open();
                    using (SqlTransaction tx = c.BeginTransaction(IsolationLevel.Serializable))
                    {
                        AcquireLock(c, tx, "ForteMove.Driver.EmployeeNumber");
                        int sequence;
                        using (SqlCommand cmd = NewCommand(c, tx, @"SELECT ISNULL(MAX(TRY_CONVERT(INT, SUBSTRING(EmployeeNumber,5,46))),0)+1
FROM dbo.StaffProfiles WITH (UPDLOCK,HOLDLOCK) WHERE EmployeeNumber LIKE N'DRV-%'
AND SUBSTRING(EmployeeNumber,5,46) NOT LIKE N'%[^0-9]%';"))
                            sequence = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
                        driver.EmployeeNumber = IdentifierCodePolicy.FormatDriverEmployeeNumber(sequence);

                        int roleId;
                        using (SqlCommand cmd = NewCommand(c, tx, "SELECT RoleId FROM dbo.Roles WITH (UPDLOCK,HOLDLOCK) WHERE RoleCode=N'Driver' AND IsActive=1;"))
                        {
                            object role = cmd.ExecuteScalar();
                            if (role == null) throw new InvalidOperationException("The Driver role is unavailable.");
                            roleId = Convert.ToInt32(role, CultureInfo.InvariantCulture);
                        }

                        long userId;
                        using (SqlCommand cmd = NewCommand(c, tx, @"INSERT dbo.UserAccounts
(RoleId,Email,NormalizedEmail,PasswordAlgorithm,PasswordHash,PasswordSalt,PasswordIterations,IsActive,MustChangePassword)
OUTPUT inserted.UserAccountId VALUES
(@RoleId,@Email,@NormalizedEmail,@Algorithm,@Hash,@Salt,@Iterations,1,1);"))
                        {
                            cmd.Parameters.Add("@RoleId", SqlDbType.Int).Value = roleId;
                            AddString(cmd,"@Email",254,driver.Email); AddString(cmd,"@NormalizedEmail",254,driver.NormalizedEmail);
                            AddString(cmd,"@Algorithm",50,driver.PasswordAlgorithm);
                            cmd.Parameters.Add("@Hash",SqlDbType.VarBinary,64).Value=driver.PasswordHash;
                            cmd.Parameters.Add("@Salt",SqlDbType.VarBinary,64).Value=driver.PasswordSalt;
                            cmd.Parameters.Add("@Iterations",SqlDbType.Int).Value=driver.PasswordIterations;
                            userId=Convert.ToInt64(cmd.ExecuteScalar(),CultureInfo.InvariantCulture);
                        }
                        long staffId;
                        using (SqlCommand cmd = NewCommand(c, tx, @"INSERT dbo.StaffProfiles
(UserAccountId,EmployeeNumber,FirstName,LastName,PhoneNumber,EmploymentStatus)
OUTPUT inserted.StaffProfileId VALUES (@UserId,@EmployeeNumber,@FirstName,@LastName,@Phone,N'Active');"))
                        {
                            cmd.Parameters.Add("@UserId",SqlDbType.BigInt).Value=userId; AddString(cmd,"@EmployeeNumber",50,driver.EmployeeNumber);
                            AddString(cmd,"@FirstName",100,driver.FirstName); AddString(cmd,"@LastName",100,driver.LastName); AddNullableString(cmd,"@Phone",30,driver.PhoneNumber);
                            staffId=Convert.ToInt64(cmd.ExecuteScalar(),CultureInfo.InvariantCulture);
                        }
                        long driverId;
                        using (SqlCommand cmd = NewCommand(c, tx, @"INSERT dbo.DriverProfiles
(StaffProfileId,DateOfBirth,AvailabilityStatus,LicenceNumber,LicenceCode,LicenceExpiryDate,PrdpNumber,PrdpCategory,PrdpExpiryDate,CreatedByUserAccountId,UpdatedByUserAccountId)
OUTPUT inserted.DriverProfileId VALUES
(@StaffId,@Dob,@Availability,@LicenceNumber,@LicenceCode,@LicenceExpiry,@PrdpNumber,N'P',@PrdpExpiry,@Actor,@Actor);"))
                        {
                            cmd.Parameters.Add("@StaffId",SqlDbType.BigInt).Value=staffId; AddDate(cmd,"@Dob",driver.DateOfBirth);
                            AddString(cmd,"@Availability",30,driver.AvailabilityStatus.ToString()); AddString(cmd,"@LicenceNumber",50,driver.LicenceNumber);
                            AddString(cmd,"@LicenceCode",5,driver.LicenceCode.ToString()); AddDate(cmd,"@LicenceExpiry",driver.LicenceExpiryDate);
                            AddString(cmd,"@PrdpNumber",50,driver.PrdpNumber); AddDate(cmd,"@PrdpExpiry",driver.PrdpExpiryDate);
                            cmd.Parameters.Add("@Actor",SqlDbType.BigInt).Value=actorUserAccountId;
                            driverId=Convert.ToInt64(cmd.ExecuteScalar(),CultureInfo.InvariantCulture);
                        }
                        SqlAuditWriter.Write(c,tx,actorUserAccountId,"DriverCreated","DriverProfile",driverId.ToString(CultureInfo.InvariantCulture),
                            "EmployeeNumber="+driver.EmployeeNumber,null,DateTime.UtcNow);
                        tx.Commit(); return driverId;
                    }
                }
            }
            catch (SqlException ex)
            {
                if (!IsDuplicate(ex)) throw;
                string field = Contains(ex.Message,"NormalizedEmail") ? "Email" : Contains(ex.Message,"EmployeeNumber") ? "EmployeeNumber" :
                    Contains(ex.Message,"LicenceNumber") ? "LicenceNumber" : Contains(ex.Message,"PrdpNumber") ? "PrdpNumber" : string.Empty;
                throw new DriverPersistenceException(field, field.Length == 0 ? "A Driver with one of these identifiers already exists." : "This " + Friendly(field) + " is already in use.", ex);
            }
        }

        public IList<Driver> GetDrivers()
        {
            IList<Driver> rows=new List<Driver>();
            using(SqlConnection c=new SqlConnection(connectionString)) using(SqlCommand cmd=new SqlCommand(SelectDriversSql+" ORDER BY sp.EmployeeNumber;",c))
            { c.Open(); using(SqlDataReader r=cmd.ExecuteReader()) while(r.Read()) rows.Add(ReadDriver(r)); }
            return rows;
        }

        public Driver GetDriver(long driverProfileId)
        {
            using(SqlConnection c=new SqlConnection(connectionString)) using(SqlCommand cmd=new SqlCommand(SelectDriversSql+" AND dp.DriverProfileId=@Id;",c))
            { cmd.Parameters.Add("@Id",SqlDbType.BigInt).Value=driverProfileId; c.Open(); using(SqlDataReader r=cmd.ExecuteReader(CommandBehavior.SingleRow)) return r.Read()?ReadDriver(r):null; }
        }

        public Driver GetDriverByUserAccount(long userAccountId)
        {
            using(SqlConnection c=new SqlConnection(connectionString)) using(SqlCommand cmd=new SqlCommand(SelectDriversSql+" AND ua.UserAccountId=@Id;",c))
            { cmd.Parameters.Add("@Id",SqlDbType.BigInt).Value=userAccountId; c.Open(); using(SqlDataReader r=cmd.ExecuteReader(CommandBehavior.SingleRow)) return r.Read()?ReadDriver(r):null; }
        }

        public void UpdateDriver(UpdateDriverRequest request, long actorUserAccountId)
        {
            try
            {
                using(SqlConnection c=new SqlConnection(connectionString))
                { c.Open(); using(SqlTransaction tx=c.BeginTransaction(IsolationLevel.Serializable))
                    {
                        long userId; long staffId; string previousAvailability;
                        using(SqlCommand cmd=NewCommand(c,tx,@"SELECT sp.UserAccountId,dp.StaffProfileId,dp.AvailabilityStatus
FROM dbo.DriverProfiles dp WITH(UPDLOCK,HOLDLOCK) INNER JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId
WHERE dp.DriverProfileId=@Id AND dp.RowVersion=@DriverRv AND sp.RowVersion=@StaffRv
AND EXISTS(SELECT 1 FROM dbo.UserAccounts ua WHERE ua.UserAccountId=sp.UserAccountId AND ua.RowVersion=@UserRv);"))
                        { cmd.Parameters.Add("@Id",SqlDbType.BigInt).Value=request.DriverProfileId; AddBinary(cmd,"@DriverRv",request.DriverRowVersion); AddBinary(cmd,"@StaffRv",request.StaffRowVersion); AddBinary(cmd,"@UserRv",request.UserRowVersion);
                            using(SqlDataReader r=cmd.ExecuteReader(CommandBehavior.SingleRow)){if(!r.Read())throw new DriverPersistenceException(string.Empty,"This Driver was changed by another user. Refresh and try again.",null);userId=r.GetInt64(0);staffId=r.GetInt64(1);previousAvailability=r.GetString(2);} }
                        using(SqlCommand cmd=NewCommand(c,tx,@"UPDATE dbo.UserAccounts SET Email=@Email,NormalizedEmail=@NormalizedEmail,UpdatedUtc=SYSUTCDATETIME() WHERE UserAccountId=@UserId;
UPDATE dbo.StaffProfiles SET FirstName=@FirstName,LastName=@LastName,PhoneNumber=@Phone,UpdatedUtc=SYSUTCDATETIME() WHERE StaffProfileId=@StaffId;
UPDATE dbo.DriverProfiles SET DateOfBirth=@Dob,AvailabilityStatus=@Availability,LicenceNumber=@LicenceNumber,LicenceCode=@LicenceCode,
LicenceExpiryDate=@LicenceExpiry,PrdpNumber=@PrdpNumber,PrdpExpiryDate=@PrdpExpiry,UpdatedByUserAccountId=@Actor,UpdatedUtc=SYSUTCDATETIME()
WHERE DriverProfileId=@DriverId;"))
                        { AddString(cmd,"@Email",254,request.Email);AddString(cmd,"@NormalizedEmail",254,request.Email.ToUpperInvariant());cmd.Parameters.Add("@UserId",SqlDbType.BigInt).Value=userId;
                          AddString(cmd,"@FirstName",100,request.FirstName);AddString(cmd,"@LastName",100,request.LastName);AddNullableString(cmd,"@Phone",30,request.PhoneNumber);cmd.Parameters.Add("@StaffId",SqlDbType.BigInt).Value=staffId;
                          AddDate(cmd,"@Dob",request.DateOfBirth.Value);AddString(cmd,"@Availability",30,request.AvailabilityStatus.Value.ToString());AddString(cmd,"@LicenceNumber",50,request.LicenceNumber);AddString(cmd,"@LicenceCode",5,request.LicenceCode.Value.ToString());AddDate(cmd,"@LicenceExpiry",request.LicenceExpiryDate.Value);AddString(cmd,"@PrdpNumber",50,request.PrdpNumber);AddDate(cmd,"@PrdpExpiry",request.PrdpExpiryDate.Value);cmd.Parameters.Add("@Actor",SqlDbType.BigInt).Value=actorUserAccountId;cmd.Parameters.Add("@DriverId",SqlDbType.BigInt).Value=request.DriverProfileId;cmd.ExecuteNonQuery(); }
                        SqlAuditWriter.Write(c,tx,actorUserAccountId,"DriverUpdated","DriverProfile",request.DriverProfileId.ToString(CultureInfo.InvariantCulture),"Fields=Profile,Credentials",null,DateTime.UtcNow);
                        if(!string.Equals(previousAvailability,request.AvailabilityStatus.Value.ToString(),StringComparison.Ordinal)) SqlAuditWriter.Write(c,tx,actorUserAccountId,"DriverAvailabilityChanged","DriverProfile",request.DriverProfileId.ToString(CultureInfo.InvariantCulture),"Availability="+request.AvailabilityStatus.Value,null,DateTime.UtcNow);
                        tx.Commit();
                    }
                }
            }
            catch(SqlException ex){if(!IsDuplicate(ex))throw;string field=Contains(ex.Message,"NormalizedEmail")?"Email":Contains(ex.Message,"LicenceNumber")?"LicenceNumber":Contains(ex.Message,"PrdpNumber")?"PrdpNumber":string.Empty;throw new DriverPersistenceException(field,"This "+Friendly(field)+" is already in use.",ex);}
        }

        private const string SelectDriversSql=@"SELECT dp.DriverProfileId,dp.StaffProfileId,ua.UserAccountId,sp.EmployeeNumber,ua.Email,sp.FirstName,sp.LastName,sp.PhoneNumber,
sp.EmploymentStatus,ua.IsActive,dp.DateOfBirth,dp.AvailabilityStatus,dp.LicenceNumber,dp.LicenceCode,dp.LicenceExpiryDate,dp.PrdpNumber,dp.PrdpExpiryDate,
ua.RowVersion,sp.RowVersion,dp.RowVersion
FROM dbo.DriverProfiles dp INNER JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId
INNER JOIN dbo.UserAccounts ua ON ua.UserAccountId=sp.UserAccountId WHERE 1=1";
        private static Driver ReadDriver(SqlDataReader r){return new Driver{DriverProfileId=r.GetInt64(0),StaffProfileId=r.GetInt64(1),UserAccountId=r.GetInt64(2),EmployeeNumber=r.GetString(3),Email=r.GetString(4),FirstName=r.GetString(5),LastName=r.GetString(6),PhoneNumber=r.IsDBNull(7)?null:r.GetString(7),EmploymentStatus=r.GetString(8),AccountIsActive=r.GetBoolean(9),DateOfBirth=r.GetDateTime(10),AvailabilityStatus=(DriverAvailabilityStatus)Enum.Parse(typeof(DriverAvailabilityStatus),r.GetString(11),false),LicenceNumber=r.GetString(12),LicenceCode=(DriverLicenceCode)Enum.Parse(typeof(DriverLicenceCode),r.GetString(13),false),LicenceExpiryDate=r.GetDateTime(14),PrdpNumber=r.GetString(15),PrdpExpiryDate=r.GetDateTime(16),UserRowVersion=(byte[])r.GetValue(17),StaffRowVersion=(byte[])r.GetValue(18),DriverRowVersion=(byte[])r.GetValue(19)};}
        private static void AcquireLock(SqlConnection c,SqlTransaction tx,string resource){using(SqlCommand cmd=NewCommand(c,tx,"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@Resource,@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000; IF @r<0 THROW 51012,'Could not acquire Driver sequence lock.',1;")){AddString(cmd,"@Resource",255,resource);cmd.ExecuteNonQuery();}}
        private static SqlCommand NewCommand(SqlConnection c,SqlTransaction tx,string sql){SqlCommand cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText=sql;return cmd;}
        private static void AddString(SqlCommand c,string n,int s,string v){c.Parameters.Add(n,SqlDbType.NVarChar,s).Value=v;}
        private static void AddNullableString(SqlCommand c,string n,int s,string v){c.Parameters.Add(n,SqlDbType.NVarChar,s).Value=(object)v??DBNull.Value;}
        private static void AddDate(SqlCommand c,string n,DateTime v){c.Parameters.Add(n,SqlDbType.Date).Value=v.Date;}
        private static void AddBinary(SqlCommand c,string n,byte[] v){c.Parameters.Add(n,SqlDbType.Timestamp,8).Value=v;}
        private static bool IsDuplicate(SqlException e){foreach(SqlError x in e.Errors)if(x.Number==2601||x.Number==2627)return true;return false;}
        private static bool Contains(string s,string v){return s!=null&&s.IndexOf(v,StringComparison.OrdinalIgnoreCase)>=0;}
        private static string Friendly(string f){return f=="Email"?"email address":f=="LicenceNumber"?"licence number":f=="PrdpNumber"?"PrDP number":f=="EmployeeNumber"?"employee number":"identifier";}
    }
}
