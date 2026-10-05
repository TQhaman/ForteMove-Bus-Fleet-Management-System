using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using ForteMove.Business.Exceptions;
namespace ForteMove.Data.Internal
{
    internal static class SqlFuelLifecycle
    {
        internal static void AcquireLock(SqlConnection connection,SqlTransaction transaction)
        {
            using(var command=Command(connection,transaction,@"DECLARE @result int;
EXEC @result=sys.sp_getapplock @Resource=N'ForteMove.FuelVouchers',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000;
IF @result<0 THROW 51101,'Fuel authorizations are busy. Please try again.',1;")) command.ExecuteNonQuery();
        }
        internal static void AcquireAssignmentLock(SqlConnection connection,SqlTransaction transaction)
        {
            using(var command=Command(connection,transaction,@"DECLARE @result int;
EXEC @result=sys.sp_getapplock @Resource=N'ForteMove.Assignments',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000;
IF @result<0 THROW 51101,'Assignments are busy. Please try again.',1;")) command.ExecuteNonQuery();
        }
        internal static void Authorise(SqlConnection connection,SqlTransaction transaction,long userId,bool driver)
        {
            using(var command=Command(connection,transaction,@"SELECT COUNT(*) FROM dbo.UserAccounts u JOIN dbo.Roles r ON r.RoleId=u.RoleId
WHERE u.UserAccountId=@User AND u.IsActive=1 AND u.MustChangePassword=0 AND r.IsActive=1 AND r.RoleCode=@Role;"))
            {
                Id(command,"@User",userId);Text(command,"@Role",50,driver?"Driver":"TransportAdministrator");
                if(Convert.ToInt32(command.ExecuteScalar(),CultureInfo.InvariantCulture)!=1)
                    throw new FuelPersistenceException("Your account is not authorized for this Fuel action.");
            }
        }
        internal static void Invalidate(SqlConnection connection,SqlTransaction transaction,long tripId,long? assignmentId,long actor,string reason,DateTime utc)
        {
            AcquireLock(connection,transaction);
            var events=new List<Tuple<long,string>>();
            using(var command=Command(connection,transaction,@"SELECT FuelRequestId,N'FuelRequestCancelled' FROM dbo.FuelRequests WITH(UPDLOCK,HOLDLOCK)
WHERE TripId=@Trip AND (@Assignment IS NULL OR TripAssignmentId=@Assignment) AND RequestStatus=N'Pending';
SELECT FuelVoucherId,N'FuelVoucherCancelled' FROM dbo.FuelVouchers WITH(UPDLOCK,HOLDLOCK)
WHERE TripId=@Trip AND (@Assignment IS NULL OR TripAssignmentId=@Assignment) AND VoucherStatus=N'Active';"))
            {
                Id(command,"@Trip",tripId);command.Parameters.Add("@Assignment",SqlDbType.BigInt).Value=(object)assignmentId??DBNull.Value;
                using(var reader=command.ExecuteReader()) do { while(reader.Read()) events.Add(Tuple.Create(reader.GetInt64(0),reader.GetString(1))); } while(reader.NextResult());
            }
            using(var command=Command(connection,transaction,@"UPDATE dbo.FuelRequests SET RequestStatus=N'Cancelled',CancelledByUserAccountId=@Actor,CancelledUtc=@Utc,CancellationReason=@Reason,UpdatedUtc=@Utc
WHERE TripId=@Trip AND (@Assignment IS NULL OR TripAssignmentId=@Assignment) AND RequestStatus=N'Pending';
UPDATE dbo.FuelVouchers SET VoucherStatus=N'Cancelled',CancelledByUserAccountId=@Actor,CancelledUtc=@Utc,CancellationReason=@Reason,UpdatedUtc=@Utc
WHERE TripId=@Trip AND (@Assignment IS NULL OR TripAssignmentId=@Assignment) AND VoucherStatus=N'Active';"))
            {
                Id(command,"@Trip",tripId);Id(command,"@Actor",actor);command.Parameters.Add("@Assignment",SqlDbType.BigInt).Value=(object)assignmentId??DBNull.Value;
                Text(command,"@Reason",500,reason);Utc(command,"@Utc",utc);command.ExecuteNonQuery();
            }
            foreach(var item in events) SqlAuditWriter.Write(connection,transaction,actor,item.Item2,item.Item2=="FuelRequestCancelled"?"FuelRequest":"FuelVoucher",item.Item1.ToString(CultureInfo.InvariantCulture),"Reason="+reason,null,utc);
        }
        internal static SqlCommand Command(SqlConnection c,SqlTransaction tx,string sql) { return new SqlCommand(sql,c,tx); }
        internal static void Id(SqlCommand c,string name,long value) { c.Parameters.Add(name,SqlDbType.BigInt).Value=value; }
        internal static void Text(SqlCommand c,string name,int length,string value) { c.Parameters.Add(name,SqlDbType.NVarChar,length).Value=(object)value??DBNull.Value; }
        internal static void Utc(SqlCommand c,string name,DateTime value) { var p=c.Parameters.Add(name,SqlDbType.DateTime2);p.Scale=0;p.Value=value; }
        internal static void Version(SqlCommand c,string name,byte[] value) { c.Parameters.Add(name,SqlDbType.Binary,8).Value=(object)value??DBNull.Value; }
        internal static void Decimal(SqlCommand c,string name,byte precision,byte scale,decimal value) { var p=c.Parameters.Add(name,SqlDbType.Decimal);p.Precision=precision;p.Scale=scale;p.Value=value; }
        internal static bool Same(byte[] a,byte[] b) { if(a==null || b==null || a.Length!=b.Length) return false;int diff=0;for(int i=0;i<a.Length;i++) diff|=a[i]^b[i];return diff==0; }
        internal static long Next(SqlConnection c,SqlTransaction tx,string table,string column,int start)
        {
            // All callers supply compile-time identifiers, never user-derived table/column names.
            using(var command=Command(c,tx,"SELECT COALESCE(MAX(TRY_CONVERT(bigint,SUBSTRING("+column+","+start.ToString(CultureInfo.InvariantCulture)+",25))),0)+1 FROM dbo."+table+" WITH(UPDLOCK,HOLDLOCK);"))
                return Convert.ToInt64(command.ExecuteScalar(),CultureInfo.InvariantCulture);
        }
    }
}
