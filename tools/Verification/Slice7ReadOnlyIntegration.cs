using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;
using ForteMove.Business.Services;
using ForteMove.Business.Time;
using ForteMove.Data.Repositories;
using ForteMove.Models.Fleet;
using ForteMove.Models.Routing;
using ForteMove.Models.Scheduling;
using ForteMove.Models.Security;
using ForteMove.Models.Tracking;
using ForteMove.Web.Infrastructure;
using ForteMove.Web.Tracking;

internal static class Slice7ReadOnlyIntegration
{
    private const string ConnectionString=@"Data Source=.\SQLEXPRESS;Initial Catalog=ForteMove;Integrated Security=True;Application Name=ForteMove.Slice7ReadOnlyVerification";
    private static int checks;
    public static int Main()
    {
        try
        {
            long admin,driver,passenger,trip,route;
            using(var connection=new SqlConnection(ConnectionString))
            {connection.Open();using(var command=new SqlCommand(@"SELECT
 (SELECT MIN(u.UserAccountId) FROM dbo.UserAccounts u JOIN dbo.Roles r ON r.RoleId=u.RoleId WHERE r.RoleCode=N'TransportAdministrator'),
 (SELECT MIN(u.UserAccountId) FROM dbo.UserAccounts u JOIN dbo.Roles r ON r.RoleId=u.RoleId WHERE r.RoleCode=N'Driver'),
 (SELECT MIN(u.UserAccountId) FROM dbo.UserAccounts u JOIN dbo.Roles r ON r.RoleId=u.RoleId WHERE r.RoleCode=N'Passenger'),
 (SELECT MIN(TripId) FROM dbo.Trips), (SELECT MIN(RouteId) FROM dbo.Routes);",connection))using(var reader=command.ExecuteReader()){reader.Read();admin=reader.GetInt64(0);driver=reader.GetInt64(1);passenger=reader.GetInt64(2);trip=reader.GetInt64(3);route=reader.GetInt64(4);}}
            var auth=new SqlAuthenticationRepository(ConnectionString);var principal=auth.GetPrincipalContext(admin);
            Check(AuthenticationService.IsCurrentPrincipalValid(principal),"Existing Admin principal reload");
            Check(new SqlBusRepository(ConnectionString).GetFleetList(new FleetQuery()).Count>0,"Fleet mapping regression");
            var routes=new SqlRouteRepository(ConnectionString);Check(routes.GetRouteList(new RouteQuery()).Count>0,"Route list regression");
            Check(routes.GetRouteDetails(route).Stops.Count>=2,"Route details with new coordinate mapping");
            Check(new SqlDriverRepository(ConnectionString).GetDrivers().Count>0,"Driver list regression");
            var schedules=new SqlSchedulingRepository(ConnectionString);Check(schedules.GetTripList(new TripQuery()).Count>0,"Trips mapping regression");
            Check(new DashboardService(new SqlDashboardRepository(ConnectionString)).GetSummary()!=null,"Dashboard regression");
            Check(new SqlTripOperationsRepository(ConnectionString).GetDefectReports(new ForteMove.Models.Operations.DefectQuery())!=null,"Defects regression");
            Check(new SqlPassengerRepository(ConnectionString).GetAccount(passenger)!=null,"Passenger profile regression");
            var service=new TrackingService(new SqlTrackingRepository(ConnectionString));var snapshot=service.GetAdminTrackingSnapshot(trip,admin);
            Check(snapshot!=null,"Live tracking repository maps existing Trip");
            Check(snapshot.CanPoll&&snapshot.UnavailableReason==TrackingUnavailableReason.MissingCoordinates,"Current missing geography remains refreshable domain result");
            var rows=service.GetAdminActiveTrips(new TrackingQuery{ServiceDate=new DateTime(2026,10,6)},admin);Check(rows.Count>0,"Admin active-service batch maps assignments");
            Check(service.GetAdminTrackingSnapshot(trip,passenger)==null,"Data refuses Passenger Admin projection");
            Request(null,"tripId="+trip,401);
            Request(auth.GetPrincipalContext(passenger),"list=1&date=2026-10-06",403);
            Request(auth.GetPrincipalContext(passenger),"tripId="+trip,400);
            Request(auth.GetPrincipalContext(passenger),"ticketId=9223372036854775807",404);
            Request(principal,"tripId=abc",400);
            Request(principal,"ticketId=1",400);
            var response=Request(principal,"tripId="+trip,200);
            Check(response.Contains("MissingCoordinateCount")&&!response.Contains("Password"),"Endpoint structured unavailable JSON");
            var dp=auth.GetPrincipalContext(driver);if(dp.MustChangePassword)Request(dp,"tripId="+trip,403);
            Request(principal,"list=1&date=not-a-date",400);
            Request(principal,"list=1&date=2026-10-06",200);
            Console.WriteLine("Slice 7 read-only integration: {0} checks passed; no writes or login attempts.",checks);return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static string Request(PrincipalContext principal,string query,int expected)
    {
        var writer=new StringWriter();var context=new HttpContext(new HttpRequest("","https://localhost/Tracking/Snapshot.ashx",query),new HttpResponse(writer));
        if(principal!=null)context.User=new ForteMovePrincipal(principal);
        new SnapshotHandler().ProcessRequest(context);
        Check(context.Response.StatusCode==expected,"Handler status "+expected+" for "+query);
        Check(context.Response.ContentType=="application/json","Handler stays JSON, not a login redirect");
        new JavaScriptSerializer().DeserializeObject(writer.ToString());return writer.ToString();
    }
    private static void Check(bool pass,string name){if(!pass)throw new InvalidOperationException(name);checks++;}
}
