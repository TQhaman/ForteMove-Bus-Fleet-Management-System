using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Identifiers;
using ForteMove.Data.Internal;
using ForteMove.Models.Operations;
using ForteMove.Models.Scheduling;

namespace ForteMove.Data.Repositories
{
    public sealed class SqlTripOperationsRepository : ITripOperationsRepository
    {
        private readonly string connectionString;

        public SqlTripOperationsRepository(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("A connection string is required.", "connectionString");
            this.connectionString = connectionString;
        }

        public IList<DriverTripListItem> GetDriverTrips(long userAccountId, DriverTripBucket bucket, DateTime operationalNow)
        {
            List<DriverTripListItem> items = new List<DriverTripListItem>();
            const string sql = @"
DECLARE @DriverId bigint=(SELECT dp.DriverProfileId FROM dbo.DriverProfiles dp
JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId
JOIN dbo.UserAccounts ua ON ua.UserAccountId=sp.UserAccountId
JOIN dbo.Roles ro ON ro.RoleId=ua.RoleId AND ro.RoleCode=N'Driver'
WHERE ua.UserAccountId=@UserId AND ua.IsActive=1 AND ro.IsActive=1);
SELECT DISTINCT t.TripId,t.TripCode,r.RouteCode,r.RouteName,
 originStop.StopName,destinationStop.StopName,t.ServiceDate,t.ScheduledDepartureTime,t.ExpectedFinishLocal,
 b.FleetNumber,t.TripStatus,te.ActualStartUtc,te.ActualCompletionUtc,t.RequiresReview,
 CONVERT(bit,CASE WHEN cp.TripCannotProceedReportId IS NULL THEN 0 ELSE 1 END),
 CONVERT(bit,CASE WHEN critical.BusDefectReportId IS NULL THEN 0 ELSE 1 END),t.RowVersion,ta.RowVersion,b.RowVersion
FROM dbo.Trips t
JOIN dbo.Routes r ON r.RouteId=t.RouteId
JOIN dbo.TripAssignments ta ON ta.TripId=t.TripId
JOIN dbo.Buses b ON b.BusId=ta.BusId
OUTER APPLY(SELECT TOP(1) s.StopName FROM dbo.RouteStops rs JOIN dbo.Stops s ON s.StopId=rs.StopId WHERE rs.RouteId=t.RouteId ORDER BY rs.StopOrder) originStop
OUTER APPLY(SELECT TOP(1) s.StopName FROM dbo.RouteStops rs JOIN dbo.Stops s ON s.StopId=rs.StopId WHERE rs.RouteId=t.RouteId ORDER BY rs.StopOrder DESC) destinationStop
LEFT JOIN dbo.TripExecutions te ON te.TripId=t.TripId
OUTER APPLY(SELECT TOP(1) x.TripCannotProceedReportId FROM dbo.TripCannotProceedReports x WHERE x.TripId=t.TripId AND x.ResolvedUtc IS NULL) cp
OUTER APPLY(SELECT TOP(1) d.BusDefectReportId FROM dbo.BusDefectReports d WHERE d.BusId=ta.BusId AND d.Severity=N'Critical' AND d.DefectStatus<>N'Resolved') critical
WHERE @DriverId IS NOT NULL
 AND ((ta.IsCurrent=1 AND ta.DriverProfileId=@DriverId)
      OR (te.DriverProfileId=@DriverId AND t.TripStatus=N'Completed')
      OR (ta.DriverProfileId=@DriverId AND ta.EndType=N'Cancelled' AND t.TripStatus=N'Cancelled'))
 AND (
   (@Bucket=N'Today' AND ((t.ServiceDate=@Today AND ta.IsCurrent=1) OR (te.DriverProfileId=@DriverId AND te.ActualStartUtc IS NOT NULL AND te.ActualCompletionUtc IS NULL)))
   OR (@Bucket=N'Upcoming' AND t.ServiceDate>@Today AND ta.IsCurrent=1 AND t.TripStatus NOT IN(N'Completed',N'Cancelled'))
   OR (@Bucket=N'History' AND (t.TripStatus IN(N'Completed',N'Cancelled') OR t.ServiceDate<@Today))
 )
ORDER BY t.ServiceDate,t.ScheduledDepartureTime,t.TripCode;";
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@UserId", SqlDbType.BigInt).Value = userAccountId;
                AddString(command, "@Bucket", 20, bucket.ToString());
                command.Parameters.Add("@Today", SqlDbType.Date).Value = operationalNow.Date;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DriverTripListItem item = new DriverTripListItem
                        {
                            TripId=reader.GetInt64(0), TripCode=reader.GetString(1), RouteCode=reader.GetString(2), RouteName=reader.GetString(3),
                            OriginName=reader.IsDBNull(4)?string.Empty:reader.GetString(4), DestinationName=reader.IsDBNull(5)?string.Empty:reader.GetString(5),
                            ServiceDate=reader.GetDateTime(6), ScheduledDepartureTime=reader.GetTimeSpan(7), ExpectedFinishLocal=reader.GetDateTime(8),
                            FleetNumber=reader.GetString(9), Status=ParseTripStatus(reader.GetString(10)),
                            ActualStartUtc=reader.IsDBNull(11)?(DateTime?)null:reader.GetDateTime(11), ActualCompletionUtc=reader.IsDBNull(12)?(DateTime?)null:reader.GetDateTime(12),
                            RequiresReview=reader.GetBoolean(13), HasOpenCannotProceed=reader.GetBoolean(14), HasUnresolvedCriticalDefect=reader.GetBoolean(15)
                        };
                        item.IsOverdueNotStarted = !item.ActualStartUtc.HasValue && item.ServiceDate.Date.Add(item.ScheduledDepartureTime) < operationalNow &&
                            item.Status != TripStatus.Completed && item.Status != TripStatus.Cancelled;
                        items.Add(item);
                    }
                }
            }
            return items;
        }

        public DriverTripDetails GetDriverTripDetails(long userAccountId, long tripId, DateTime operationalNow)
        {
            DriverTripDetails details = null;
            const string sql = @"
DECLARE @DriverId bigint=(SELECT dp.DriverProfileId FROM dbo.DriverProfiles dp
JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId
JOIN dbo.UserAccounts ua ON ua.UserAccountId=sp.UserAccountId
JOIN dbo.Roles ro ON ro.RoleId=ua.RoleId AND ro.RoleCode=N'Driver'
WHERE ua.UserAccountId=@UserId AND ua.IsActive=1 AND ro.IsActive=1);
SELECT t.TripId,t.TripCode,r.RouteCode,r.RouteName,originStop.StopName,destinationStop.StopName,t.ServiceDate,
 t.ScheduledDepartureTime,t.ExpectedFinishLocal,t.TripStatus,t.RequiresReview,ta.TripAssignmentId,ta.DriverProfileId,
 ta.BusId,b.FleetNumber,b.OdometerKilometres,t.RowVersion,ta.RowVersion,b.RowVersion,
 CONVERT(bit,CASE WHEN critical.BusDefectReportId IS NULL THEN 0 ELSE 1 END)
FROM dbo.Trips t JOIN dbo.Routes r ON r.RouteId=t.RouteId
JOIN dbo.TripAssignments ta ON ta.TripId=t.TripId
JOIN dbo.Buses b ON b.BusId=ta.BusId
OUTER APPLY(SELECT TOP(1) s.StopName FROM dbo.RouteStops rs JOIN dbo.Stops s ON s.StopId=rs.StopId WHERE rs.RouteId=t.RouteId ORDER BY rs.StopOrder) originStop
OUTER APPLY(SELECT TOP(1) s.StopName FROM dbo.RouteStops rs JOIN dbo.Stops s ON s.StopId=rs.StopId WHERE rs.RouteId=t.RouteId ORDER BY rs.StopOrder DESC) destinationStop
OUTER APPLY(SELECT TOP(1) d.BusDefectReportId FROM dbo.BusDefectReports d WHERE d.BusId=ta.BusId AND d.Severity=N'Critical' AND d.DefectStatus<>N'Resolved') critical
LEFT JOIN dbo.TripExecutions te ON te.TripId=t.TripId
WHERE t.TripId=@TripId AND @DriverId IS NOT NULL
 AND ((ta.IsCurrent=1 AND ta.DriverProfileId=@DriverId)
      OR (t.TripStatus=N'Completed' AND te.DriverProfileId=@DriverId AND te.TripAssignmentId=ta.TripAssignmentId)
      OR (t.TripStatus=N'Cancelled' AND ta.DriverProfileId=@DriverId AND ta.EndType=N'Cancelled'));";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    command.Parameters.Add("@UserId", SqlDbType.BigInt).Value=userAccountId;
                    command.Parameters.Add("@TripId", SqlDbType.BigInt).Value=tripId;
                    using (SqlDataReader reader=command.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (!reader.Read()) return null;
                        details=new DriverTripDetails
                        {
                            TripId=reader.GetInt64(0),TripCode=reader.GetString(1),RouteCode=reader.GetString(2),RouteName=reader.GetString(3),
                            OriginName=reader.IsDBNull(4)?string.Empty:reader.GetString(4),DestinationName=reader.IsDBNull(5)?string.Empty:reader.GetString(5),
                            ServiceDate=reader.GetDateTime(6),ScheduledDepartureTime=reader.GetTimeSpan(7),ExpectedFinishLocal=reader.GetDateTime(8),
                            Status=ParseTripStatus(reader.GetString(9)),RequiresReview=reader.GetBoolean(10),TripAssignmentId=reader.GetInt64(11),
                            DriverProfileId=reader.GetInt64(12),BusId=reader.GetInt64(13),FleetNumber=reader.GetString(14),BusOdometerKilometres=reader.GetDecimal(15),
                            TripRowVersion=(byte[])reader.GetValue(16),AssignmentRowVersion=(byte[])reader.GetValue(17),BusRowVersion=(byte[])reader.GetValue(18),
                            HasUnresolvedCriticalDefect=reader.GetBoolean(19)
                        };
                    }
                }
                LoadStops(connection, details);
                LoadInspection(connection, details);
                LoadExecution(connection, details);
                LoadDelays(connection, details);
                LoadCannotProceed(connection, details);
                LoadDefects(connection, details);
                LoadStatusHistory(connection, details);
            }
            return details;
        }

        public void ConfirmReadiness(ConfirmReadinessRequest request,long userAccountId,DateTime operationalNow,DateTime utcNow)
        {
            ExecuteWrite(delegate(SqlConnection c,SqlTransaction tx)
            {
                AcquireAssignmentLock(c,tx);
                const string sql=@"
DECLARE @DriverId bigint,@AssignmentId bigint,@BusId bigint,@OldStatus nvarchar(30),@BusOdo decimal(12,1);
SELECT @DriverId=dp.DriverProfileId,@AssignmentId=ta.TripAssignmentId,@BusId=b.BusId,@OldStatus=t.TripStatus,@BusOdo=b.OdometerKilometres
FROM dbo.Trips t WITH(UPDLOCK,HOLDLOCK)
JOIN dbo.TripAssignments ta WITH(UPDLOCK,HOLDLOCK) ON ta.TripId=t.TripId AND ta.IsCurrent=1
JOIN dbo.DriverProfiles dp WITH(UPDLOCK,HOLDLOCK) ON dp.DriverProfileId=ta.DriverProfileId
JOIN dbo.StaffProfiles sp WITH(UPDLOCK,HOLDLOCK) ON sp.StaffProfileId=dp.StaffProfileId
JOIN dbo.UserAccounts ua WITH(UPDLOCK,HOLDLOCK) ON ua.UserAccountId=sp.UserAccountId
JOIN dbo.Roles ro ON ro.RoleId=ua.RoleId AND ro.RoleCode=N'Driver' AND ro.IsActive=1
JOIN dbo.Buses b WITH(UPDLOCK,HOLDLOCK) ON b.BusId=ta.BusId
JOIN dbo.RouteScheduleVersions rsv ON rsv.RouteScheduleVersionId=t.RouteScheduleVersionId
WHERE t.TripId=@TripId AND t.RowVersion=@TripRv AND ta.RowVersion=@AssignmentRv AND ua.UserAccountId=@UserId AND ua.IsActive=1
 AND sp.EmploymentStatus=N'Active' AND dp.AvailabilityStatus=N'Available' AND t.RequiresReview=0
 AND t.ServiceDate=@OperationalDate AND (t.TripStatus=N'Scheduled' OR (t.TripStatus=N'Delayed' AND NOT EXISTS(SELECT 1 FROM dbo.TripExecutions e WHERE e.TripId=t.TripId)))
 AND dp.LicenceExpiryDate>=CONVERT(date,t.ExpectedFinishLocal) AND dp.PrdpExpiryDate>=CONVERT(date,t.ExpectedFinishLocal)
 AND b.BaseOperationalState=N'Operational' AND b.LicenceExpiryDate>=CONVERT(date,t.ExpectedFinishLocal)
 AND b.RoadworthyExpiryDate>=CONVERT(date,t.ExpectedFinishLocal) AND b.InsuranceExpiryDate>=CONVERT(date,t.ExpectedFinishLocal)
 AND b.GrossVehicleMassKg IS NOT NULL AND (rsv.ExpectedCapacity IS NULL OR b.PassengerCapacity>=rsv.ExpectedCapacity)
 AND DATEADD(year,21,dp.DateOfBirth)<=t.ServiceDate
 AND ((b.GrossVehicleMassKg<=3500) OR (b.GrossVehicleMassKg<=16000 AND dp.LicenceCode IN(N'C1',N'C',N'EC1',N'EC')) OR (b.GrossVehicleMassKg>16000 AND dp.LicenceCode IN(N'C',N'EC')))
 AND NOT EXISTS(SELECT 1 FROM dbo.TripCannotProceedReports x WHERE x.TripId=t.TripId AND x.ResolvedUtc IS NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.BusDefectReports d WHERE d.BusId=b.BusId AND d.Severity=N'Critical' AND d.DefectStatus<>N'Resolved')
 AND NOT EXISTS(SELECT 1 FROM dbo.PreTripInspections p WHERE p.TripId=t.TripId AND p.InvalidatedUtc IS NULL);
IF @DriverId IS NULL THROW 51030,'This Trip is no longer eligible for readiness. Refresh and review its assignment, status, compliance, and safety conditions.',1;
IF @StartOdometer<@BusOdo THROW 51031,'The starting odometer cannot be lower than the Bus current odometer.',1;
INSERT dbo.PreTripInspections(TripId,TripAssignmentId,DriverProfileId,BusId,ExteriorConditionChecked,TyresSafeChecked,LightsIndicatorsChecked,NoCriticalDashboardWarningsChecked,DoorsOperationalChecked,EmergencyEquipmentPresentChecked,NoBlockingNewDefectChecked,StartOdometerKilometres,ConfirmedUtc)
VALUES(@TripId,@AssignmentId,@DriverId,@BusId,1,1,1,1,1,1,1,@StartOdometer,@Utc);
UPDATE dbo.Trips SET TripStatus=CASE WHEN @OldStatus=N'Scheduled' THEN N'Ready' ELSE TripStatus END,
 OperationallyTouchedUtc=COALESCE(OperationallyTouchedUtc,@Utc),OperationallyTouchedByUserAccountId=COALESCE(OperationallyTouchedByUserAccountId,@UserId),UpdatedByUserAccountId=@UserId,UpdatedUtc=@Utc WHERE TripId=@TripId;
IF @OldStatus=N'Scheduled' INSERT dbo.TripStatusHistory(TripId,FromStatus,ToStatus,EventType,OccurredUtc,ActorUserAccountId) VALUES(@TripId,N'Scheduled',N'Ready',N'TripReady',@Utc,@UserId);";
                using(SqlCommand cmd=NewCommand(c,tx,sql))
                { AddCoreActionParameters(cmd,request.TripId,userAccountId,request.TripRowVersion,request.AssignmentRowVersion,operationalNow,utcNow);AddDecimal(cmd,"@StartOdometer",request.StartOdometerKilometres.Value);cmd.ExecuteNonQuery(); }
                SqlAuditWriter.Write(c,tx,userAccountId,"PreTripInspectionCompleted","Trip",request.TripId.ToString(CultureInfo.InvariantCulture),"Checklist completed;StartOdometerKm="+request.StartOdometerKilometres.Value.ToString("0.0",CultureInfo.InvariantCulture),null,utcNow);
                SqlAuditWriter.Write(c,tx,userAccountId,"TripReady","Trip",request.TripId.ToString(CultureInfo.InvariantCulture),"Pre-trip readiness confirmed.",null,utcNow);
            },"Readiness could not be confirmed because the Trip changed.");
        }

        public void StartTrip(TripOperationRequest request,long userAccountId,DateTime operationalNow,DateTime utcNow)
        {
            ExecuteWrite(delegate(SqlConnection c,SqlTransaction tx)
            {
                AcquireAssignmentLock(c,tx);
                const string sql=@"
DECLARE @AssignmentId bigint,@DriverId bigint,@BusId bigint,@InspectionId bigint,@OldStatus nvarchar(30),@Departure datetime2(0);
SELECT @AssignmentId=ta.TripAssignmentId,@DriverId=dp.DriverProfileId,@BusId=b.BusId,@InspectionId=p.PreTripInspectionId,@OldStatus=t.TripStatus,
 @Departure=DATEADD(SECOND,DATEDIFF(SECOND,CONVERT(time(0),'00:00'),t.ScheduledDepartureTime),CONVERT(datetime2(0),t.ServiceDate))
FROM dbo.Trips t WITH(UPDLOCK,HOLDLOCK)
JOIN dbo.TripAssignments ta WITH(UPDLOCK,HOLDLOCK) ON ta.TripId=t.TripId AND ta.IsCurrent=1
JOIN dbo.DriverProfiles dp WITH(UPDLOCK,HOLDLOCK) ON dp.DriverProfileId=ta.DriverProfileId
JOIN dbo.StaffProfiles sp WITH(UPDLOCK,HOLDLOCK) ON sp.StaffProfileId=dp.StaffProfileId
JOIN dbo.UserAccounts ua WITH(UPDLOCK,HOLDLOCK) ON ua.UserAccountId=sp.UserAccountId
JOIN dbo.Roles ro ON ro.RoleId=ua.RoleId AND ro.RoleCode=N'Driver' AND ro.IsActive=1
JOIN dbo.Buses b WITH(UPDLOCK,HOLDLOCK) ON b.BusId=ta.BusId
JOIN dbo.RouteScheduleVersions rsv ON rsv.RouteScheduleVersionId=t.RouteScheduleVersionId
JOIN dbo.PreTripInspections p WITH(UPDLOCK,HOLDLOCK) ON p.TripId=t.TripId AND p.TripAssignmentId=ta.TripAssignmentId AND p.InvalidatedUtc IS NULL
WHERE t.TripId=@TripId AND t.RowVersion=@TripRv AND ta.RowVersion=@AssignmentRv AND ua.UserAccountId=@UserId AND ua.IsActive=1
 AND sp.EmploymentStatus=N'Active' AND dp.AvailabilityStatus=N'Available' AND t.RequiresReview=0
 AND t.ServiceDate=@OperationalDate AND @OperationalNow>=DATEADD(minute,-5,DATEADD(SECOND,DATEDIFF(SECOND,CONVERT(time(0),'00:00'),t.ScheduledDepartureTime),CONVERT(datetime2(0),t.ServiceDate)))
 AND @OperationalNow<DATEADD(day,1,CONVERT(datetime2(0),t.ServiceDate))
 AND (t.TripStatus=N'Ready' OR (t.TripStatus=N'Delayed' AND EXISTS(SELECT 1 FROM dbo.TripDelayEvents d WHERE d.TripId=t.TripId AND d.DelayPhase=N'PreStart' AND d.EndedUtc IS NULL)))
 AND NOT EXISTS(SELECT 1 FROM dbo.TripExecutions e WHERE e.TripId=t.TripId)
 AND NOT EXISTS(SELECT 1 FROM dbo.TripCannotProceedReports x WHERE x.TripId=t.TripId AND x.ResolvedUtc IS NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.BusDefectReports d WHERE d.BusId=b.BusId AND d.Severity=N'Critical' AND d.DefectStatus<>N'Resolved')
 AND b.BaseOperationalState=N'Operational' AND b.LicenceExpiryDate>=CONVERT(date,t.ExpectedFinishLocal) AND b.RoadworthyExpiryDate>=CONVERT(date,t.ExpectedFinishLocal) AND b.InsuranceExpiryDate>=CONVERT(date,t.ExpectedFinishLocal)
 AND b.GrossVehicleMassKg IS NOT NULL AND (rsv.ExpectedCapacity IS NULL OR b.PassengerCapacity>=rsv.ExpectedCapacity)
 AND DATEADD(year,21,dp.DateOfBirth)<=t.ServiceDate
 AND ((b.GrossVehicleMassKg<=3500) OR (b.GrossVehicleMassKg<=16000 AND dp.LicenceCode IN(N'C1',N'C',N'EC1',N'EC')) OR (b.GrossVehicleMassKg>16000 AND dp.LicenceCode IN(N'C',N'EC')))
 AND dp.LicenceExpiryDate>=CONVERT(date,t.ExpectedFinishLocal) AND dp.PrdpExpiryDate>=CONVERT(date,t.ExpectedFinishLocal);
IF @AssignmentId IS NULL THROW 51032,'The Trip cannot be started. Confirm current readiness, service date, assignment, compliance, and safety conditions.',1;
IF EXISTS(SELECT 1 FROM dbo.TripExecutions e JOIN dbo.Trips x ON x.TripId=e.TripId WHERE e.ActualCompletionUtc IS NULL AND e.TripId<>@TripId AND (e.DriverProfileId=@DriverId OR e.BusId=@BusId))
 THROW 51033,'The assigned Driver or Bus is still operating another Trip.',1;
INSERT dbo.TripExecutions(TripId,PreTripInspectionId,TripAssignmentId,DriverProfileId,BusId,ActualStartUtc) VALUES(@TripId,@InspectionId,@AssignmentId,@DriverId,@BusId,@Utc);
UPDATE dbo.TripDelayEvents SET EndedUtc=@Utc,EndType=N'Started',EndedByUserAccountId=@UserId WHERE TripId=@TripId AND EndedUtc IS NULL AND DelayPhase=N'PreStart';
UPDATE dbo.Trips SET TripStatus=N'InProgress',OperationallyTouchedUtc=COALESCE(OperationallyTouchedUtc,@Utc),OperationallyTouchedByUserAccountId=COALESCE(OperationallyTouchedByUserAccountId,@UserId),UpdatedByUserAccountId=@UserId,UpdatedUtc=@Utc WHERE TripId=@TripId;
INSERT dbo.TripStatusHistory(TripId,FromStatus,ToStatus,EventType,OccurredUtc,ActorUserAccountId) VALUES(@TripId,@OldStatus,N'InProgress',N'TripStarted',@Utc,@UserId);";
                using(SqlCommand cmd=NewCommand(c,tx,sql)){AddCoreActionParameters(cmd,request.TripId,userAccountId,request.TripRowVersion,request.AssignmentRowVersion,operationalNow,utcNow);cmd.ExecuteNonQuery();}
                SqlAuditWriter.Write(c,tx,userAccountId,"TripStarted","Trip",request.TripId.ToString(CultureInfo.InvariantCulture),"Actual start recorded.",null,utcNow);
            },"The Trip could not be started because its operational state changed.");
        }

        public void ReportDelay(ReportDelayRequest request,long userAccountId,DateTime operationalNow,DateTime utcNow)
        {
            ExecuteWrite(delegate(SqlConnection c,SqlTransaction tx)
            {
                AcquireAssignmentLock(c,tx);
                const string sql=@"
DECLARE @AssignmentId bigint,@DriverId bigint,@BusId bigint,@OldStatus nvarchar(30),@Phase nvarchar(20);
SELECT @AssignmentId=ta.TripAssignmentId,@DriverId=ta.DriverProfileId,@BusId=ta.BusId,@OldStatus=t.TripStatus,
 @Phase=CASE WHEN EXISTS(SELECT 1 FROM dbo.TripExecutions e WHERE e.TripId=t.TripId) THEN N'InTrip' ELSE N'PreStart' END
FROM dbo.Trips t WITH(UPDLOCK,HOLDLOCK)
JOIN dbo.TripAssignments ta WITH(UPDLOCK,HOLDLOCK) ON ta.TripId=t.TripId AND ta.IsCurrent=1
JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=ta.DriverProfileId JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId
JOIN dbo.UserAccounts ua ON ua.UserAccountId=sp.UserAccountId JOIN dbo.Roles ro ON ro.RoleId=ua.RoleId AND ro.RoleCode=N'Driver'
WHERE t.TripId=@TripId AND t.RowVersion=@TripRv AND ta.RowVersion=@AssignmentRv AND ua.UserAccountId=@UserId AND ua.IsActive=1
 AND t.TripStatus IN(N'Scheduled',N'Ready',N'InProgress') AND t.RequiresReview=0
 AND NOT EXISTS(SELECT 1 FROM dbo.TripCannotProceedReports x WHERE x.TripId=t.TripId AND x.ResolvedUtc IS NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.TripDelayEvents d WHERE d.TripId=t.TripId AND d.EndedUtc IS NULL);
IF @AssignmentId IS NULL THROW 51034,'The delay cannot be reported because the Trip or assignment changed.',1;
INSERT dbo.TripDelayEvents(TripId,TripAssignmentId,DriverProfileId,BusId,DelayPhase,Reason,EstimatedDelayMinutes,ReportedUtc)
VALUES(@TripId,@AssignmentId,@DriverId,@BusId,@Phase,@Reason,@Estimate,@Utc);
UPDATE dbo.Trips SET TripStatus=N'Delayed',OperationallyTouchedUtc=COALESCE(OperationallyTouchedUtc,@Utc),OperationallyTouchedByUserAccountId=COALESCE(OperationallyTouchedByUserAccountId,@UserId),UpdatedByUserAccountId=@UserId,UpdatedUtc=@Utc WHERE TripId=@TripId;
INSERT dbo.TripStatusHistory(TripId,FromStatus,ToStatus,EventType,OccurredUtc,ActorUserAccountId,Note) VALUES(@TripId,@OldStatus,N'Delayed',N'TripDelayReported',@Utc,@UserId,@Reason);";
                using(SqlCommand cmd=NewCommand(c,tx,sql)){AddCoreActionParameters(cmd,request.TripId,userAccountId,request.TripRowVersion,request.AssignmentRowVersion,operationalNow,utcNow);AddString(cmd,"@Reason",500,request.Reason);AddNullableInt(cmd,"@Estimate",request.EstimatedDelayMinutes);cmd.ExecuteNonQuery();}
                SqlAuditWriter.Write(c,tx,userAccountId,"TripDelayReported","Trip",request.TripId.ToString(CultureInfo.InvariantCulture),"Reason="+request.Reason+(request.EstimatedDelayMinutes.HasValue?";EstimatedMinutes="+request.EstimatedDelayMinutes.Value:string.Empty),null,utcNow);
            },"The delay could not be reported because the Trip changed.");
        }

        public void ResumeTrip(TripOperationRequest request,long userAccountId,DateTime operationalNow,DateTime utcNow)
        {
            ExecuteWrite(delegate(SqlConnection c,SqlTransaction tx)
            {
                AcquireAssignmentLock(c,tx);
                const string sql=@"
UPDATE d SET EndedUtc=@Utc,EndType=N'Resumed',EndedByUserAccountId=@UserId
FROM dbo.TripDelayEvents d
JOIN dbo.Trips t WITH(UPDLOCK,HOLDLOCK) ON t.TripId=d.TripId
JOIN dbo.TripAssignments ta WITH(UPDLOCK,HOLDLOCK) ON ta.TripId=t.TripId AND ta.IsCurrent=1
JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=ta.DriverProfileId JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId
JOIN dbo.UserAccounts ua ON ua.UserAccountId=sp.UserAccountId
WHERE t.TripId=@TripId AND t.RowVersion=@TripRv AND ta.RowVersion=@AssignmentRv AND ua.UserAccountId=@UserId
 AND t.TripStatus=N'Delayed' AND d.EndedUtc IS NULL AND d.DelayPhase=N'InTrip'
 AND EXISTS(SELECT 1 FROM dbo.TripExecutions e WHERE e.TripId=t.TripId AND e.ActualCompletionUtc IS NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.TripCannotProceedReports x WHERE x.TripId=t.TripId AND x.ResolvedUtc IS NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.BusDefectReports x WHERE x.BusId=ta.BusId AND x.Severity=N'Critical' AND x.DefectStatus<>N'Resolved');
IF @@ROWCOUNT<>1 THROW 51035,'The Trip cannot be resumed. Refresh and review its delay and safety conditions.',1;
UPDATE dbo.Trips SET TripStatus=N'InProgress',UpdatedByUserAccountId=@UserId,UpdatedUtc=@Utc WHERE TripId=@TripId;
INSERT dbo.TripStatusHistory(TripId,FromStatus,ToStatus,EventType,OccurredUtc,ActorUserAccountId) VALUES(@TripId,N'Delayed',N'InProgress',N'TripResumed',@Utc,@UserId);";
                using(SqlCommand cmd=NewCommand(c,tx,sql)){AddCoreActionParameters(cmd,request.TripId,userAccountId,request.TripRowVersion,request.AssignmentRowVersion,operationalNow,utcNow);cmd.ExecuteNonQuery();}
                SqlAuditWriter.Write(c,tx,userAccountId,"TripResumed","Trip",request.TripId.ToString(CultureInfo.InvariantCulture),"Normal operation resumed.",null,utcNow);
            },"The Trip could not be resumed because its state changed.");
        }

        public void ReportCannotProceed(ReportCannotProceedRequest request,long userAccountId,DateTime operationalNow,DateTime utcNow)
        {
            ExecuteWrite(delegate(SqlConnection c,SqlTransaction tx)
            {
                AcquireAssignmentLock(c,tx);
                const string sql=@"
DECLARE @AssignmentId bigint,@DriverId bigint,@BusId bigint,@Phase nvarchar(20);
SELECT @AssignmentId=ta.TripAssignmentId,@DriverId=ta.DriverProfileId,@BusId=ta.BusId,
 @Phase=CASE WHEN EXISTS(SELECT 1 FROM dbo.TripExecutions e WHERE e.TripId=t.TripId) THEN N'InTrip' ELSE N'PreStart' END
FROM dbo.Trips t WITH(UPDLOCK,HOLDLOCK) JOIN dbo.TripAssignments ta WITH(UPDLOCK,HOLDLOCK) ON ta.TripId=t.TripId AND ta.IsCurrent=1
JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=ta.DriverProfileId JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId JOIN dbo.UserAccounts ua ON ua.UserAccountId=sp.UserAccountId
WHERE t.TripId=@TripId AND t.RowVersion=@TripRv AND ta.RowVersion=@AssignmentRv AND ua.UserAccountId=@UserId
 AND t.TripStatus IN(N'Scheduled',N'Ready',N'InProgress',N'Delayed')
 AND NOT EXISTS(SELECT 1 FROM dbo.TripCannotProceedReports x WHERE x.TripId=t.TripId AND x.ResolvedUtc IS NULL);
IF @AssignmentId IS NULL THROW 51036,'Cannot Proceed could not be reported because the Trip or assignment changed.',1;
INSERT dbo.TripCannotProceedReports(TripId,TripAssignmentId,DriverProfileId,BusId,OccurrencePhase,Reason,Note,ReportedUtc)
VALUES(@TripId,@AssignmentId,@DriverId,@BusId,@Phase,@Reason,@Note,@Utc);
UPDATE dbo.Trips SET OperationallyTouchedUtc=COALESCE(OperationallyTouchedUtc,@Utc),OperationallyTouchedByUserAccountId=COALESCE(OperationallyTouchedByUserAccountId,@UserId),UpdatedByUserAccountId=@UserId,UpdatedUtc=@Utc WHERE TripId=@TripId;";
                using(SqlCommand cmd=NewCommand(c,tx,sql)){AddCoreActionParameters(cmd,request.TripId,userAccountId,request.TripRowVersion,request.AssignmentRowVersion,operationalNow,utcNow);AddString(cmd,"@Reason",500,request.Reason);AddNullableString(cmd,"@Note",1000,request.Note);cmd.ExecuteNonQuery();}
                SqlAuditWriter.Write(c,tx,userAccountId,"TripCannotProceedReported","Trip",request.TripId.ToString(CultureInfo.InvariantCulture),"Reason="+request.Reason,null,utcNow);
            },"Cannot Proceed could not be reported because the Trip changed.");
        }

        public DefectReportResult ReportDefect(ReportDefectRequest request,long userAccountId,DateTime operationalNow,DateTime utcNow)
        {
            DefectReportResult result=null;
            ExecuteWrite(delegate(SqlConnection c,SqlTransaction tx)
            {
                AcquireAssignmentLock(c,tx);
                long sequence;
                using(SqlCommand code=NewCommand(c,tx,"SELECT ISNULL(MAX(TRY_CONVERT(bigint,SUBSTRING(DefectCode,4,20))),0)+1 FROM dbo.BusDefectReports WITH(UPDLOCK,HOLDLOCK) WHERE DefectCode LIKE N'DF-%';")) sequence=Convert.ToInt64(code.ExecuteScalar(),CultureInfo.InvariantCulture);
                string defectCode=IdentifierCodePolicy.FormatDefectCode(sequence);
                const string sql=@"
DECLARE @DriverId bigint,@AssignmentId bigint,@BusId bigint;
SELECT TOP(1) @DriverId=dp.DriverProfileId,@AssignmentId=ta.TripAssignmentId,@BusId=ta.BusId
FROM dbo.Trips t WITH(UPDLOCK,HOLDLOCK)
JOIN dbo.TripAssignments ta WITH(UPDLOCK,HOLDLOCK) ON ta.TripId=t.TripId
JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=ta.DriverProfileId JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId JOIN dbo.UserAccounts ua ON ua.UserAccountId=sp.UserAccountId
LEFT JOIN dbo.TripExecutions te ON te.TripId=t.TripId
WHERE t.TripId=@TripId AND t.RowVersion=@TripRv AND ua.UserAccountId=@UserId
 AND ((ta.IsCurrent=1 AND t.TripStatus IN(N'Scheduled',N'Ready',N'InProgress',N'Delayed')) OR (t.TripStatus=N'Completed' AND te.DriverProfileId=dp.DriverProfileId AND te.TripAssignmentId=ta.TripAssignmentId));
IF @AssignmentId IS NULL THROW 51037,'You are no longer authorised to report a defect for this Trip.',1;
INSERT dbo.BusDefectReports(DefectCode,BusId,TripId,TripAssignmentId,ReportedByDriverProfileId,Category,Severity,Description,DefectStatus,ReportedUtc,UpdatedUtc)
VALUES(@Code,@BusId,@TripId,@AssignmentId,@DriverId,@Category,@Severity,@Description,N'Open',@Utc,@Utc);
SELECT CONVERT(bigint,SCOPE_IDENTITY());
UPDATE dbo.Trips SET OperationallyTouchedUtc=COALESCE(OperationallyTouchedUtc,@Utc),OperationallyTouchedByUserAccountId=COALESCE(OperationallyTouchedByUserAccountId,@UserId),UpdatedByUserAccountId=@UserId,UpdatedUtc=@Utc WHERE TripId=@TripId;";
                long id;
                using(SqlCommand cmd=NewCommand(c,tx,sql))
                { cmd.Parameters.Add("@TripId",SqlDbType.BigInt).Value=request.TripId;cmd.Parameters.Add("@UserId",SqlDbType.BigInt).Value=userAccountId;AddTimestamp(cmd,"@TripRv",request.TripRowVersion);AddString(cmd,"@Code",20,defectCode);AddString(cmd,"@Category",30,request.Category.Value.ToString());AddString(cmd,"@Severity",20,request.Severity.Value.ToString());AddString(cmd,"@Description",1000,request.Description);AddUtc(cmd,"@Utc",utcNow);id=Convert.ToInt64(cmd.ExecuteScalar(),CultureInfo.InvariantCulture); }
                SqlAuditWriter.Write(c,tx,userAccountId,"DefectReported","BusDefectReport",id.ToString(CultureInfo.InvariantCulture),"Code="+defectCode+";Severity="+request.Severity.Value+";Category="+request.Category.Value,null,utcNow);
                result=new DefectReportResult{BusDefectReportId=id,DefectCode=defectCode};
            },"The defect could not be reported because the Trip or assignment changed.");
            return result;
        }

        public void CompleteTrip(CompleteTripRequest request,long userAccountId,DateTime operationalNow,DateTime utcNow)
        {
            ExecuteWrite(delegate(SqlConnection c,SqlTransaction tx)
            {
                AcquireAssignmentLock(c,tx);
                const string sql=@"
DECLARE @ExecutionId bigint,@StartOdo decimal(12,1),@BusOdo decimal(12,1),@OldStatus nvarchar(30),@BusId bigint;
SELECT @ExecutionId=e.TripExecutionId,@StartOdo=p.StartOdometerKilometres,@BusOdo=b.OdometerKilometres,@OldStatus=t.TripStatus,@BusId=b.BusId
FROM dbo.Trips t WITH(UPDLOCK,HOLDLOCK)
JOIN dbo.TripAssignments ta WITH(UPDLOCK,HOLDLOCK) ON ta.TripId=t.TripId AND ta.IsCurrent=1
JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=ta.DriverProfileId JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId JOIN dbo.UserAccounts ua ON ua.UserAccountId=sp.UserAccountId
JOIN dbo.TripExecutions e WITH(UPDLOCK,HOLDLOCK) ON e.TripId=t.TripId AND e.TripAssignmentId=ta.TripAssignmentId AND e.ActualCompletionUtc IS NULL
JOIN dbo.PreTripInspections p ON p.PreTripInspectionId=e.PreTripInspectionId
JOIN dbo.Buses b WITH(UPDLOCK,HOLDLOCK) ON b.BusId=ta.BusId
WHERE t.TripId=@TripId AND t.RowVersion=@TripRv AND ta.RowVersion=@AssignmentRv AND e.RowVersion=@ExecutionRv AND b.RowVersion=@BusRv
 AND ua.UserAccountId=@UserId AND t.TripStatus IN(N'InProgress',N'Delayed')
 AND (t.TripStatus<>N'Delayed' OR EXISTS(SELECT 1 FROM dbo.TripDelayEvents d WHERE d.TripId=t.TripId AND d.EndedUtc IS NULL AND d.DelayPhase=N'InTrip'))
 AND NOT EXISTS(SELECT 1 FROM dbo.TripCannotProceedReports x WHERE x.TripId=t.TripId AND x.ResolvedUtc IS NULL);
IF @ExecutionId IS NULL THROW 51038,'The Trip cannot be completed because its assignment or state changed.',1;
IF @EndOdo<@StartOdo THROW 51039,'The ending odometer cannot be lower than the Trip starting odometer.',1;
IF @EndOdo<@BusOdo THROW 51040,'The ending odometer cannot be lower than the Bus current odometer.',1;
UPDATE dbo.TripExecutions SET ActualCompletionUtc=@Utc,EndOdometerKilometres=@EndOdo,CompletionNote=@Note WHERE TripExecutionId=@ExecutionId;
UPDATE dbo.TripDelayEvents SET EndedUtc=@Utc,EndType=N'Completed',EndedByUserAccountId=@UserId WHERE TripId=@TripId AND EndedUtc IS NULL AND DelayPhase=N'InTrip';
UPDATE dbo.Buses SET OdometerKilometres=@EndOdo,UpdatedByUserAccountId=@UserId,UpdatedUtc=@Utc WHERE BusId=@BusId AND RowVersion=@BusRv;
IF @@ROWCOUNT<>1 THROW 51041,'The Bus odometer changed. Reload the Trip before completing it.',1;
UPDATE dbo.Trips SET TripStatus=N'Completed',UpdatedByUserAccountId=@UserId,UpdatedUtc=@Utc WHERE TripId=@TripId;
INSERT dbo.TripStatusHistory(TripId,FromStatus,ToStatus,EventType,OccurredUtc,ActorUserAccountId,Note) VALUES(@TripId,@OldStatus,N'Completed',N'TripCompleted',@Utc,@UserId,@Note);";
                using(SqlCommand cmd=NewCommand(c,tx,sql)){AddCoreActionParameters(cmd,request.TripId,userAccountId,request.TripRowVersion,request.AssignmentRowVersion,operationalNow,utcNow);AddTimestamp(cmd,"@ExecutionRv",request.RelatedRowVersion);AddTimestamp(cmd,"@BusRv",request.BusRowVersion);AddDecimal(cmd,"@EndOdo",request.EndOdometerKilometres.Value);AddNullableString(cmd,"@Note",1000,request.CompletionNote);cmd.ExecuteNonQuery();}
                SqlAuditWriter.Write(c,tx,userAccountId,"TripCompleted","Trip",request.TripId.ToString(CultureInfo.InvariantCulture),"EndOdometerKm="+request.EndOdometerKilometres.Value.ToString("0.0",CultureInfo.InvariantCulture),null,utcNow);
            },"The Trip could not be completed because its operational data changed.");
        }

        public IList<CannotProceedDetails> GetCannotProceedReports(ExceptionQuery query)
        {
            List<CannotProceedDetails> items=new List<CannotProceedDetails>();
            const string sql=@"SELECT cp.TripCannotProceedReportId,cp.TripId,t.TripCode,r.RouteName,sp.FirstName+N' '+sp.LastName,sp.EmployeeNumber,b.FleetNumber,t.TripStatus,cp.OccurrencePhase,cp.Reason,cp.Note,cp.ReportedUtc,cp.ResolutionType,cp.ResolutionNote,cp.ResolvedUtc,cp.RowVersion,t.RowVersion
FROM dbo.TripCannotProceedReports cp JOIN dbo.Trips t ON t.TripId=cp.TripId JOIN dbo.Routes r ON r.RouteId=t.RouteId
JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=cp.DriverProfileId JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId JOIN dbo.Buses b ON b.BusId=cp.BusId
WHERE (@OpenOnly IS NULL OR @OpenOnly=0 OR cp.ResolvedUtc IS NULL) ORDER BY CASE WHEN cp.ResolvedUtc IS NULL THEN 0 ELSE 1 END,cp.ReportedUtc DESC;";
            using(SqlConnection c=new SqlConnection(connectionString))using(SqlCommand cmd=new SqlCommand(sql,c)){cmd.Parameters.Add("@OpenOnly",SqlDbType.Bit).Value=query!=null&&query.OpenOnly.HasValue?(object)query.OpenOnly.Value:DBNull.Value;c.Open();using(SqlDataReader r=cmd.ExecuteReader())while(r.Read())items.Add(ReadCannotProceed(r));}
            return items;
        }

        public CannotProceedDetails GetCannotProceedDetails(long reportId)
        {
            using(SqlConnection c=new SqlConnection(connectionString))using(SqlCommand cmd=new SqlCommand(@"SELECT cp.TripCannotProceedReportId,cp.TripId,t.TripCode,r.RouteName,sp.FirstName+N' '+sp.LastName,sp.EmployeeNumber,b.FleetNumber,t.TripStatus,cp.OccurrencePhase,cp.Reason,cp.Note,cp.ReportedUtc,cp.ResolutionType,cp.ResolutionNote,cp.ResolvedUtc,cp.RowVersion,t.RowVersion
FROM dbo.TripCannotProceedReports cp JOIN dbo.Trips t ON t.TripId=cp.TripId JOIN dbo.Routes r ON r.RouteId=t.RouteId JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=cp.DriverProfileId JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId JOIN dbo.Buses b ON b.BusId=cp.BusId WHERE cp.TripCannotProceedReportId=@Id;",c)){cmd.Parameters.Add("@Id",SqlDbType.BigInt).Value=reportId;c.Open();using(SqlDataReader r=cmd.ExecuteReader(CommandBehavior.SingleRow)){if(!r.Read())return null;return ReadCannotProceed(r);}}
        }

        public void AllowProceed(ResolveCannotProceedRequest request,long actorUserAccountId,DateTime utcNow)
        {
            ExecuteWrite(delegate(SqlConnection c,SqlTransaction tx){AcquireAssignmentLock(c,tx);using(SqlCommand cmd=NewCommand(c,tx,@"UPDATE cp SET ResolutionType=N'Proceed',ResolutionNote=@Note,ResolvedByUserAccountId=@Actor,ResolvedUtc=@Utc
FROM dbo.TripCannotProceedReports cp JOIN dbo.Trips t WITH(UPDLOCK,HOLDLOCK) ON t.TripId=cp.TripId JOIN dbo.TripAssignments ta ON ta.TripId=t.TripId AND ta.IsCurrent=1
WHERE cp.TripCannotProceedReportId=@Id AND cp.RowVersion=@ReportRv AND t.RowVersion=@TripRv AND cp.ResolvedUtc IS NULL AND t.TripStatus NOT IN(N'Completed',N'Cancelled')
 AND NOT EXISTS(SELECT 1 FROM dbo.BusDefectReports d WHERE d.BusId=ta.BusId AND d.Severity=N'Critical' AND d.DefectStatus<>N'Resolved');
IF @@ROWCOUNT<>1 THROW 51042,'The exception cannot be cleared. Refresh it and resolve any Critical Bus defect first.',1;")){cmd.Parameters.Add("@Id",SqlDbType.BigInt).Value=request.TripCannotProceedReportId;AddString(cmd,"@Note",1000,request.ResolutionNote);cmd.Parameters.Add("@Actor",SqlDbType.BigInt).Value=actorUserAccountId;AddUtc(cmd,"@Utc",utcNow);AddTimestamp(cmd,"@ReportRv",request.ReportRowVersion);AddTimestamp(cmd,"@TripRv",request.TripRowVersion);cmd.ExecuteNonQuery();}SqlAuditWriter.Write(c,tx,actorUserAccountId,"TripExceptionResolved","TripCannotProceedReport",request.TripCannotProceedReportId.ToString(CultureInfo.InvariantCulture),"Resolution=Proceed;Note="+request.ResolutionNote,null,utcNow);},"The exception could not be resolved because it changed.");
        }

        public void CancelTrip(CancelTripRequest request,long actorUserAccountId,DateTime utcNow)
        {
            ExecuteWrite(delegate(SqlConnection c,SqlTransaction tx)
            {
                AcquireAssignmentLock(c,tx);
                const string sql=@"
DECLARE @OldStatus nvarchar(30),@AssignmentId bigint,@AssignmentRv binary(8);
SELECT @OldStatus=t.TripStatus,@AssignmentId=ta.TripAssignmentId,@AssignmentRv=ta.RowVersion
FROM dbo.Trips t WITH(UPDLOCK,HOLDLOCK) LEFT JOIN dbo.TripAssignments ta WITH(UPDLOCK,HOLDLOCK) ON ta.TripId=t.TripId AND ta.IsCurrent=1
WHERE t.TripId=@TripId AND t.RowVersion=@TripRv AND t.TripStatus NOT IN(N'Completed',N'Cancelled');
IF @OldStatus IS NULL THROW 51043,'Only a nonterminal Trip can be cancelled. Refresh and try again.',1;
IF @AssignmentId IS NOT NULL AND (@PostedAssignmentRv IS NULL OR @AssignmentRv<>@PostedAssignmentRv) THROW 51044,'The assignment changed. Reload the Trip before cancelling it.',1;
UPDATE dbo.TripAssignments SET IsCurrent=0,EndType=N'Cancelled',EndReason=@Reason,EndedByUserAccountId=@Actor,EndedUtc=@Utc,UpdatedUtc=@Utc WHERE TripAssignmentId=@AssignmentId;
UPDATE dbo.PreTripInspections SET InvalidatedUtc=@Utc,InvalidatedByUserAccountId=@Actor,InvalidationReason=N'Trip cancelled: '+@Reason WHERE TripId=@TripId AND InvalidatedUtc IS NULL;
UPDATE dbo.TripDelayEvents SET EndedUtc=@Utc,EndType=N'Cancelled',EndedByUserAccountId=@Actor WHERE TripId=@TripId AND EndedUtc IS NULL;
UPDATE dbo.TripCannotProceedReports SET ResolutionType=N'Cancelled',ResolutionNote=@Reason,ResolvedByUserAccountId=@Actor,ResolvedUtc=@Utc WHERE TripId=@TripId AND ResolvedUtc IS NULL;
UPDATE dbo.Trips SET TripStatus=N'Cancelled',OperationallyTouchedUtc=COALESCE(OperationallyTouchedUtc,@Utc),OperationallyTouchedByUserAccountId=COALESCE(OperationallyTouchedByUserAccountId,@Actor),UpdatedByUserAccountId=@Actor,UpdatedUtc=@Utc WHERE TripId=@TripId;
INSERT dbo.TripStatusHistory(TripId,FromStatus,ToStatus,EventType,OccurredUtc,ActorUserAccountId,Note) VALUES(@TripId,@OldStatus,N'Cancelled',N'TripCancelled',@Utc,@Actor,@Reason);";
                using(SqlCommand cmd=NewCommand(c,tx,sql)){cmd.Parameters.Add("@TripId",SqlDbType.BigInt).Value=request.TripId;AddTimestamp(cmd,"@TripRv",request.TripRowVersion);AddNullableTimestamp(cmd,"@PostedAssignmentRv",request.AssignmentRowVersion);AddString(cmd,"@Reason",500,request.Reason);cmd.Parameters.Add("@Actor",SqlDbType.BigInt).Value=actorUserAccountId;AddUtc(cmd,"@Utc",utcNow);cmd.ExecuteNonQuery();}
                SqlAuditWriter.Write(c,tx,actorUserAccountId,"TripCancelled","Trip",request.TripId.ToString(CultureInfo.InvariantCulture),"Reason="+request.Reason,null,utcNow);
            },"The Trip could not be cancelled because it changed.");
        }

        public IList<DefectReportDetails> GetDefectReports(DefectQuery query)
        {
            List<DefectReportDetails> items=new List<DefectReportDetails>();
            const string sql=@"SELECT d.BusDefectReportId,d.DefectCode,d.BusId,d.TripId,t.TripCode,b.FleetNumber,sp.FirstName+N' '+sp.LastName,sp.EmployeeNumber,d.Category,d.Severity,d.Description,d.DefectStatus,d.ReportedUtc,d.ReviewNote,d.ReviewedUtc,d.ResolutionNote,d.ResolvedUtc,d.RowVersion
FROM dbo.BusDefectReports d JOIN dbo.Buses b ON b.BusId=d.BusId JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=d.ReportedByDriverProfileId JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId LEFT JOIN dbo.Trips t ON t.TripId=d.TripId
WHERE (@Status IS NULL OR d.DefectStatus=@Status) AND (@Severity IS NULL OR d.Severity=@Severity)
ORDER BY CASE d.DefectStatus WHEN N'Open' THEN 0 WHEN N'Reviewed' THEN 1 ELSE 2 END,CASE d.Severity WHEN N'Critical' THEN 0 WHEN N'Major' THEN 1 ELSE 2 END,d.ReportedUtc DESC;";
            using(SqlConnection c=new SqlConnection(connectionString))using(SqlCommand cmd=new SqlCommand(sql,c)){AddNullableString(cmd,"@Status",20,query!=null&&query.Status.HasValue?query.Status.Value.ToString():null);AddNullableString(cmd,"@Severity",20,query!=null&&query.Severity.HasValue?query.Severity.Value.ToString():null);c.Open();using(SqlDataReader r=cmd.ExecuteReader())while(r.Read())items.Add(ReadDefect(r));}return items;
        }

        public DefectReportDetails GetDefectDetails(long reportId)
        {
            using(SqlConnection c=new SqlConnection(connectionString))using(SqlCommand cmd=new SqlCommand(@"SELECT d.BusDefectReportId,d.DefectCode,d.BusId,d.TripId,t.TripCode,b.FleetNumber,sp.FirstName+N' '+sp.LastName,sp.EmployeeNumber,d.Category,d.Severity,d.Description,d.DefectStatus,d.ReportedUtc,d.ReviewNote,d.ReviewedUtc,d.ResolutionNote,d.ResolvedUtc,d.RowVersion
FROM dbo.BusDefectReports d JOIN dbo.Buses b ON b.BusId=d.BusId JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=d.ReportedByDriverProfileId JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId LEFT JOIN dbo.Trips t ON t.TripId=d.TripId WHERE d.BusDefectReportId=@Id;",c)){cmd.Parameters.Add("@Id",SqlDbType.BigInt).Value=reportId;c.Open();using(SqlDataReader r=cmd.ExecuteReader(CommandBehavior.SingleRow)){return r.Read()?ReadDefect(r):null;}}
        }

        public void ReviewDefect(UpdateDefectRequest request,long actorUserAccountId,DateTime utcNow)
        {
            UpdateDefect(request,actorUserAccountId,utcNow,false);
        }

        public void ResolveDefect(UpdateDefectRequest request,long actorUserAccountId,DateTime utcNow)
        {
            UpdateDefect(request,actorUserAccountId,utcNow,true);
        }

        private void UpdateDefect(UpdateDefectRequest request,long actor,DateTime utcNow,bool resolve)
        {
            ExecuteWrite(delegate(SqlConnection c,SqlTransaction tx){AcquireAssignmentLock(c,tx);string sql=resolve?@"UPDATE dbo.BusDefectReports SET DefectStatus=N'Resolved',ResolvedByUserAccountId=@Actor,ResolvedUtc=@Utc,ResolutionNote=@Note,UpdatedUtc=@Utc WHERE BusDefectReportId=@Id AND RowVersion=@Rv AND DefectStatus IN(N'Open',N'Reviewed');":@"UPDATE dbo.BusDefectReports SET DefectStatus=N'Reviewed',ReviewedByUserAccountId=@Actor,ReviewedUtc=@Utc,ReviewNote=@Note,UpdatedUtc=@Utc WHERE BusDefectReportId=@Id AND RowVersion=@Rv AND DefectStatus=N'Open';";using(SqlCommand cmd=NewCommand(c,tx,sql)){cmd.Parameters.Add("@Id",SqlDbType.BigInt).Value=request.BusDefectReportId;AddTimestamp(cmd,"@Rv",request.RowVersion);cmd.Parameters.Add("@Actor",SqlDbType.BigInt).Value=actor;AddUtc(cmd,"@Utc",utcNow);AddString(cmd,"@Note",1000,request.Note);if(cmd.ExecuteNonQuery()!=1)throw new TripOperationsPersistenceException("The defect report changed. Refresh it and try again.",null);}SqlAuditWriter.Write(c,tx,actor,resolve?"DefectResolved":"DefectReviewed","BusDefectReport",request.BusDefectReportId.ToString(CultureInfo.InvariantCulture),"Note="+request.Note,null,utcNow);},"The defect report could not be updated because it changed.");
        }

        private static CannotProceedDetails ReadCannotProceed(SqlDataReader r)
        {
            return new CannotProceedDetails{TripCannotProceedReportId=r.GetInt64(0),TripId=r.GetInt64(1),TripCode=r.GetString(2),RouteName=r.GetString(3),DriverName=r.GetString(4),EmployeeNumber=r.GetString(5),FleetNumber=r.GetString(6),TripStatus=ParseTripStatus(r.GetString(7)),Phase=(TripDelayPhase)Enum.Parse(typeof(TripDelayPhase),r.GetString(8)),Reason=r.GetString(9),Note=r.IsDBNull(10)?null:r.GetString(10),ReportedUtc=r.GetDateTime(11),ResolutionType=r.IsDBNull(12)?(CannotProceedResolutionType?)null:(CannotProceedResolutionType)Enum.Parse(typeof(CannotProceedResolutionType),r.GetString(12)),ResolutionNote=r.IsDBNull(13)?null:r.GetString(13),ResolvedUtc=r.IsDBNull(14)?(DateTime?)null:r.GetDateTime(14),RowVersion=(byte[])r.GetValue(15),TripRowVersion=(byte[])r.GetValue(16)};
        }

        private static DefectReportDetails ReadDefect(SqlDataReader r)
        {
            return new DefectReportDetails{BusDefectReportId=r.GetInt64(0),DefectCode=r.GetString(1),BusId=r.GetInt64(2),TripId=r.IsDBNull(3)?(long?)null:r.GetInt64(3),TripCode=r.IsDBNull(4)?null:r.GetString(4),FleetNumber=r.GetString(5),DriverName=r.GetString(6),EmployeeNumber=r.GetString(7),Category=(DefectCategory)Enum.Parse(typeof(DefectCategory),r.GetString(8)),Severity=(DefectSeverity)Enum.Parse(typeof(DefectSeverity),r.GetString(9)),Description=r.GetString(10),Status=(DefectStatus)Enum.Parse(typeof(DefectStatus),r.GetString(11)),ReportedUtc=r.GetDateTime(12),ReviewNote=r.IsDBNull(13)?null:r.GetString(13),ReviewedUtc=r.IsDBNull(14)?(DateTime?)null:r.GetDateTime(14),ResolutionNote=r.IsDBNull(15)?null:r.GetString(15),ResolvedUtc=r.IsDBNull(16)?(DateTime?)null:r.GetDateTime(16),RowVersion=(byte[])r.GetValue(17)};
        }

        private static void LoadStops(SqlConnection c,DriverTripDetails d){using(SqlCommand cmd=new SqlCommand("SELECT s.StopName FROM dbo.Trips t JOIN dbo.RouteStops rs ON rs.RouteId=t.RouteId JOIN dbo.Stops s ON s.StopId=rs.StopId WHERE t.TripId=@TripId ORDER BY rs.StopOrder;",c)){cmd.Parameters.Add("@TripId",SqlDbType.BigInt).Value=d.TripId;using(SqlDataReader r=cmd.ExecuteReader())while(r.Read())d.StopNames.Add(r.GetString(0));}}
        private static void LoadInspection(SqlConnection c,DriverTripDetails d){using(SqlCommand cmd=new SqlCommand("SELECT TOP(1) PreTripInspectionId,StartOdometerKilometres,ConfirmedUtc,InvalidatedUtc,InvalidationReason,RowVersion FROM dbo.PreTripInspections WHERE TripId=@TripId ORDER BY ConfirmedUtc DESC,PreTripInspectionId DESC;",c)){cmd.Parameters.Add("@TripId",SqlDbType.BigInt).Value=d.TripId;using(SqlDataReader r=cmd.ExecuteReader(CommandBehavior.SingleRow))if(r.Read())d.CurrentInspection=new PreTripInspectionDetails{PreTripInspectionId=r.GetInt64(0),StartOdometerKilometres=r.GetDecimal(1),ConfirmedUtc=r.GetDateTime(2),InvalidatedUtc=r.IsDBNull(3)?(DateTime?)null:r.GetDateTime(3),InvalidationReason=r.IsDBNull(4)?null:r.GetString(4),RowVersion=(byte[])r.GetValue(5)};}}
        private static void LoadExecution(SqlConnection c,DriverTripDetails d){using(SqlCommand cmd=new SqlCommand("SELECT TripExecutionId,ActualStartUtc,ActualCompletionUtc,EndOdometerKilometres,CompletionNote,RowVersion FROM dbo.TripExecutions WHERE TripId=@TripId;",c)){cmd.Parameters.Add("@TripId",SqlDbType.BigInt).Value=d.TripId;using(SqlDataReader r=cmd.ExecuteReader(CommandBehavior.SingleRow))if(r.Read())d.Execution=new TripExecutionDetails{TripExecutionId=r.GetInt64(0),ActualStartUtc=r.GetDateTime(1),ActualCompletionUtc=r.IsDBNull(2)?(DateTime?)null:r.GetDateTime(2),EndOdometerKilometres=r.IsDBNull(3)?(decimal?)null:r.GetDecimal(3),CompletionNote=r.IsDBNull(4)?null:r.GetString(4),RowVersion=(byte[])r.GetValue(5)};}}
        private static void LoadDelays(SqlConnection c,DriverTripDetails d){using(SqlCommand cmd=new SqlCommand("SELECT TripDelayEventId,DelayPhase,Reason,EstimatedDelayMinutes,ReportedUtc,EndedUtc,EndType,RowVersion FROM dbo.TripDelayEvents WHERE TripId=@TripId ORDER BY ReportedUtc DESC,TripDelayEventId DESC;",c)){cmd.Parameters.Add("@TripId",SqlDbType.BigInt).Value=d.TripId;using(SqlDataReader r=cmd.ExecuteReader())while(r.Read()){TripDelayDetails x=new TripDelayDetails{TripDelayEventId=r.GetInt64(0),Phase=(TripDelayPhase)Enum.Parse(typeof(TripDelayPhase),r.GetString(1)),Reason=r.GetString(2),EstimatedDelayMinutes=r.IsDBNull(3)?(int?)null:r.GetInt32(3),ReportedUtc=r.GetDateTime(4),EndedUtc=r.IsDBNull(5)?(DateTime?)null:r.GetDateTime(5),EndType=r.IsDBNull(6)?(TripDelayEndType?)null:(TripDelayEndType)Enum.Parse(typeof(TripDelayEndType),r.GetString(6)),RowVersion=(byte[])r.GetValue(7)};d.DelayHistory.Add(x);if(x.IsOpen)d.OpenDelay=x;}}}
        private static void LoadCannotProceed(SqlConnection c,DriverTripDetails d){using(SqlCommand cmd=new SqlCommand("SELECT TOP(1) TripCannotProceedReportId,OccurrencePhase,Reason,Note,ReportedUtc,RowVersion FROM dbo.TripCannotProceedReports WHERE TripId=@TripId AND ResolvedUtc IS NULL;",c)){cmd.Parameters.Add("@TripId",SqlDbType.BigInt).Value=d.TripId;using(SqlDataReader r=cmd.ExecuteReader(CommandBehavior.SingleRow))if(r.Read())d.OpenCannotProceed=new CannotProceedDetails{TripCannotProceedReportId=r.GetInt64(0),TripId=d.TripId,TripCode=d.TripCode,Phase=(TripDelayPhase)Enum.Parse(typeof(TripDelayPhase),r.GetString(1)),Reason=r.GetString(2),Note=r.IsDBNull(3)?null:r.GetString(3),ReportedUtc=r.GetDateTime(4),RowVersion=(byte[])r.GetValue(5)};}}
        private static void LoadDefects(SqlConnection c,DriverTripDetails d){using(SqlCommand cmd=new SqlCommand(@"SELECT x.BusDefectReportId,x.DefectCode,x.BusId,x.TripId,t.TripCode,b.FleetNumber,sp.FirstName+N' '+sp.LastName,sp.EmployeeNumber,x.Category,x.Severity,x.Description,x.DefectStatus,x.ReportedUtc,x.ReviewNote,x.ReviewedUtc,x.ResolutionNote,x.ResolvedUtc,x.RowVersion FROM dbo.BusDefectReports x JOIN dbo.Buses b ON b.BusId=x.BusId JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=x.ReportedByDriverProfileId JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId LEFT JOIN dbo.Trips t ON t.TripId=x.TripId WHERE x.TripId=@TripId ORDER BY x.ReportedUtc DESC;",c)){cmd.Parameters.Add("@TripId",SqlDbType.BigInt).Value=d.TripId;using(SqlDataReader r=cmd.ExecuteReader())while(r.Read())d.DefectHistory.Add(ReadDefect(r));}}
        private static void LoadStatusHistory(SqlConnection c,DriverTripDetails d){using(SqlCommand cmd=new SqlCommand("SELECT FromStatus,ToStatus,EventType,OccurredUtc,Note FROM dbo.TripStatusHistory WHERE TripId=@TripId ORDER BY OccurredUtc,TripStatusHistoryId;",c)){cmd.Parameters.Add("@TripId",SqlDbType.BigInt).Value=d.TripId;using(SqlDataReader r=cmd.ExecuteReader())while(r.Read())d.StatusHistory.Add(new TripStatusHistoryItem{FromStatus=r.IsDBNull(0)?(TripStatus?)null:ParseTripStatus(r.GetString(0)),ToStatus=ParseTripStatus(r.GetString(1)),EventType=r.GetString(2),OccurredUtc=r.GetDateTime(3),Note=r.IsDBNull(4)?null:r.GetString(4)});}}

        private void ExecuteWrite(Action<SqlConnection,SqlTransaction> action,string friendly)
        {
            try{using(SqlConnection c=new SqlConnection(connectionString)){c.Open();using(SqlTransaction tx=c.BeginTransaction(IsolationLevel.Serializable)){action(c,tx);tx.Commit();}}}
            catch(TripOperationsPersistenceException){throw;}
            catch(SqlException ex){throw new TripOperationsPersistenceException(GetFriendly(ex,friendly),ex);}
        }
        private static string GetFriendly(SqlException ex,string fallback){return ex.Number>=51030&&ex.Number<=51099?ex.Message:fallback;}
        private static void AcquireAssignmentLock(SqlConnection c,SqlTransaction tx){using(SqlCommand cmd=NewCommand(c,tx,"DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource=N'ForteMove.Assignments',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;IF @r<0 THROW 51029,'Could not acquire the operations lock.',1;"))cmd.ExecuteNonQuery();}
        private static SqlCommand NewCommand(SqlConnection c,SqlTransaction tx,string sql){SqlCommand cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText=sql;cmd.CommandType=CommandType.Text;return cmd;}
        private static void AddCoreActionParameters(SqlCommand cmd,long tripId,long userId,byte[] tripRv,byte[] assignmentRv,DateTime operationalNow,DateTime utcNow){cmd.Parameters.Add("@TripId",SqlDbType.BigInt).Value=tripId;cmd.Parameters.Add("@UserId",SqlDbType.BigInt).Value=userId;AddTimestamp(cmd,"@TripRv",tripRv);AddTimestamp(cmd,"@AssignmentRv",assignmentRv);cmd.Parameters.Add("@OperationalDate",SqlDbType.Date).Value=operationalNow.Date;SqlParameter now=cmd.Parameters.Add("@OperationalNow",SqlDbType.DateTime2);now.Scale=0;now.Value=operationalNow;AddUtc(cmd,"@Utc",utcNow);}
        private static void AddString(SqlCommand cmd,string name,int size,string value){cmd.Parameters.Add(name,SqlDbType.NVarChar,size).Value=value;}
        private static void AddNullableString(SqlCommand cmd,string name,int size,string value){cmd.Parameters.Add(name,SqlDbType.NVarChar,size).Value=(object)value??DBNull.Value;}
        private static void AddNullableInt(SqlCommand cmd,string name,int? value){cmd.Parameters.Add(name,SqlDbType.Int).Value=value.HasValue?(object)value.Value:DBNull.Value;}
        private static void AddDecimal(SqlCommand cmd,string name,decimal value){SqlParameter p=cmd.Parameters.Add(name,SqlDbType.Decimal);p.Precision=12;p.Scale=1;p.Value=value;}
        private static void AddTimestamp(SqlCommand cmd,string name,byte[] value){cmd.Parameters.Add(name,SqlDbType.Timestamp,8).Value=(object)value??DBNull.Value;}
        private static void AddNullableTimestamp(SqlCommand cmd,string name,byte[] value){cmd.Parameters.Add(name,SqlDbType.Binary,8).Value=(object)value??DBNull.Value;}
        private static void AddUtc(SqlCommand cmd,string name,DateTime value){SqlParameter p=cmd.Parameters.Add(name,SqlDbType.DateTime2);p.Scale=0;p.Value=value;}
        private static TripStatus ParseTripStatus(string value){return (TripStatus)Enum.Parse(typeof(TripStatus),value,false);}
    }
}
