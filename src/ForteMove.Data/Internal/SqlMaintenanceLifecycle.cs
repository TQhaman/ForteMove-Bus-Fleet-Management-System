using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Fleet;
using ForteMove.Business.Maintenance;
using ForteMove.Business.Time;
using ForteMove.Models.Common;
using ForteMove.Models.Fleet;
using ForteMove.Models.Maintenance;
namespace ForteMove.Data.Internal
{
    internal static class SqlMaintenanceLifecycle
    {
        internal static void AcquireLock(SqlConnection c,SqlTransaction tx)
        {
            using(var cmd=new SqlCommand("DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource=N'ForteMove.Maintenance',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000;IF @r<0 THROW 51201,'Maintenance is busy. Please try again.',1;",c,tx))cmd.ExecuteNonQuery();
        }
        internal static void Authorise(SqlConnection c,SqlTransaction tx,long actor)
        {
            using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.UserAccounts u JOIN dbo.Roles r ON r.RoleId=u.RoleId WHERE u.UserAccountId=@Actor AND u.IsActive=1 AND u.MustChangePassword=0 AND r.IsActive=1 AND r.RoleCode=N'TransportAdministrator';",c,tx))
            {Id(cmd,"@Actor",actor);if(Convert.ToInt32(cmd.ExecuteScalar(),CultureInfo.InvariantCulture)!=1)throw new MaintenancePersistenceException("Your account is not authorized for this maintenance action.");}
        }
        internal static BusSafetyContext ReadSafety(SqlConnection c,SqlTransaction tx,long id)
        {
            var hints=tx==null?"":" WITH(UPDLOCK,HOLDLOCK)";
            var result=new BusSafetyContext();
            using(var cmd=new SqlCommand(@"SELECT b.BusId,b.FleetNumber,b.BusCategoryId,bc.DisplayName,b.PassengerCapacity,b.GrossVehicleMassKg,b.OdometerKilometres,b.LicenceExpiryDate,b.RoadworthyExpiryDate,b.InsuranceExpiryDate,b.BaseOperationalState,b.RowVersion,bc.IsActive,
CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.BusDefectReports d WHERE d.BusId=b.BusId AND d.Severity=N'Critical' AND d.DefectStatus<>N'Resolved') THEN 1 ELSE 0 END),
CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.MaintenanceWorkOrders w WHERE w.BusId=b.BusId AND w.OrderStatus=N'InProgress') THEN 1 ELSE 0 END),
CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.TripExecutions e JOIN dbo.Trips t ON t.TripId=e.TripId WHERE e.BusId=b.BusId AND e.ActualCompletionUtc IS NULL AND t.TripStatus NOT IN(N'Completed',N'Cancelled')) THEN 1 ELSE 0 END)
FROM dbo.Buses b"+hints+" JOIN dbo.BusCategories bc ON bc.BusCategoryId=b.BusCategoryId WHERE b.BusId=@Id;",c,tx))
            {
                Id(cmd,"@Id",id);
                using(var r=cmd.ExecuteReader())
                {
                    if(!r.Read())return null;
                    result.Bus=new BusDetails{BusId=r.GetInt64(0),FleetNumber=r.GetString(1),BusCategoryId=r.GetInt32(2),CategoryName=r.GetString(3),PassengerCapacity=r.GetInt16(4),GrossVehicleMassKg=r.IsDBNull(5)?(int?)null:r.GetInt32(5),OdometerKilometres=r.GetDecimal(6),LicenceExpiryDate=r.GetDateTime(7),RoadworthyExpiryDate=r.GetDateTime(8),InsuranceExpiryDate=r.GetDateTime(9),BaseOperationalState=(BusOperationalState)Enum.Parse(typeof(BusOperationalState),r.GetString(10)),RowVersion=(byte[])r.GetValue(11)};
                    result.CategoryActive=r.GetBoolean(12);result.HasCriticalDefect=r.GetBoolean(13);result.HasInProgressMaintenance=r.GetBoolean(14);result.HasActiveExecution=r.GetBoolean(15);
                }
            }
            using(var cmd=new SqlCommand("SELECT p.*,b.FleetNumber,b.OdometerKilometres AS CurrentOdometer FROM dbo.MaintenancePlans p"+hints+" JOIN dbo.Buses b ON b.BusId=p.BusId WHERE p.BusId=@Id ORDER BY p.MaintenancePlanId;",c,tx))
            {Id(cmd,"@Id",id);using(var r=cmd.ExecuteReader())while(r.Read())result.Plans.Add(ReadPlan(r));}
            return result;
        }
        internal static IList<string> BlockingReasons(SqlConnection c,SqlTransaction tx,long busId,DateTime now)
        {
            var x=ReadSafety(c,tx,busId);
            return x==null?new List<string>{"The bus is unavailable."}:BusSafetyPolicy.MaintenanceBlocks(x.Plans,now,x.Bus.OdometerKilometres);
        }
        internal static void EnsureNewOperation(SqlConnection c,SqlTransaction tx,long busId,DateTime now)
        {
            var e=BlockingReasons(c,tx,busId,now);if(e.Count>0)throw new MaintenancePersistenceException(string.Join(" ",e));
        }
        internal static void LockTripContext(SqlConnection c,SqlTransaction tx,long tripId)
        {
            using(var cmd=new SqlCommand(@"SELECT TripId FROM dbo.Trips WITH(UPDLOCK,HOLDLOCK) WHERE TripId=@Trip;
SELECT a.TripAssignmentId FROM dbo.TripAssignments a WITH(UPDLOCK,HOLDLOCK) WHERE a.TripId=@Trip AND a.IsCurrent=1 ORDER BY a.TripAssignmentId;
SELECT d.DriverProfileId FROM dbo.TripAssignments a JOIN dbo.DriverProfiles d WITH(UPDLOCK,HOLDLOCK) ON d.DriverProfileId=a.DriverProfileId JOIN dbo.StaffProfiles s WITH(UPDLOCK,HOLDLOCK) ON s.StaffProfileId=d.StaffProfileId JOIN dbo.UserAccounts u WITH(UPDLOCK,HOLDLOCK) ON u.UserAccountId=s.UserAccountId WHERE a.TripId=@Trip AND a.IsCurrent=1;",c,tx)){Id(cmd,"@Trip",tripId);cmd.ExecuteNonQuery();}
        }
        internal static void EnsureDriverDeparture(SqlConnection c,SqlTransaction tx,long tripId,long userId,DateTime now)
        {
            LockTripContext(c,tx,tripId);
            using(var cmd=new SqlCommand(@"SELECT a.BusId FROM dbo.TripAssignments a JOIN dbo.DriverProfiles d ON d.DriverProfileId=a.DriverProfileId JOIN dbo.StaffProfiles s ON s.StaffProfileId=d.StaffProfileId
WHERE a.TripId=@Trip AND a.IsCurrent=1 AND s.UserAccountId=@User;",c,tx))
            {Id(cmd,"@Trip",tripId);Id(cmd,"@User",userId);var id=cmd.ExecuteScalar();if(id!=null)EnsureNewOperation(c,tx,Convert.ToInt64(id,CultureInfo.InvariantCulture),now);}
        }
        internal static MaintenancePlan ReadPlan(SqlDataReader r)
        {
            return new MaintenancePlan{MaintenancePlanId=Long(r,"MaintenancePlanId"),PlanCode=String(r,"PlanCode"),BusId=Long(r,"BusId"),FleetNumber=String(r,"FleetNumber"),ServiceName=String(r,"ServiceName"),IntervalDays=NullableInt(r,"IntervalDays"),IntervalKilometres=NullableDecimal(r,"IntervalKilometres"),LastServiceDate=NullableDate(r,"LastServiceDate"),LastServiceOdometer=NullableDecimal(r,"LastServiceOdometer"),NextDueDate=NullableDate(r,"NextDueDate"),NextDueOdometer=NullableDecimal(r,"NextDueOdometer"),LastCompletedWorkOrderId=NullableLong(r,"LastCompletedWorkOrderId"),BlocksOperationWhenOverdue=(bool)r["BlocksOperationWhenOverdue"],IsActive=(bool)r["IsActive"],CurrentOdometer=(decimal)r["CurrentOdometer"],RowVersion=(byte[])r["RowVersion"]};
        }
        internal static void Errors(IEnumerable<ValidationError> e){var errors=e.ToList();if(errors.Count>0)throw new MaintenancePersistenceException(string.Join(" ",errors.Select(x=>x.Message)));}
        internal static bool Same(byte[] a,byte[] b){return SqlFuelLifecycle.Same(a,b);}
        internal static void Id(SqlCommand c,string name,long value){SqlFuelLifecycle.Id(c,name,value);}
        internal static void NullableId(SqlCommand c,string name,long? value){c.Parameters.Add(name,SqlDbType.BigInt).Value=(object)value??DBNull.Value;}
        internal static void Text(SqlCommand c,string name,int size,string value){SqlFuelLifecycle.Text(c,name,size,value);}
        internal static void Utc(SqlCommand c,string name,DateTime value){SqlFuelLifecycle.Utc(c,name,value);}
        internal static void Date(SqlCommand c,string name,DateTime? value){c.Parameters.Add(name,SqlDbType.Date).Value=(object)value??DBNull.Value;}
        internal static void Decimal(SqlCommand c,string name,decimal? value,byte scale=1){var p=c.Parameters.Add(name,SqlDbType.Decimal);p.Precision=12;p.Scale=scale;p.Value=(object)value??DBNull.Value;}
        internal static void Version(SqlCommand c,string name,byte[] value){SqlFuelLifecycle.Version(c,name,value);}
        internal static long Next(SqlConnection c,SqlTransaction tx,string table,string column,int start){return SqlFuelLifecycle.Next(c,tx,table,column,start);}
        internal static string String(SqlDataReader r,string name){return r[name]==DBNull.Value?null:(string)r[name];}
        internal static long Long(SqlDataReader r,string name){return (long)r[name];}
        internal static long? NullableLong(SqlDataReader r,string name){return r[name]==DBNull.Value?(long?)null:(long)r[name];}
        internal static int? NullableInt(SqlDataReader r,string name){return r[name]==DBNull.Value?(int?)null:(int)r[name];}
        internal static decimal? NullableDecimal(SqlDataReader r,string name){return r[name]==DBNull.Value?(decimal?)null:(decimal)r[name];}
        internal static DateTime? NullableDate(SqlDataReader r,string name){return r[name]==DBNull.Value?(DateTime?)null:(DateTime)r[name];}
    }
}
