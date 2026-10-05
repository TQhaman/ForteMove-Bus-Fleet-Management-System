using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using ForteMove.Business.Contracts;
using ForteMove.Models.Passengers;
using ForteMove.Models.Scheduling;
using ForteMove.Models.Tracking;

namespace ForteMove.Data.Repositories
{
    public sealed class SqlTrackingRepository : ITrackingRepository
    {
        private readonly string connectionString;
        public SqlTrackingRepository(string connectionString) { this.connectionString=connectionString; }

        private const string ActiveAccount = @"EXISTS(SELECT 1 FROM dbo.UserAccounts viewer JOIN dbo.Roles role ON role.RoleId=viewer.RoleId
WHERE viewer.UserAccountId=@UserId AND viewer.IsActive=1 AND role.IsActive=1 AND viewer.MustChangePassword=0 AND role.RoleCode=@Role)";

        public TrackingTimeline GetTrip(TrackingAudience audience,long userAccountId,long resourceId)
        {
            return ReadBatch(OwnershipSelection(audience),audience==TrackingAudience.Passenger,TripParameters(audience,userAccountId,resourceId)).FirstOrDefault();
        }

        internal static TrackingTimeline GetTripInTransaction(SqlConnection connection,SqlTransaction transaction,TrackingAudience audience,long userAccountId,long resourceId)
        {
            return ReadBatchInTransaction(connection,transaction,OwnershipSelection(audience),audience==TrackingAudience.Passenger,TripParameters(audience,userAccountId,resourceId)).FirstOrDefault();
        }

