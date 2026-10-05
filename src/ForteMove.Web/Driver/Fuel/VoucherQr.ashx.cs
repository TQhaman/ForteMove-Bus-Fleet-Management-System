using System.Web;
using ForteMove.Models.Security;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Driver.Fuel
{
    public sealed class VoucherQrHandler : IHttpHandler
    {
        public bool IsReusable { get { return false; } }
        public void ProcessRequest(HttpContext context)
        {
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);context.Response.Cache.SetNoStore();
            context.Response.SuppressFormsAuthenticationRedirect=true;context.Response.TrySkipIisCustomErrors=true;
            var principal=context.User as ForteMovePrincipal;
            if(context.Request.HttpMethod!="GET") { context.Response.StatusCode=405;return; }
            if(principal==null || !principal.Identity.IsAuthenticated) { context.Response.StatusCode=401;return; }
            if(principal.Context.MustChangePassword || !principal.IsInRole(RoleCode.Driver.ToString())) { context.Response.StatusCode=403;return; }
            var result=ServiceFactory.CreateDriverFuelService().GetVoucherToken(FuelPresentation.Id(context.Request.QueryString["id"]),principal.Context.UserAccountId);
            if(!result.Succeeded) { context.Response.StatusCode=404;return; }
            context.Response.ContentType="image/png";context.Response.BinaryWrite(FuelQrRenderer.Render(result.Value));
        }
    }
}
