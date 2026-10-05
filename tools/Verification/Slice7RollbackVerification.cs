using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using ForteMove.Data.Repositories;
using ForteMove.Models.Routing;
using ForteMove.Models.Tracking;
using ForteMove.Models.Passengers;

internal static class Slice7RollbackVerification
{
    private static int checks;
    private const string ConnectionString=@"Data Source=.\SQLEXPRESS;Initial Catalog=ForteMove;Integrated Security=True;Application Name=ForteMove.Slice7RollbackVerification";
    public static int Main()
    {
        using(var connection=new SqlConnection(ConnectionString))
        {
            connection.Open();
            using(var transaction=connection.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    long trip,assignment,driverUser,otherDriver,passengerUser,otherPassenger,profile,actor,stop;
                    using(var command=Command(connection,transaction,@"
SELECT TOP(1) t.TripId,a.TripAssignmentId,sp.UserAccountId,
 (SELECT MIN(sp2.UserAccountId) FROM dbo.DriverProfiles dp2 JOIN dbo.StaffProfiles sp2 ON sp2.StaffProfileId=dp2.StaffProfileId WHERE dp2.DriverProfileId<>a.DriverProfileId),
 (SELECT MIN(p.UserAccountId) FROM dbo.PassengerProfiles p),
 (SELECT MAX(p.UserAccountId) FROM dbo.PassengerProfiles p),
 (SELECT MIN(p.PassengerProfileId) FROM dbo.PassengerProfiles p),
 (SELECT MIN(u.UserAccountId) FROM dbo.UserAccounts u JOIN dbo.Roles r ON r.RoleId=u.RoleId WHERE r.RoleCode=N'TransportAdministrator'),
 (SELECT MIN(rs.StopId) FROM dbo.RouteStops rs WHERE rs.RouteId=t.RouteId)
FROM dbo.Trips t JOIN dbo.TripAssignments a ON a.TripId=t.TripId AND a.IsCurrent=1 JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=a.DriverProfileId JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId JOIN dbo.UserAccounts u ON u.UserAccountId=sp.UserAccountId
WHERE t.TripStatus=N'Scheduled' AND NOT EXISTS(SELECT 1 FROM dbo.TripExecutions e WHERE e.TripId=t.TripId) ORDER BY t.TripId;"))
                    using(var reader=command.ExecuteReader())
                    {
                        if(!reader.Read()||Enumerable.Range(0,9).Any(reader.IsDBNull))throw new InvalidOperationException("Requires an assigned unstarted Trip, two Drivers, two Passengers and an Administrator.");
                        trip=reader.GetInt64(0);assignment=reader.GetInt64(1);driverUser=reader.GetInt64(2);otherDriver=reader.GetInt64(3);passengerUser=reader.GetInt64(4);otherPassenger=reader.GetInt64(5);profile=reader.GetInt64(6);actor=reader.GetInt64(7);stop=reader.GetInt64(8);
                    }
                    // Set only the fixture's gate state inside this rollback transaction, never a real password.
                    Execute(connection,transaction,@"UPDATE dbo.UserAccounts SET MustChangePassword=0 WHERE UserAccountId=@Id;",driverUser,actor);
                    Execute(connection,transaction,@"UPDATE dbo.UserAccounts SET MustChangePassword=0 WHERE UserAccountId=@Id;",otherDriver,actor);
                    var admin=Read(connection,transaction,TrackingAudience.Administrator,actor,trip);
                    Check(admin!=null&&admin.Stops.Count>=2,"Admin authorized mapping and itinerary");
                    Check(Read(connection,transaction,TrackingAudience.Driver,driverUser,trip)!=null,"Assigned Driver authorized");
                    Check(Read(connection,transaction,TrackingAudience.Driver,otherDriver,trip)==null,"Cross-Driver URL denied");
                    Check(Read(connection,transaction,TrackingAudience.Driver,actor,trip)==null,"Admin cannot impersonate Driver audience");
                    Check(Read(connection,transaction,TrackingAudience.Administrator,passengerUser,trip)==null,"Passenger cannot use Admin audience");
                    Check(Read(connection,transaction,TrackingAudience.Administrator,0,trip)==null,"Anonymous denied in Data");
                    long ticket;
                    using(var command=Command(connection,transaction,@"INSERT dbo.Tickets(TicketCode,PassengerProfileId,TripId,FareAmount,TicketStatus,PurchasedUtc,UpdatedUtc) VALUES(N'TKT-S7-ROLLBACK',@Profile,@Trip,0,N'Purchased',SYSUTCDATETIME(),SYSUTCDATETIME()); SELECT CONVERT(bigint,SCOPE_IDENTITY());"))
                    {command.Parameters.Add("@Profile",SqlDbType.BigInt).Value=profile;command.Parameters.Add("@Trip",SqlDbType.BigInt).Value=trip;ticket=(long)command.ExecuteScalar();}
                    Check(Read(connection,transaction,TrackingAudience.Passenger,passengerUser,ticket)!=null,"Purchased Ticket owner authorized");
                    Check(Read(connection,transaction,TrackingAudience.Passenger,otherPassenger,ticket)==null,"Cross-Passenger Ticket denied");
                    Check(Read(connection,transaction,TrackingAudience.Passenger,driverUser,ticket)==null,"Driver cannot impersonate Passenger audience");
                    Execute(connection,transaction,@"UPDATE dbo.Tickets SET TicketStatus=N'Refunded',RefundedUtc=SYSUTCDATETIME(),RefundedByUserAccountId=@Actor,RefundReason=N'Rollback only' WHERE TicketId=@Id;",ticket,actor);
                    Check(Read(connection,transaction,TrackingAudience.Passenger,passengerUser,ticket).TicketStatus==TicketStatus.Refunded,"Refund state available for terminal domain result");
                    Execute(connection,transaction,@"UPDATE dbo.UserAccounts SET IsActive=0 WHERE UserAccountId=@Id;",driverUser,actor);
                    Check(Read(connection,transaction,TrackingAudience.Driver,driverUser,trip)==null,"Deactivation immediately denies refresh");
                    Execute(connection,transaction,@"UPDATE dbo.UserAccounts SET IsActive=1,MustChangePassword=1 WHERE UserAccountId=@Id;",driverUser,actor);
                    Check(Read(connection,transaction,TrackingAudience.Driver,driverUser,trip)==null,"Forced password change denies refresh");
                    Execute(connection,transaction,@"UPDATE dbo.UserAccounts SET MustChangePassword=0 WHERE UserAccountId=@Id;",driverUser,actor);
                    // Operational fixtures are temporary and roll back; no fake UI action exists.
                    using(var command=Command(connection,transaction,@"
INSERT dbo.PreTripInspections(TripId,TripAssignmentId,DriverProfileId,BusId,ExteriorConditionChecked,TyresSafeChecked,LightsIndicatorsChecked,NoCriticalDashboardWarningsChecked,DoorsOperationalChecked,EmergencyEquipmentPresentChecked,NoBlockingNewDefectChecked,StartOdometerKilometres,ConfirmedUtc)
SELECT TripId,TripAssignmentId,DriverProfileId,BusId,1,1,1,1,1,1,1,0,DATEADD(minute,-31,SYSUTCDATETIME()) FROM dbo.TripAssignments WHERE TripAssignmentId=@Assignment;
DECLARE @Inspection bigint=SCOPE_IDENTITY();
INSERT dbo.TripExecutions(TripId,PreTripInspectionId,TripAssignmentId,DriverProfileId,BusId,ActualStartUtc)
SELECT TripId,@Inspection,TripAssignmentId,DriverProfileId,BusId,DATEADD(minute,-30,SYSUTCDATETIME()) FROM dbo.TripAssignments WHERE TripAssignmentId=@Assignment;
UPDATE dbo.Trips SET TripStatus=N'InProgress' WHERE TripId=@Trip;
INSERT dbo.TripDelayEvents(TripId,TripAssignmentId,DriverProfileId,BusId,DelayPhase,Reason,EstimatedDelayMinutes,ReportedUtc,EndedUtc,EndType,EndedByUserAccountId)
SELECT TripId,TripAssignmentId,DriverProfileId,BusId,N'InTrip',N'PRIVATE delay reason',5,DATEADD(minute,-20,SYSUTCDATETIME()),DATEADD(minute,-15,SYSUTCDATETIME()),N'Resumed',@Actor FROM dbo.TripAssignments WHERE TripAssignmentId=@Assignment;
INSERT dbo.TripCannotProceedReports(TripId,TripAssignmentId,DriverProfileId,BusId,OccurrencePhase,Reason,Note,ReportedUtc)
SELECT TripId,TripAssignmentId,DriverProfileId,BusId,N'InTrip',N'PRIVATE safety reason',N'PRIVATE note',DATEADD(minute,-3,SYSUTCDATETIME()) FROM dbo.TripAssignments WHERE TripAssignmentId=@Assignment;"))
                    {command.Parameters.Add("@Assignment",SqlDbType.BigInt).Value=assignment;command.Parameters.Add("@Trip",SqlDbType.BigInt).Value=trip;command.Parameters.Add("@Actor",SqlDbType.BigInt).Value=actor;command.ExecuteNonQuery();}
                    admin=Read(connection,transaction,TrackingAudience.Administrator,actor,trip);
                    Check(admin.ActualStartUtc.HasValue&&admin.ActualStartUtc.Value.Kind==DateTimeKind.Utc,"Execution mapping uses UTC");
                    Check(admin.Pauses.Count==2&&admin.Pauses.Any(p=>p.IsCannotProceed&&p.EndedUtc==null),"Delay/exception intervals mapped without notes");
                    byte[] originalVersion=Version(connection,transaction,stop);
                    var request=new UpdateStopCoordinatesRequest{StopId=stop,Latitude=-33.123456m,Longitude=27.123456m,RowVersion=originalVersion};
                    var warning=Update(connection,transaction,request,actor);
                    Check(!warning.Saved&&warning.Details.ActiveTripCodes.Contains(admin.TripCode),"Started unfinished Trip warning returned under lock");
                    Check(Version(connection,transaction,stop).SequenceEqual(originalVersion),"Warning cannot persist unconfirmed correction");
                    request.ActiveTripWarningAcknowledged=true;var saved=Update(connection,transaction,request,actor);
                    Check(saved.Saved&&saved.Details.Latitude==request.Latitude,"Acknowledged active-Trip correction permitted");
                    Check(!saved.Details.RowVersion.SequenceEqual(originalVersion),"Coordinate update advances rowversion");
                    bool stale=false;try{Update(connection,transaction,request,actor);}catch(TargetInvocationException e){stale=e.InnerException is ForteMove.Business.Exceptions.StopCoordinatePersistenceException;}
                    Check(stale,"Stale coordinate correction rejected");
                    using(var command=Command(connection,transaction,@"SELECT TOP(1) Detail FROM dbo.AuditEntries WHERE EventType=N'StopCoordinatesUpdated' AND EntityId=CONVERT(nvarchar(100),@Id) ORDER BY AuditEntryId DESC;"))
                    {command.Parameters.Add("@Id",SqlDbType.BigInt).Value=stop;string detail=Convert.ToString(command.ExecuteScalar());Check(detail.Contains("OldLatitude=")&&detail.Contains("OldLongitude=")&&detail.Contains("NewLatitude=-33.123456")&&detail.Contains("NewLongitude=27.123456"),"Audit retains old and new coordinate pairs");}
                    admin=Read(connection,transaction,TrackingAudience.Administrator,actor,trip);Check(admin.Stops.Any(s=>s.Latitude==-33.123456m),"Refresh sees coordinate correction immediately");
                    request.RowVersion=saved.Details.RowVersion;request.Latitude=null;request.Longitude=null;var cleared=Update(connection,transaction,request,actor);Check(cleared.Saved&&cleared.Details.Latitude==null&&cleared.Details.Longitude==null,"Clearing coordinates retains paired nulls");
                    Check(ConstraintRejects(connection,transaction,stop,"UPDATE dbo.Stops SET Latitude=91,Longitude=28 WHERE StopId=@Id;"),"SQL latitude constraint remains authoritative");
                    Check(ConstraintRejects(connection,transaction,stop,"UPDATE dbo.Stops SET Latitude=-33,Longitude=250 WHERE StopId=@Id;"),"SQL longitude constraint remains authoritative");
                    Check(ConstraintRejects(connection,transaction,stop,"UPDATE dbo.Stops SET Latitude=-33,Longitude=NULL WHERE StopId=@Id;"),"SQL coordinate pairing remains authoritative");
                    using(var second=new SqlConnection(ConnectionString))
                    {second.Open();using(var secondTransaction=second.BeginTransaction()){using(var command=Command(second,secondTransaction,@"DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=N'ForteMove.Assignments',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=0;SELECT @result;")){Check(Convert.ToInt32(command.ExecuteScalar())==-1,"Coordinate write serializes with assignment/operation lock");}secondTransaction.Rollback();}}
                    Execute(connection,transaction,@"UPDATE dbo.TripAssignments SET IsCurrent=0,EndType=N'Removed',EndReason=N'Rollback only',EndedByUserAccountId=@Actor,EndedUtc=SYSUTCDATETIME() WHERE TripAssignmentId=@Id;",assignment,actor);
                    Check(Read(connection,transaction,TrackingAudience.Driver,driverUser,trip)==null,"Stale reassigned Driver no longer authorized");
                    transaction.Rollback();
                    Console.WriteLine("Slice 7 SQL rollback verification: {0} assertions passed; every fixture and correction rolled back.",checks);return 0;
                }
                catch(Exception e){if(transaction.Connection!=null)transaction.Rollback();Console.Error.WriteLine(e is TargetInvocationException?e.InnerException:e);return 1;}
            }
        }
    }
    private static void Check(bool condition,string name){if(!condition)throw new InvalidOperationException(name);checks++;}
    private static SqlCommand Command(SqlConnection c,SqlTransaction t,string sql){return new SqlCommand(sql,c,t);}
    private static void Execute(SqlConnection c,SqlTransaction t,string sql,long id,long actor){using(var command=Command(c,t,sql)){command.Parameters.Add("@Id",SqlDbType.BigInt).Value=id;command.Parameters.Add("@Actor",SqlDbType.BigInt).Value=actor;command.ExecuteNonQuery();}}
    private static byte[] Version(SqlConnection c,SqlTransaction t,long stop){using(var command=Command(c,t,"SELECT RowVersion FROM dbo.Stops WHERE StopId=@Id;")){command.Parameters.Add("@Id",SqlDbType.BigInt).Value=stop;return (byte[])command.ExecuteScalar();}}
    private static bool ConstraintRejects(SqlConnection c,SqlTransaction t,long stop,string sql){try{using(var command=Command(c,t,sql)){command.Parameters.Add("@Id",SqlDbType.BigInt).Value=stop;command.ExecuteNonQuery();}return false;}catch(SqlException e){return e.Number==547;}}
    private static TrackingTimeline Read(SqlConnection c,SqlTransaction t,TrackingAudience audience,long user,long id){return (TrackingTimeline)typeof(SqlTrackingRepository).GetMethod("GetTripInTransaction",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{c,t,audience,user,id});}
    private static StopCoordinateSaveResult Update(SqlConnection c,SqlTransaction t,UpdateStopCoordinatesRequest request,long actor){return (StopCoordinateSaveResult)typeof(SqlRouteRepository).GetMethod("UpdateCoordinatesInTransaction",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{c,t,request,actor});}
}