        private static string OwnershipSelection(TrackingAudience audience)
        {
            string ownership;
            if(audience==TrackingAudience.Administrator)ownership="t.TripId=@Id";
            else if(audience==TrackingAudience.Driver) ownership=@"t.TripId=@Id AND EXISTS(SELECT 1 FROM dbo.TripAssignments own_assignment
JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=own_assignment.DriverProfileId JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId
WHERE own_assignment.TripId=t.TripId AND own_assignment.IsCurrent=1 AND sp.UserAccountId=@UserId)";
            else ownership=@"EXISTS(SELECT 1 FROM dbo.Tickets ticket JOIN dbo.PassengerProfiles p ON p.PassengerProfileId=ticket.PassengerProfileId
WHERE ticket.TicketId=@Id AND ticket.TripId=t.TripId AND p.UserAccountId=@UserId)";
            return ownership;
        }
        private static Action<SqlCommand> TripParameters(TrackingAudience audience,long userAccountId,long resourceId)
        {
            return cmd=> {
                cmd.Parameters.Add("@UserId",SqlDbType.BigInt).Value=userAccountId;
                cmd.Parameters.Add("@Id",SqlDbType.BigInt).Value=resourceId;
                cmd.Parameters.Add("@Role",SqlDbType.NVarChar,50).Value=audience==TrackingAudience.Administrator?"TransportAdministrator":audience.ToString(); };
        }

        public IList<TrackingTimeline> GetAdminTrips(TrackingQuery query,long userAccountId)
        {
            const string selection=@"(
 (t.ServiceDate=@Date AND (EXISTS(SELECT 1 FROM dbo.TripAssignments a WHERE a.TripId=t.TripId AND a.IsCurrent=1)
    OR (@Completed=1 AND t.TripStatus=N'Completed')) AND (t.TripStatus NOT IN(N'Completed',N'Cancelled') OR (@Completed=1 AND t.TripStatus=N'Completed')))
 OR (t.ServiceDate<@Date AND t.TripStatus NOT IN(N'Completed',N'Cancelled') AND EXISTS(SELECT 1 FROM dbo.TripAssignments a WHERE a.TripId=t.TripId AND a.IsCurrent=1))
 OR (t.TripStatus NOT IN(N'Completed',N'Cancelled') AND EXISTS(SELECT 1 FROM dbo.TripExecutions e WHERE e.TripId=t.TripId AND e.ActualCompletionUtc IS NULL)))";
            return ReadBatch(selection,false,cmd=> {
                cmd.Parameters.Add("@UserId",SqlDbType.BigInt).Value=userAccountId;
                cmd.Parameters.Add("@Role",SqlDbType.NVarChar,50).Value="TransportAdministrator";
                cmd.Parameters.Add("@Date",SqlDbType.Date).Value=query.ServiceDate.Value.Date;
                cmd.Parameters.Add("@Completed",SqlDbType.Bit).Value=query.IncludeCompleted; });
        }

        private IList<TrackingTimeline> ReadBatch(string selection,bool passenger,Action<SqlCommand> parameters)
        {
            using(var connection=new SqlConnection(connectionString))
            {
                connection.Open();
                using(var transaction=connection.BeginTransaction(IsolationLevel.RepeatableRead))
                {
                    var rows=ReadBatchInTransaction(connection,transaction,selection,passenger,parameters);
                    transaction.Commit();
                    return rows;
                }
            }
        }

        internal static IList<TrackingTimeline> ReadBatchInTransaction(SqlConnection connection,SqlTransaction transaction,string selection,bool passenger,Action<SqlCommand> parameters)
        {
            // All interpolated SQL fragments are private constants, never user input.
            string selected="WITH Selected AS(SELECT t.TripId,t.RouteId FROM dbo.Trips t WHERE "+ActiveAccount+" AND "+selection+") ";
            string ticketState=passenger?"(SELECT TicketStatus FROM dbo.Tickets WHERE TicketId=@Id)":"CAST(NULL AS nvarchar(20))";
            string sql=selected+@"
SELECT t.TripId,t.RouteId,t.TripCode,r.RouteCode,r.RouteName,t.ServiceDate,t.ScheduledDepartureTime,t.ExpectedFinishLocal,t.TripStatus,
 e.ActualStartUtc,e.ActualCompletionUtc,CONVERT(bit,CASE WHEN a.TripAssignmentId IS NULL THEN 0 ELSE 1 END),COALESCE(b.FleetNumber,eb.FleetNumber),
 sp.FirstName+N' '+sp.LastName,sp.EmployeeNumber,t.RequiresReview,
 CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.BusDefectReports d WHERE d.BusId=COALESCE(a.BusId,e.BusId) AND d.Severity=N'Critical' AND d.DefectStatus<>N'Resolved') THEN 1 ELSE 0 END),"+ticketState+@"
