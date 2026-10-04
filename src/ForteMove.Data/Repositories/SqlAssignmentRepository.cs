using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Data.Internal;
using ForteMove.Models.Assignments;
using ForteMove.Models.Drivers;
using ForteMove.Models.Scheduling;

namespace ForteMove.Data.Repositories
{
    public sealed class SqlAssignmentRepository : IAssignmentRepository
    {
        private readonly string connectionString;
        public SqlAssignmentRepository(string connectionString){if(string.IsNullOrWhiteSpace(connectionString))throw new ArgumentException("A connection string is required.","connectionString");this.connectionString=connectionString;}

        public AssignmentData GetAssignmentData(DateTime serviceDate)
        {
            AssignmentData data=new AssignmentData();
            using(SqlConnection c=new SqlConnection(connectionString)){c.Open();
                using(SqlCommand cmd=new SqlCommand(@"SELECT t.TripId,t.TripCode,t.RouteId,r.RouteCode,r.RouteName,t.ServiceDate,t.ScheduledDepartureTime,t.ExpectedFinishLocal,
rsv.PreferredBusCategoryId,rsv.ExpectedCapacity,t.RequiresReview,t.TripStatus,t.RowVersion
FROM dbo.Trips t INNER JOIN dbo.Routes r ON r.RouteId=t.RouteId INNER JOIN dbo.RouteScheduleVersions rsv ON rsv.RouteScheduleVersionId=t.RouteScheduleVersionId
WHERE t.ServiceDate=@Date ORDER BY t.ScheduledDepartureTime,t.TripCode;",c))
                {cmd.Parameters.Add("@Date",SqlDbType.Date).Value=serviceDate.Date;using(SqlDataReader r=cmd.ExecuteReader())while(r.Read())data.Trips.Add(ReadTrip(r));}
                using(SqlCommand cmd=new SqlCommand(@"SELECT b.BusId,b.FleetNumber,b.BusCategoryId,b.PassengerCapacity,b.GrossVehicleMassKg,b.BaseOperationalState,
b.LicenceExpiryDate,b.RoadworthyExpiryDate,b.InsuranceExpiryDate,b.RowVersion
FROM dbo.Buses b ORDER BY b.FleetNumber;",c))
                {using(SqlDataReader r=cmd.ExecuteReader())while(r.Read())data.Buses.Add(new AssignmentBusCandidate{BusId=r.GetInt64(0),FleetNumber=r.GetString(1),BusCategoryId=r.GetInt32(2),PassengerCapacity=r.GetInt16(3),GrossVehicleMassKg=r.IsDBNull(4)?(int?)null:r.GetInt32(4),OperationalState=r.GetString(5),LicenceExpiryDate=r.GetDateTime(6),RoadworthyExpiryDate=r.GetDateTime(7),InsuranceExpiryDate=r.GetDateTime(8),RowVersion=(byte[])r.GetValue(9)});}
                using(SqlCommand cmd=new SqlCommand(@"SELECT dp.DriverProfileId,sp.EmployeeNumber,sp.FirstName+N' '+sp.LastName,dp.DateOfBirth,dp.AvailabilityStatus,dp.LicenceCode,
dp.LicenceExpiryDate,dp.PrdpExpiryDate,ua.IsActive,r.IsActive,sp.EmploymentStatus,ua.RowVersion,sp.RowVersion,dp.RowVersion
FROM dbo.DriverProfiles dp INNER JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId
INNER JOIN dbo.UserAccounts ua ON ua.UserAccountId=sp.UserAccountId INNER JOIN dbo.Roles r ON r.RoleId=ua.RoleId AND r.RoleCode=N'Driver'
ORDER BY sp.EmployeeNumber;",c))
                {using(SqlDataReader r=cmd.ExecuteReader())while(r.Read())data.Drivers.Add(new AssignmentDriverCandidate{DriverProfileId=r.GetInt64(0),EmployeeNumber=r.GetString(1),DriverName=r.GetString(2),DateOfBirth=r.GetDateTime(3),AvailabilityStatus=(DriverAvailabilityStatus)Enum.Parse(typeof(DriverAvailabilityStatus),r.GetString(4),false),LicenceCode=(DriverLicenceCode)Enum.Parse(typeof(DriverLicenceCode),r.GetString(5),false),LicenceExpiryDate=r.GetDateTime(6),PrdpExpiryDate=r.GetDateTime(7),AccountIsActive=r.GetBoolean(8),RoleIsActive=r.GetBoolean(9),EmploymentStatus=r.GetString(10),UserRowVersion=(byte[])r.GetValue(11),StaffRowVersion=(byte[])r.GetValue(12),DriverRowVersion=(byte[])r.GetValue(13)});}
                using(SqlCommand cmd=new SqlCommand(@"SELECT ta.DriverProfileId,ta.BusId,t.TripId,t.ServiceDate,t.ScheduledDepartureTime,t.ExpectedFinishLocal
FROM dbo.TripAssignments ta INNER JOIN dbo.Trips t ON t.TripId=ta.TripId
WHERE ta.IsCurrent=1 AND (t.ServiceDate=@Date OR t.ExpectedFinishLocal>=@DayStart AND t.ExpectedFinishLocal<@DayEnd);",c))
                {cmd.Parameters.Add("@Date",SqlDbType.Date).Value=serviceDate.Date;cmd.Parameters.Add("@DayStart",SqlDbType.DateTime2).Value=serviceDate.Date.AddDays(-1);cmd.Parameters.Add("@DayEnd",SqlDbType.DateTime2).Value=serviceDate.Date.AddDays(2);using(SqlDataReader r=cmd.ExecuteReader())while(r.Read()){AssignmentResourceWindow w=new AssignmentResourceWindow{TripId=r.GetInt64(2),StartLocal=r.GetDateTime(3).Date.Add(r.GetTimeSpan(4)),FinishLocal=r.GetDateTime(5)};AssignmentDriverCandidate d=FindDriver(data,r.GetInt64(0));AssignmentBusCandidate b=FindBus(data,r.GetInt64(1));if(d!=null)d.Windows.Add(w);if(b!=null)b.Windows.Add(w);}}
            }return data;
        }

        public int GetRouteFamiliarity(long driverProfileId,long routeId)
        {using(SqlConnection c=new SqlConnection(connectionString))using(SqlCommand cmd=new SqlCommand(@"SELECT COUNT(*) FROM dbo.TripAssignments ta INNER JOIN dbo.Trips t ON t.TripId=ta.TripId WHERE ta.DriverProfileId=@DriverId AND t.RouteId=@RouteId AND t.TripStatus=N'Completed';",c)){cmd.Parameters.Add("@DriverId",SqlDbType.BigInt).Value=driverProfileId;cmd.Parameters.Add("@RouteId",SqlDbType.BigInt).Value=routeId;c.Open();return Convert.ToInt32(cmd.ExecuteScalar(),CultureInfo.InvariantCulture);}}

        public AssignmentDetails GetDetails(long tripId)
        {
            AssignmentDetails details=new AssignmentDetails();
            using(SqlConnection c=new SqlConnection(connectionString)){c.Open();
                using(SqlCommand cmd=new SqlCommand(@"SELECT t.TripId,t.TripCode,t.RouteId,r.RouteCode,r.RouteName,t.ServiceDate,t.ScheduledDepartureTime,t.ExpectedFinishLocal,
rsv.PreferredBusCategoryId,rsv.ExpectedCapacity,t.RequiresReview,t.TripStatus,t.RowVersion
FROM dbo.Trips t INNER JOIN dbo.Routes r ON r.RouteId=t.RouteId INNER JOIN dbo.RouteScheduleVersions rsv ON rsv.RouteScheduleVersionId=t.RouteScheduleVersionId WHERE t.TripId=@TripId;",c))
                {cmd.Parameters.Add("@TripId",SqlDbType.BigInt).Value=tripId;using(SqlDataReader r=cmd.ExecuteReader(CommandBehavior.SingleRow)){if(!r.Read())return null;details.Trip=ReadTrip(r);}}
                using(SqlCommand cmd=new SqlCommand(@"SELECT ta.TripAssignmentId,ta.DriverProfileId,ta.BusId,sp.FirstName+N' '+sp.LastName,sp.EmployeeNumber,b.FleetNumber,ta.DecisionType,ta.DecisionReason,
ta.AssignedUtc,ta.IsCurrent,ta.EndType,ta.EndReason,ta.EndedUtc,ta.RowVersion
FROM dbo.TripAssignments ta INNER JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=ta.DriverProfileId INNER JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId
INNER JOIN dbo.Buses b ON b.BusId=ta.BusId WHERE ta.TripId=@TripId ORDER BY ta.AssignedUtc DESC,ta.TripAssignmentId DESC;",c))
                {cmd.Parameters.Add("@TripId",SqlDbType.BigInt).Value=tripId;using(SqlDataReader r=cmd.ExecuteReader())while(r.Read()){AssignmentHistoryItem h=new AssignmentHistoryItem{TripAssignmentId=r.GetInt64(0),DriverProfileId=r.GetInt64(1),BusId=r.GetInt64(2),DriverName=r.GetString(3),EmployeeNumber=r.GetString(4),FleetNumber=r.GetString(5),DecisionType=(AssignmentDecisionType)Enum.Parse(typeof(AssignmentDecisionType),r.GetString(6),false),DecisionReason=r.IsDBNull(7)?null:r.GetString(7),AssignedUtc=r.GetDateTime(8),IsCurrent=r.GetBoolean(9),EndType=r.IsDBNull(10)?(AssignmentEndType?)null:(AssignmentEndType)Enum.Parse(typeof(AssignmentEndType),r.GetString(10),false),EndReason=r.IsDBNull(11)?null:r.GetString(11),EndedUtc=r.IsDBNull(12)?(DateTime?)null:r.GetDateTime(12),RowVersion=(byte[])r.GetValue(13)};details.History.Add(h);if(h.IsCurrent)details.CurrentAssignment=h;}}
            }return details;
        }

        public void ConfirmAssignments(ConfirmAssignmentsRequest request,long actorUserAccountId,DateTime operationalNow,DateTime utcNow)
        {ExecuteWrite((c,tx)=>{AcquireLock(c,tx);foreach(ConfirmAssignmentItem item in request.Items)ValidateAndInsert(c,tx,item,actorUserAccountId,operationalNow,utcNow,false,null);},"The assignments could not be confirmed because a Trip, Driver, or bus changed. Review the recommendations and try again.");}

        public void ChangeAssignment(ConfirmAssignmentItem replacement,byte[] currentAssignmentRowVersion,long actorUserAccountId,DateTime operationalNow,DateTime utcNow)
        {ExecuteWrite((c,tx)=>{AcquireLock(c,tx);using(SqlCommand cmd=NewCommand(c,tx,@"UPDATE dbo.TripAssignments SET IsCurrent=0,EndType=N'Changed',EndReason=@Reason,EndedByUserAccountId=@Actor,EndedUtc=@Utc,UpdatedUtc=@Utc
WHERE TripId=@TripId AND IsCurrent=1 AND RowVersion=@AssignmentRv AND EXISTS(SELECT 1 FROM dbo.Trips WHERE TripId=@TripId AND TripStatus=N'Scheduled');")){AddString(cmd,"@Reason",500,replacement.Reason);cmd.Parameters.Add("@Actor",SqlDbType.BigInt).Value=actorUserAccountId;AddUtc(cmd,"@Utc",utcNow);cmd.Parameters.Add("@TripId",SqlDbType.BigInt).Value=replacement.TripId;AddTimestamp(cmd,"@AssignmentRv",currentAssignmentRowVersion);if(cmd.ExecuteNonQuery()!=1)throw new AssignmentPersistenceException("Only a current Scheduled assignment can be changed. Refresh and try again.",null);}ValidateAndInsert(c,tx,replacement,actorUserAccountId,operationalNow,utcNow,true,"AssignmentChanged");},"The assignment could not be changed because its state or resources changed.");}

        public void RemoveAssignment(AssignmentMutationRequest request,long actorUserAccountId,DateTime utcNow)
        {ExecuteWrite((c,tx)=>{AcquireLock(c,tx);using(SqlCommand cmd=NewCommand(c,tx,@"UPDATE dbo.TripAssignments SET IsCurrent=0,EndType=N'Removed',EndReason=@Reason,EndedByUserAccountId=@Actor,EndedUtc=@Utc,UpdatedUtc=@Utc
WHERE TripId=@TripId AND IsCurrent=1 AND RowVersion=@AssignmentRv AND EXISTS(SELECT 1 FROM dbo.Trips WHERE TripId=@TripId AND TripStatus=N'Scheduled' AND RowVersion=@TripRv);
IF @@ROWCOUNT=1 UPDATE dbo.Trips SET TripStatus=N'Unassigned',RequiresReview=0,UpdatedByUserAccountId=@Actor,UpdatedUtc=@Utc WHERE TripId=@TripId;")){AddString(cmd,"@Reason",500,request.Reason.Trim());cmd.Parameters.Add("@Actor",SqlDbType.BigInt).Value=actorUserAccountId;AddUtc(cmd,"@Utc",utcNow);cmd.Parameters.Add("@TripId",SqlDbType.BigInt).Value=request.TripId;AddTimestamp(cmd,"@AssignmentRv",request.AssignmentRowVersion);AddTimestamp(cmd,"@TripRv",request.TripRowVersion);if(cmd.ExecuteNonQuery()<2)throw new AssignmentPersistenceException("Only a current Scheduled assignment can be removed. Refresh and try again.",null);}SqlAuditWriter.Write(c,tx,actorUserAccountId,"AssignmentRemoved","Trip",request.TripId.ToString(CultureInfo.InvariantCulture),"Reason="+request.Reason.Trim(),null,utcNow);},"The assignment could not be removed because it changed.");}

        public void ResolveReview(long tripId,byte[] tripRowVersion,string note,long actorUserAccountId,DateTime utcNow)
        {ExecuteWrite((c,tx)=>{using(SqlCommand cmd=NewCommand(c,tx,"UPDATE dbo.Trips SET RequiresReview=0,UpdatedByUserAccountId=@Actor,UpdatedUtc=@Utc WHERE TripId=@TripId AND RequiresReview=1 AND RowVersion=@Rv;")){cmd.Parameters.Add("@Actor",SqlDbType.BigInt).Value=actorUserAccountId;AddUtc(cmd,"@Utc",utcNow);cmd.Parameters.Add("@TripId",SqlDbType.BigInt).Value=tripId;AddTimestamp(cmd,"@Rv",tripRowVersion);if(cmd.ExecuteNonQuery()!=1)throw new AssignmentPersistenceException("This review item has changed. Refresh and try again.",null);}SqlAuditWriter.Write(c,tx,actorUserAccountId,"AssignmentReviewResolved","Trip",tripId.ToString(CultureInfo.InvariantCulture),"Outcome="+note,null,utcNow);},"The review could not be resolved.");}

        private void ValidateAndInsert(SqlConnection c,SqlTransaction tx,ConfirmAssignmentItem item,long actor,DateTime operationalNow,DateTime utcNow,bool change,string eventType)
        {
            const string sql=@"SELECT COUNT(*) FROM dbo.Trips t WITH(UPDLOCK,HOLDLOCK)
INNER JOIN dbo.RouteScheduleVersions rsv ON rsv.RouteScheduleVersionId=t.RouteScheduleVersionId
INNER JOIN dbo.Buses b WITH(UPDLOCK,HOLDLOCK) ON b.BusId=@BusId
INNER JOIN dbo.DriverProfiles dp WITH(UPDLOCK,HOLDLOCK) ON dp.DriverProfileId=@DriverId
INNER JOIN dbo.StaffProfiles sp WITH(UPDLOCK,HOLDLOCK) ON sp.StaffProfileId=dp.StaffProfileId
INNER JOIN dbo.UserAccounts ua WITH(UPDLOCK,HOLDLOCK) ON ua.UserAccountId=sp.UserAccountId
INNER JOIN dbo.Roles ro ON ro.RoleId=ua.RoleId AND ro.RoleCode=N'Driver'
WHERE t.TripId=@TripId AND t.RowVersion=@TripRv AND t.TripStatus=@RequiredStatus AND t.RequiresReview=0
AND (@IsChange=1 OR DATEADD(SECOND,DATEDIFF(SECOND,CONVERT(time(0),'00:00'),t.ScheduledDepartureTime),CONVERT(datetime2(0),t.ServiceDate))>@OperationalNow)
AND b.RowVersion=@BusRv AND b.BaseOperationalState=N'Operational' AND b.GrossVehicleMassKg IS NOT NULL
AND (rsv.ExpectedCapacity IS NULL OR b.PassengerCapacity>=rsv.ExpectedCapacity)
AND b.LicenceExpiryDate>=CONVERT(date,t.ExpectedFinishLocal) AND b.RoadworthyExpiryDate>=CONVERT(date,t.ExpectedFinishLocal) AND b.InsuranceExpiryDate>=CONVERT(date,t.ExpectedFinishLocal)
AND dp.RowVersion=@DriverRv AND sp.RowVersion=@StaffRv AND ua.RowVersion=@UserRv AND ua.IsActive=1 AND ro.IsActive=1 AND sp.EmploymentStatus=N'Active' AND dp.AvailabilityStatus=N'Available'
AND DATEADD(year,21,dp.DateOfBirth)<=t.ServiceDate AND dp.LicenceExpiryDate>=CONVERT(date,t.ExpectedFinishLocal) AND dp.PrdpExpiryDate>=CONVERT(date,t.ExpectedFinishLocal)
AND ((b.GrossVehicleMassKg<=3500) OR (b.GrossVehicleMassKg<=16000 AND dp.LicenceCode IN(N'C1',N'C',N'EC1',N'EC')) OR (b.GrossVehicleMassKg>16000 AND dp.LicenceCode IN(N'C',N'EC')))
AND NOT EXISTS(SELECT 1 FROM dbo.TripAssignments x INNER JOIN dbo.Trips xt ON xt.TripId=x.TripId WHERE x.IsCurrent=1 AND x.BusId=b.BusId AND x.TripId<>t.TripId
AND DATEADD(SECOND,DATEDIFF(SECOND,CONVERT(time(0),'00:00'),xt.ScheduledDepartureTime),CONVERT(datetime2(0),xt.ServiceDate))<DATEADD(minute,15,t.ExpectedFinishLocal)
AND DATEADD(minute,15,xt.ExpectedFinishLocal)>DATEADD(SECOND,DATEDIFF(SECOND,CONVERT(time(0),'00:00'),t.ScheduledDepartureTime),CONVERT(datetime2(0),t.ServiceDate)))
AND NOT EXISTS(SELECT 1 FROM dbo.TripAssignments x INNER JOIN dbo.Trips xt ON xt.TripId=x.TripId WHERE x.IsCurrent=1 AND x.DriverProfileId=dp.DriverProfileId AND x.TripId<>t.TripId
AND DATEADD(SECOND,DATEDIFF(SECOND,CONVERT(time(0),'00:00'),xt.ScheduledDepartureTime),CONVERT(datetime2(0),xt.ServiceDate))<DATEADD(minute,15,t.ExpectedFinishLocal)
AND DATEADD(minute,15,xt.ExpectedFinishLocal)>DATEADD(SECOND,DATEDIFF(SECOND,CONVERT(time(0),'00:00'),t.ScheduledDepartureTime),CONVERT(datetime2(0),t.ServiceDate)));";
            using(SqlCommand cmd=NewCommand(c,tx,sql)){cmd.Parameters.Add("@TripId",SqlDbType.BigInt).Value=item.TripId;cmd.Parameters.Add("@BusId",SqlDbType.BigInt).Value=item.BusId;cmd.Parameters.Add("@DriverId",SqlDbType.BigInt).Value=item.DriverProfileId;AddTimestamp(cmd,"@TripRv",item.TripRowVersion);AddTimestamp(cmd,"@BusRv",item.BusRowVersion);AddTimestamp(cmd,"@DriverRv",item.DriverRowVersion);AddTimestamp(cmd,"@StaffRv",item.StaffRowVersion);AddTimestamp(cmd,"@UserRv",item.UserRowVersion);AddString(cmd,"@RequiredStatus",30,change?"Scheduled":"Unassigned");cmd.Parameters.Add("@IsChange",SqlDbType.Bit).Value=change;cmd.Parameters.Add("@OperationalNow",SqlDbType.DateTime2).Value=operationalNow;if(Convert.ToInt32(cmd.ExecuteScalar(),CultureInfo.InvariantCulture)!=1)throw new AssignmentPersistenceException("A selected Trip, Driver, or bus is no longer eligible. Nothing was saved.",null);}
            using(SqlCommand cmd=NewCommand(c,tx,@"INSERT dbo.TripAssignments(TripId,DriverProfileId,BusId,IsCurrent,DecisionType,DecisionReason,AssignedByUserAccountId,AssignedUtc,UpdatedUtc)
VALUES(@TripId,@DriverId,@BusId,1,@DecisionType,@Reason,@Actor,@Utc,@Utc);
UPDATE dbo.Trips SET TripStatus=N'Scheduled',RequiresReview=CASE WHEN @IsChange=1 THEN 0 ELSE RequiresReview END,OperationallyTouchedUtc=COALESCE(OperationallyTouchedUtc,@Utc),OperationallyTouchedByUserAccountId=COALESCE(OperationallyTouchedByUserAccountId,@Actor),UpdatedByUserAccountId=@Actor,UpdatedUtc=@Utc WHERE TripId=@TripId;")){cmd.Parameters.Add("@TripId",SqlDbType.BigInt).Value=item.TripId;cmd.Parameters.Add("@DriverId",SqlDbType.BigInt).Value=item.DriverProfileId;cmd.Parameters.Add("@BusId",SqlDbType.BigInt).Value=item.BusId;AddString(cmd,"@DecisionType",30,item.DecisionType.ToString());AddNullableString(cmd,"@Reason",500,string.IsNullOrWhiteSpace(item.Reason)?null:item.Reason.Trim());cmd.Parameters.Add("@Actor",SqlDbType.BigInt).Value=actor;AddUtc(cmd,"@Utc",utcNow);cmd.Parameters.Add("@IsChange",SqlDbType.Bit).Value=change;cmd.ExecuteNonQuery();}
            string auditEvent=eventType??(item.DecisionType==AssignmentDecisionType.AlternativeSelected?"AssignmentOverridden":"AssignmentConfirmed");SqlAuditWriter.Write(c,tx,actor,auditEvent,"Trip",item.TripId.ToString(CultureInfo.InvariantCulture),"DriverProfileId="+item.DriverProfileId+";BusId="+item.BusId+(string.IsNullOrWhiteSpace(item.Reason)?string.Empty:";Reason="+item.Reason.Trim()),null,utcNow);
        }

        private void ExecuteWrite(Action<SqlConnection,SqlTransaction> action,string friendly){try{using(SqlConnection c=new SqlConnection(connectionString)){c.Open();using(SqlTransaction tx=c.BeginTransaction(IsolationLevel.Serializable)){action(c,tx);tx.Commit();}}}catch(AssignmentPersistenceException){throw;}catch(SqlException ex){throw new AssignmentPersistenceException(friendly,ex);}}
        private static void AcquireLock(SqlConnection c,SqlTransaction tx){using(SqlCommand cmd=NewCommand(c,tx,"DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource=N'ForteMove.Assignments',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;IF @r<0 THROW 51013,'Could not acquire assignment lock.',1;"))cmd.ExecuteNonQuery();}
        private static AssignmentTripCandidate ReadTrip(SqlDataReader r){return new AssignmentTripCandidate{TripId=r.GetInt64(0),TripCode=r.GetString(1),RouteId=r.GetInt64(2),RouteCode=r.GetString(3),RouteName=r.GetString(4),ServiceDate=r.GetDateTime(5),ScheduledDepartureTime=r.GetTimeSpan(6),ExpectedFinishLocal=r.GetDateTime(7),PreferredBusCategoryId=r.IsDBNull(8)?(int?)null:r.GetInt32(8),ExpectedCapacity=r.IsDBNull(9)?(int?)null:r.GetInt32(9),RequiresReview=r.GetBoolean(10),Status=(TripStatus)Enum.Parse(typeof(TripStatus),r.GetString(11),false),RowVersion=(byte[])r.GetValue(12)};}
        private static AssignmentDriverCandidate FindDriver(AssignmentData d,long id){foreach(AssignmentDriverCandidate x in d.Drivers)if(x.DriverProfileId==id)return x;return null;}
        private static AssignmentBusCandidate FindBus(AssignmentData d,long id){foreach(AssignmentBusCandidate x in d.Buses)if(x.BusId==id)return x;return null;}
        private static SqlCommand NewCommand(SqlConnection c,SqlTransaction tx,string sql){SqlCommand cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText=sql;return cmd;}
        private static void AddString(SqlCommand c,string n,int s,string v){c.Parameters.Add(n,SqlDbType.NVarChar,s).Value=v;}
        private static void AddNullableString(SqlCommand c,string n,int s,string v){c.Parameters.Add(n,SqlDbType.NVarChar,s).Value=(object)v??DBNull.Value;}
        private static void AddUtc(SqlCommand c,string n,DateTime v){SqlParameter p=c.Parameters.Add(n,SqlDbType.DateTime2);p.Scale=0;p.Value=v;}
        private static void AddTimestamp(SqlCommand c,string n,byte[] v){c.Parameters.Add(n,SqlDbType.Timestamp,8).Value=(object)v??DBNull.Value;}
    }
}
