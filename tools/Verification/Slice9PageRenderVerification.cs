using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using ForteMove.Models.Maintenance;
using System.Threading;
using System.Web;
using System.Web.Compilation;
using System.Web.Hosting;
using System.Web.UI;
using System.Web.SessionState;
using ForteMove.Data.Repositories;
using ForteMove.Web.Infrastructure;
using ForteMove.Web.Driver.Fuel;
using ForteMove.Business.Fuel;

public sealed class Slice9PageRenderVerification:MarshalByRefObject
{
    int checks;
    public static int Main(string[] args)
    {
        try {
            if(args.Length!=1)throw new Exception("Supply isolated application directory.");
            var host=(Slice9PageRenderVerification)ApplicationHost.CreateApplicationHost(typeof(Slice9PageRenderVerification),"/",Path.GetFullPath(args[0]).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar);
            Console.WriteLine(host.Verify());return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    public string Verify()
    {
        try { return VerifyCore(); }
        catch(Exception error) { throw new InvalidOperationException(error.ToString()); }
    }
    private string VerifyCore()
    {
        string cs=ConfigurationManager.ConnectionStrings["ForteMove"].ConnectionString;
        long admin=Id(cs,"TransportAdministrator"),driver=Id(cs,"Driver"),passenger=Id(cs,"Passenger");
        var auth=new SqlAuthenticationRepository(cs);
        var a=new ForteMovePrincipal(auth.GetPrincipalContext(admin));
        var d=new ForteMovePrincipal(auth.GetPrincipalContext(driver));
        var p=new ForteMovePrincipal(auth.GetPrincipalContext(passenger));
        foreach(string name in new[]{"Overview","Due","Plans","RepairProviders","WorkOrders","CreateWorkOrder","ServiceHistory","ReturnToService","WorkOrderDetails","CompleteWorkOrder"})
        {
            string html=Render("Admin/Maintenance/"+name+".aspx","",a,200);Require(html.Contains("Maintenance")&&html.Contains("__VIEWSTATE"),"Maintenance page "+name);
        }
        foreach(string name in new[]{"ReturnToService","WorkOrderDetails","CompleteWorkOrder"})
            Require(Render("Admin/Maintenance/"+name+".aspx","id=9223372036854775807",a,200).Contains("not found")||name=="CompleteWorkOrder","Unknown maintenance context remains usable "+name);
        if(new SqlConnectionStringBuilder(cs).InitialCatalog.StartsWith("ForteMove_Slice9_Verification_")) {
            var orders=new SqlMaintenanceRepository(cs).GetWorkOrders(new MaintenanceQuery());
            foreach(var state in new[]{MaintenanceOrderStatus.Completed,MaintenanceOrderStatus.Open,MaintenanceOrderStatus.InProgress}) {
                var order=orders.First(x=>x.Status==state);
                Require(Render("Admin/Maintenance/WorkOrderDetails.aspx","id="+order.MaintenanceWorkOrderId,a,200).Contains(order.WorkOrderCode),"Populated work order "+state);
            }
            var current=orders.First(x=>x.Status==MaintenanceOrderStatus.InProgress);
            Require(Render("Admin/Maintenance/CompleteWorkOrder.aspx","id="+current.MaintenanceWorkOrderId,a,200).Contains("Verified service odometer"),"Populated completion form");
            Require(Render("Admin/Maintenance/Plans.aspx","id=1",a,200).Contains("Synthetic service"),"Populated plan edit");
            Require(Render("Admin/Maintenance/RepairProviders.aspx","id=1",a,200).Contains("Synthetic provider"),"Populated provider edit");
            Require(Render("Admin/Maintenance/ReturnToService.aspx","id=1",a,200).Contains("in progress"),"Populated return blockers");
            Require(Render("Admin/Maintenance/ServiceHistory.aspx","id=1",a,200).Contains("MWO-"),"Populated service history");
        }
        foreach(string name in new[]{"Requests","Vouchers","Transactions","Stations","CreateStation","PrototypeTerminal"})
        {
            string html=Render("Admin/Fuel/"+name+".aspx","",a,200);
            Require(html.Contains("Fuel") && html.Contains("__VIEWSTATE"),"Admin page "+name);
            Require(!html.Contains("FMFV1."),"Admin pages do not reveal tokens");
        }
        foreach(string name in new[]{"RequestDetails","VoucherDetails","TransactionDetails","EditStation"})
            Require(Render("Admin/Fuel/"+name+".aspx","id=9223372036854775807",a,404).Contains("unavailable"),"Unknown Admin detail "+name);
        foreach(string name in new[]{"Requests","RequestFuel","Vouchers","History"})
            Require(Render("Driver/Fuel/"+name+".aspx","",d,200).Contains("Fuel"),"Driver page "+name);
        foreach(string name in new[]{"RequestDetails","VoucherDetails"})
            Require(Render("Driver/Fuel/"+name+".aspx","id=9223372036854775807",d,404).Contains("unavailable"),"Unknown Driver detail "+name);
        Render("Admin/Dashboard.aspx","",a,200);
        Render("Admin/FleetList.aspx","",a,200);
        Render("Admin/Routes/RouteList.aspx","",a,200);
        Render("Admin/Scheduling/TripList.aspx","",a,200);
        Render("Admin/Assignments/AssignmentQueue.aspx","",a,200);
        Render("Admin/Operations/Defects.aspx","",a,200);
        Render("Driver/Today.aspx","",d,200);
        Render("Driver/Account.aspx","",d,200);
        Render("Passenger/Home.aspx","",p,200);
        Render("Account/Login.aspx","",null,200);
        // SimpleWorkerRequest invokes Page directly, not the IIS authentication/authorization
        // pipeline. Exercise the production base-page gate in isolation without running Page_Load
        // after a simulated Response.Redirect. Full cookie login remains a manual IIS acceptance case.
        Gate(true,d);Gate(true,p);Gate(false,a);Gate(false,p);Gate(true,null);Gate(false,null);
        Qr(null,401);Qr(a,403);Qr(p,403);Qr(d,404);
        string token=FuelRedemptionToken.Create();var protector=new MachineKeyFuelTokenProtector();
        byte[] protectedBytes=protector.Protect(token);
        Require(protector.Unprotect(protectedBytes)==token,"MachineKey roundtrip in actual application");
        Require(protectedBytes.Length<=512,"Protected token fits schema");
        protectedBytes[0]^=1;bool denied=false;try{protector.Unprotect(protectedBytes);}catch(System.Security.Cryptography.CryptographicException){denied=true;}
        Require(denied,"Tampered protected token cannot decrypt");
        byte[] png=FuelQrRenderer.Render(token);
        Require(png.Length>100 && png[0]==137&&png[1]==80&&png[2]==78&&png[3]==71,"Local QRCoder PNG generated");
        return checks+" runtime page/render/security checks passed, including existing portal regression pages, role gates, local QR and protected token. No database writes.";
    }
    void Qr(ForteMovePrincipal principal,int status)
    {
        var writer=new StringWriter();var ctx=new HttpContext(new SimpleWorkerRequest("Driver/Fuel/VoucherQr.ashx","id=9223372036854775807",writer));
        HttpContext.Current=ctx;ctx.User=principal;Thread.CurrentPrincipal=principal;
        new VoucherQrHandler().ProcessRequest(ctx);
        Require(ctx.Response.StatusCode==status,"QR ownership/role response "+status);
        HttpContext.Current=null;
    }
    sealed class TestAdminPage:AdminPage { }
    sealed class TestDriverPage:DriverPage { }
    void Gate(bool admin,ForteMovePrincipal principal)
    {
        var ctx=new HttpContext(new SimpleWorkerRequest(admin?"Admin/Maintenance/Overview.aspx":"Driver/Fuel/Requests.aspx","",new StringWriter()));
        HttpContext.Current=ctx;ctx.User=principal;Thread.CurrentPrincipal=principal;
        try { if(admin)new TestAdminPage().ProcessRequest(ctx);else new TestDriverPage().ProcessRequest(ctx); }
        catch(ThreadAbortException){Thread.ResetAbort();}
        Require(ctx.Response.StatusCode==302,"Production base page denies wrong/anonymous audience");
        HttpContext.Current=null;
    }
    string Render(string path,string query,ForteMovePrincipal principal,int status)
    {
        var writer=new StringWriter();var ctx=new HttpContext(new SimpleWorkerRequest(path,query,writer));
        HttpContext.Current=ctx;ctx.User=principal;Thread.CurrentPrincipal=principal;
        var page=(Page)BuildManager.CreateInstanceFromVirtualPath("~/"+path,typeof(Page));
        SessionStateUtility.AddHttpSessionStateToContext(ctx,new HttpSessionStateContainer("verification",new SessionStateItemCollection(),new HttpStaticObjectsCollection(),20,true,HttpCookieMode.UseCookies,SessionStateMode.InProc,false));
        try{page.ProcessRequest(ctx);}catch(ThreadAbortException){Thread.ResetAbort();}
        ctx.Response.Flush();
        Require(ctx.Response.StatusCode==status,path+" expected "+status+", got "+ctx.Response.StatusCode);
        HttpContext.Current=null;return writer.ToString();
    }
    long Id(string cs,string role)
    {
        using(var c=new SqlConnection(cs)){c.Open();using(var cmd=new SqlCommand("SELECT MIN(u.UserAccountId) FROM dbo.UserAccounts u JOIN dbo.Roles r ON r.RoleId=u.RoleId WHERE r.RoleCode=@Role AND u.IsActive=1 AND u.MustChangePassword=0 AND r.IsActive=1;",c)){
            cmd.Parameters.Add("@Role",SqlDbType.NVarChar,50).Value=role;
            object value=cmd.ExecuteScalar();if(value==DBNull.Value)throw new Exception("Requires an active "+role+" account with first-login password changed.");return (long)value;
        }}
    }
    void Require(bool pass,string message){if(!pass)throw new Exception(message);checks++;}
}