FROM Selected x JOIN dbo.Trips t ON t.TripId=x.TripId JOIN dbo.Routes r ON r.RouteId=t.RouteId
LEFT JOIN dbo.TripExecutions e ON e.TripId=t.TripId
LEFT JOIN dbo.TripAssignments a ON a.TripId=t.TripId AND a.IsCurrent=1
LEFT JOIN dbo.Buses b ON b.BusId=a.BusId LEFT JOIN dbo.Buses eb ON eb.BusId=e.BusId
LEFT JOIN dbo.DriverProfiles dp ON dp.DriverProfileId=COALESCE(a.DriverProfileId,e.DriverProfileId)
LEFT JOIN dbo.StaffProfiles sp ON sp.StaffProfileId=dp.StaffProfileId
ORDER BY t.ServiceDate,t.ScheduledDepartureTime,t.TripCode;
"+selected+@"
SELECT DISTINCT rs.RouteId,rs.StopOrder,s.StopCode,s.StopName,s.Latitude,s.Longitude,rs.EstimatedMinutesFromOrigin
FROM Selected x JOIN dbo.RouteStops rs ON rs.RouteId=x.RouteId JOIN dbo.Stops s ON s.StopId=rs.StopId ORDER BY rs.RouteId,rs.StopOrder;
"+selected+@"
SELECT d.TripId,d.DelayPhase,d.ReportedUtc,d.EndedUtc,d.EstimatedDelayMinutes FROM Selected x JOIN dbo.TripDelayEvents d ON d.TripId=x.TripId;
"+selected+@"
SELECT cp.TripId,cp.OccurrencePhase,cp.ReportedUtc,cp.ResolvedUtc FROM Selected x JOIN dbo.TripCannotProceedReports cp ON cp.TripId=x.TripId;";
            var rows=new List<TrackingTimeline>();var byId=new Dictionary<long,TrackingTimeline>();var geometry=new Dictionary<long,IList<TrackingStop>>();
                using(var command=new SqlCommand(sql,connection,transaction))
                {
                    parameters(command);
                    using(var reader=command.ExecuteReader())
                    {
                        while(reader.Read()) { var row=new TrackingTimeline {TripId=reader.GetInt64(0),RouteId=reader.GetInt64(1),TripCode=reader.GetString(2),RouteCode=reader.GetString(3),RouteName=reader.GetString(4),
                            ServiceDate=reader.GetDateTime(5),DepartureTime=reader.GetTimeSpan(6),ExpectedFinishLocal=reader.GetDateTime(7),Status=(TripStatus)Enum.Parse(typeof(TripStatus),reader.GetString(8)),
                            ActualStartUtc=Utc(reader,9),ActualCompletionUtc=Utc(reader,10),HasCurrentAssignment=reader.GetBoolean(11),FleetNumber=Text(reader,12),DriverName=Text(reader,13),EmployeeNumber=Text(reader,14),
                            RequiresReview=reader.GetBoolean(15),HasCriticalDefect=reader.GetBoolean(16),TicketStatus=reader.IsDBNull(17)?(TicketStatus?)null:(TicketStatus)Enum.Parse(typeof(TicketStatus),reader.GetString(17))};rows.Add(row);byId.Add(row.TripId,row); }
                        reader.NextResult(); while(reader.Read()) { long route=reader.GetInt64(0);if(!geometry.ContainsKey(route))geometry.Add(route,new List<TrackingStop>());geometry[route].Add(new TrackingStop {
                            Order=reader.GetInt32(1),Code=reader.GetString(2),Name=reader.GetString(3),Latitude=reader.IsDBNull(4)?(decimal?)null:reader.GetDecimal(4),Longitude=reader.IsDBNull(5)?(decimal?)null:reader.GetDecimal(5),EstimatedMinutesFromOrigin=reader.IsDBNull(6)?(int?)null:reader.GetInt32(6)}); }
                        reader.NextResult(); while(reader.Read()) { TrackingTimeline row;if(byId.TryGetValue(reader.GetInt64(0),out row))row.Pauses.Add(new TrackingPause {IsInTrip=reader.GetString(1)=="InTrip",StartedUtc=Utc(reader,2).Value,EndedUtc=Utc(reader,3),EstimatedDelayMinutes=reader.IsDBNull(4)?(int?)null:reader.GetInt32(4)}); }
                        reader.NextResult(); while(reader.Read()) { TrackingTimeline row;if(byId.TryGetValue(reader.GetInt64(0),out row))row.Pauses.Add(new TrackingPause {IsInTrip=reader.GetString(1)=="InTrip",IsCannotProceed=true,StartedUtc=Utc(reader,2).Value,EndedUtc=Utc(reader,3)}); }
                    }
                    foreach(var row in rows) { IList<TrackingStop> stops;if(geometry.TryGetValue(row.RouteId,out stops))row.Stops=stops; }
                }
            return rows;
        }
        private static string Text(SqlDataReader reader,int index) { return reader.IsDBNull(index)?null:reader.GetString(index); }
        private static DateTime? Utc(SqlDataReader reader,int index) { return reader.IsDBNull(index)?(DateTime?)null:DateTime.SpecifyKind(reader.GetDateTime(index),DateTimeKind.Utc); }
    }
}
