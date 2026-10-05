using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using ForteMove.Business.Exceptions;
using ForteMove.Data.Internal;
using ForteMove.Models.Routing;

namespace ForteMove.Data.Repositories
{
    public sealed partial class SqlRouteRepository
    {
        public StopCoordinateDetails GetStopCoordinates(long stopId,long actorUserAccountId)
        {
            using(var connection=new SqlConnection(connectionString)) { connection.Open();return ReadCoordinates(connection,null,stopId,actorUserAccountId); }
        }

        public StopCoordinateSaveResult UpdateStopCoordinates(UpdateStopCoordinatesRequest request,long actorUserAccountId)
        {
            using(var connection=new SqlConnection(connectionString))
            {
                connection.Open();
                using(var transaction=connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        var result=UpdateCoordinatesInTransaction(connection,transaction,request,actorUserAccountId);
                        transaction.Commit();
                        return result;
                    }
                    catch(SqlException ex)
                    {
                        if(ex.Number==51029 || ex.Number==1205 || ex.Number==1222)
                            throw new StopCoordinatePersistenceException("Another operation is updating this service. Refresh the Stop and try again.");
                        throw;
                    }
                }
            }
        }

        // Shares the production transaction boundary with rollback verification, never exposed to Web.
        internal static StopCoordinateSaveResult UpdateCoordinatesInTransaction(SqlConnection connection,SqlTransaction transaction,UpdateStopCoordinatesRequest request,long actorUserAccountId)
        {
                    using(var command=new SqlCommand(@"DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=N'ForteMove.Assignments',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
IF @result<0 THROW 51029,'Could not acquire the operations lock.',1;",connection,transaction))command.ExecuteNonQuery();
                    var current=ReadCoordinates(connection,transaction,request.StopId,actorUserAccountId);
                    if(current==null)throw new StopCoordinatePersistenceException("The Stop is unavailable.");
                    if(!current.RowVersion.SequenceEqual(request.RowVersion))throw new StopCoordinatePersistenceException("The Stop changed. Reload it and review the correction again.");
                    bool changed=current.Latitude!=request.Latitude||current.Longitude!=request.Longitude;
                    if(changed && current.ActiveTripCodes.Count>0 && !request.ActiveTripWarningAcknowledged)
                    { return new StopCoordinateSaveResult {Saved=false,Details=current}; }
                    if(changed)
                    {
                        DateTime utc=DateTime.UtcNow;
                        using(var command=new SqlCommand(@"UPDATE dbo.Stops SET Latitude=@Latitude,Longitude=@Longitude,UpdatedByUserAccountId=@Actor,UpdatedUtc=@Utc
WHERE StopId=@Id AND RowVersion=@Version;",connection,transaction))
                        {
                            var latitude=command.Parameters.Add("@Latitude",SqlDbType.Decimal);latitude.Precision=9;latitude.Scale=6;latitude.Value=(object)request.Latitude??DBNull.Value;
                            var longitude=command.Parameters.Add("@Longitude",SqlDbType.Decimal);longitude.Precision=9;longitude.Scale=6;longitude.Value=(object)request.Longitude??DBNull.Value;
                            command.Parameters.Add("@Actor",SqlDbType.BigInt).Value=actorUserAccountId;
                            var timestamp=command.Parameters.Add("@Utc",SqlDbType.DateTime2);timestamp.Scale=0;timestamp.Value=utc;
                            command.Parameters.Add("@Id",SqlDbType.BigInt).Value=request.StopId;
                            command.Parameters.Add("@Version",SqlDbType.Timestamp,8).Value=request.RowVersion;
                            if(command.ExecuteNonQuery()!=1)throw new StopCoordinatePersistenceException("The Stop changed. Reload it before saving.");
                        }
                        string detail="Stop="+current.StopCode+";OldLatitude="+CoordinateText(current.Latitude)+";OldLongitude="+CoordinateText(current.Longitude)
                            +";NewLatitude="+CoordinateText(request.Latitude)+";NewLongitude="+CoordinateText(request.Longitude)+";StartedUnfinishedTrips="+current.ActiveTripCodes.Count;
                        SqlAuditWriter.Write(connection,transaction,actorUserAccountId,"StopCoordinatesUpdated","Stop",request.StopId.ToString(CultureInfo.InvariantCulture),detail,null,utc);
                    }
                    var updated=ReadCoordinates(connection,transaction,request.StopId,actorUserAccountId);
                    return new StopCoordinateSaveResult {Saved=true,Details=updated};
        }

        private static StopCoordinateDetails ReadCoordinates(SqlConnection connection,SqlTransaction transaction,long stopId,long actor)
        {
            // Recheck role in Data as well as the Admin page; all values are explicit parameters.
            using(var command=new SqlCommand(@"SELECT s.StopId,s.StopCode,s.StopName,s.Area,s.Latitude,s.Longitude,s.RowVersion FROM dbo.Stops s
WHERE s.StopId=@Id AND EXISTS(SELECT 1 FROM dbo.UserAccounts u JOIN dbo.Roles r ON r.RoleId=u.RoleId WHERE u.UserAccountId=@Actor AND u.IsActive=1 AND u.MustChangePassword=0 AND r.IsActive=1 AND r.RoleCode=N'TransportAdministrator');
SELECT r.RouteCode+N' - '+r.RouteName FROM dbo.Routes r JOIN dbo.RouteStops rs ON rs.RouteId=r.RouteId WHERE rs.StopId=@Id ORDER BY r.RouteCode;
SELECT DISTINCT t.TripCode FROM dbo.Trips t JOIN dbo.TripExecutions e ON e.TripId=t.TripId JOIN dbo.RouteStops rs ON rs.RouteId=t.RouteId
WHERE rs.StopId=@Id AND e.ActualCompletionUtc IS NULL AND t.TripStatus NOT IN(N'Completed',N'Cancelled') ORDER BY t.TripCode;",connection,transaction))
            {
                command.Parameters.Add("@Id",SqlDbType.BigInt).Value=stopId;command.Parameters.Add("@Actor",SqlDbType.BigInt).Value=actor;
                using(var reader=command.ExecuteReader())
                {
                    if(!reader.Read())return null;
                    var details=new StopCoordinateDetails {StopId=reader.GetInt64(0),StopCode=reader.GetString(1),StopName=reader.GetString(2),Area=reader.GetString(3),Latitude=reader.IsDBNull(4)?(decimal?)null:reader.GetDecimal(4),Longitude=reader.IsDBNull(5)?(decimal?)null:reader.GetDecimal(5),RowVersion=(byte[])reader.GetValue(6)};
                    reader.NextResult();while(reader.Read())details.Routes.Add(reader.GetString(0));
                    reader.NextResult();while(reader.Read())details.ActiveTripCodes.Add(reader.GetString(0));return details;
                }
            }
        }
        private static string CoordinateText(decimal? value) { return value.HasValue?value.Value.ToString("0.000000",CultureInfo.InvariantCulture):"NULL"; }
    }
}
