using System;
using System.Globalization;
using System.Web;
using System.Web.Script.Serialization;
using ForteMove.Models.Security;
using ForteMove.Models.Tracking;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Tracking
{
    public sealed class SnapshotHandler : IHttpHandler
    {
        public bool IsReusable { get { return false; } }
        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType="application/json";context.Response.TrySkipIisCustomErrors=true;
            context.Response.SuppressFormsAuthenticationRedirect=true;
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);context.Response.Cache.SetNoStore();
            var principal=context.User as ForteMovePrincipal;
            if(context.Request.HttpMethod!="GET") { Reply(context,405,new {Error="Method not allowed."});return; }
            if(principal==null||!principal.Identity.IsAuthenticated) { Reply(context,401,new {Error="Please sign in again."});return; }
            if(principal.Context.MustChangePassword) { Reply(context,403,new {Error="Change your password before continuing."});return; }
            try
            {
                var service=ServiceFactory.CreateTrackingService();object result=null;
                if(context.Request.QueryString["list"]=="1")
                {
                    if(!principal.IsInRole(RoleCode.TransportAdministrator.ToString())) { Reply(context,403,new {Error="Tracking is unavailable."});return; }
                    DateTime date;if(!DateTime.TryParseExact(context.Request.QueryString["date"],"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date)) { Reply(context,400,new {Error="Select a valid service date."});return; }
                    result=new {Items=service.GetAdminActiveTrips(new TrackingQuery {ServiceDate=date,IncludeCompleted=context.Request.QueryString["completed"]=="1"},principal.Context.UserAccountId)};
                }
                else
                {
                    bool passenger=principal.IsInRole(RoleCode.Passenger.ToString());
                    if((passenger&&context.Request.QueryString["tripId"]!=null)||(!passenger&&context.Request.QueryString["ticketId"]!=null)) { Reply(context,400,new {Error="Tracking is unavailable."});return; }
                    long id;if(!long.TryParse(context.Request.QueryString[passenger?"ticketId":"tripId"],NumberStyles.None,CultureInfo.InvariantCulture,out id)||id<=0) { Reply(context,400,new {Error="Tracking is unavailable."});return; }
                    long user=principal.Context.UserAccountId;
                    if(passenger) result=service.GetPassengerTrackingSnapshot(id,user);
                    else if(principal.IsInRole(RoleCode.Driver.ToString())) result=service.GetDriverTrackingSnapshot(id,user);
                    else if(principal.IsInRole(RoleCode.TransportAdministrator.ToString())) result=service.GetAdminTrackingSnapshot(id,user);
                    if(result==null) { Reply(context,404,new {Error="Tracking is unavailable or no longer belongs to your account."});return; }
                }
                Reply(context,200,result);
            }
            catch { Reply(context,503,new {Error="Tracking could not refresh. Please try again shortly."}); }
        }
        private static void Reply(HttpContext context,int status,object value)
        {
            context.Response.StatusCode=status;
            // CLR numeric properties are serialized directly; culture never participates in JSON coordinates.
            context.Response.Write(new JavaScriptSerializer {MaxJsonLength=2097152}.Serialize(value));
        }
    }
}
