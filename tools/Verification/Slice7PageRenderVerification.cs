using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Web;
using System.Web.Compilation;
using System.Web.Hosting;
using System.Web.UI;
using ForteMove.Business.Services;
using ForteMove.Data.Repositories;
using ForteMove.Models.Tracking;
using ForteMove.Web.Infrastructure;

// Runs in an isolated copy of the Web application, never installs a test page or login bypass.
public sealed class Slice7PageRenderVerification : MarshalByRefObject
{
    public static int Main(string[] args)
    {
        try
        {
            if(args.Length!=1)throw new ArgumentException("Supply the isolated application directory.");
            string directory=Path.GetFullPath(args[0]).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            var host=(Slice7PageRenderVerification)ApplicationHost.CreateApplicationHost(typeof(Slice7PageRenderVerification),"/",directory);
            Console.WriteLine(host.Verify());
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }

    public string Verify()
    {
        string connection=ConfigurationManager.ConnectionStrings["ForteMove"].ConnectionString;
        long trip,admin;
        using(var sql=new SqlConnection(connection))
        {
            sql.Open();
            using(var command=new SqlCommand(@"SELECT t.TripId,
 (SELECT MIN(u.UserAccountId) FROM dbo.UserAccounts u JOIN dbo.Roles r ON r.RoleId=u.RoleId
  WHERE u.IsActive=1 AND u.MustChangePassword=0 AND r.IsActive=1 AND r.RoleCode=N'TransportAdministrator')
FROM dbo.Trips t WHERE t.TripCode=@Code;",sql))
            {
                command.Parameters.Add("@Code",SqlDbType.NVarChar,20).Value="TR-000006";
                using(var reader=command.ExecuteReader())
                {
                    if(!reader.Read()||reader.IsDBNull(1))throw new InvalidOperationException("Requires TR-000006 and an active Administrator.");
                    trip=reader.GetInt64(0);admin=reader.GetInt64(1);
                }
            }
        }
        var principal=new ForteMovePrincipal(new SqlAuthenticationRepository(connection).GetPrincipalContext(admin));
        var writer=new StringWriter();
        var worker=new SimpleWorkerRequest("Admin/Tracking/TripTracking.aspx","id="+trip.ToString(CultureInfo.InvariantCulture),writer);
        var context=new HttpContext(worker);
        HttpContext.Current=context;
        context.User=principal;
        Thread.CurrentPrincipal=principal;
        var page=(Page)BuildManager.CreateInstanceFromVirtualPath("~/Admin/Tracking/TripTracking.aspx",typeof(Page));
        page.ProcessRequest(context);
        context.Response.Flush();
        string html=writer.ToString();
        Require(context.Response.StatusCode==200,"Actual Trip Tracking page must render successfully.");
        Require(page.Header.Controls.IsReadOnly,"Regression must exercise the real inline-code, read-only master head.");
        Require(html.Contains("leaflet/leaflet.css"),"Declarative Leaflet stylesheet must be rendered.");
        Require(html.Contains("leaflet/leaflet.js")&&html.Contains("fortemove-tracking.js"),"Both scripts must remain registered.");
        Require(html.Contains("Snapshot.ashx?tripId="+trip),"The real tracking panel must point at this Trip's endpoint.");
        var snapshot=new TrackingService(new SqlTrackingRepository(connection)).GetAdminTrackingSnapshot(trip,admin);
        Require(snapshot.UnavailableReason==TrackingUnavailableReason.MissingCoordinates && snapshot.MissingCoordinateCount==5,"The current TR-000006 dataset must report five missing-coordinate Stops.");
        Require(snapshot.Stops.Count==5 && snapshot.CurrentLatitude==null && snapshot.CurrentLongitude==null,"All five ordered Stops must remain available without a Bus marker.");
        Require(snapshot.CanPoll,"Missing-coordinate state must not stop polling.");
        Require(!string.IsNullOrWhiteSpace(snapshot.LastCalculatedLocalTime),"A server refresh time must be available.");
        HttpContext.Current=null;
        return "TR-000006 runtime page rendering passed: read-only master head, local CSS/scripts, five missing Stops, no marker, polling enabled. No database writes.";
    }
    private static void Require(bool pass,string message){if(!pass)throw new InvalidOperationException(message);}
}
