using System;
using System.Globalization;
using System.Linq;
using ForteMove.Models.Routing;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Admin.Routes
{
    public partial class StopCoordinatesPage:AdminPage
    {
        private long Id {get {long id;return long.TryParse(Request.QueryString["id"],NumberStyles.None,CultureInfo.InvariantCulture,out id)?id:0;}}
        protected void Page_Load(object sender,EventArgs e) { if(!IsPostBack)LoadStop(); }
        private void LoadStop() { var details=ServiceFactory.CreateRouteService().GetStopCoordinateDetails(Id,CurrentPrincipalContext.UserAccountId);if(details==null) {pnlEditor.Visible=false;Show("The Stop is unavailable.",false);return;}
            ViewState["RowVersion"]=details.RowVersion;litStop.Text=Server.HtmlEncode(details.StopCode+" - "+details.StopName);litArea.Text=Server.HtmlEncode(details.Area);litRoutes.Text=Server.HtmlEncode(string.Join(", ",details.Routes));
            txtLatitude.Text=Number(details.Latitude);txtLongitude.Text=Number(details.Longitude);
            if(details.ActiveTripCodes.Count>0)Warn(details,details.Latitude,details.Longitude); }
        protected void btnSave_Click(object sender,EventArgs e)
        {
            decimal? latitude,longitude;if(!Parse(txtLatitude.Text,out latitude)||!Parse(txtLongitude.Text,out longitude)) {Show("Enter valid coordinates using a decimal point.",false);return;}
            string signature=Number(latitude)+"|"+Number(longitude);
            bool acknowledged=chkAcknowledge.Checked && (string)ViewState["ReviewedCoordinates"]==signature;
            var result=ServiceFactory.CreateRouteService().UpdateStopCoordinates(new UpdateStopCoordinatesRequest {StopId=Id,Latitude=latitude,Longitude=longitude,RowVersion=ViewState["RowVersion"] as byte[],ActiveTripWarningAcknowledged=acknowledged},CurrentPrincipalContext.UserAccountId);
            if(!result.Succeeded) {Show(string.Join(" ",result.Errors.Select(x=>x.Message)),false);return;}
            if(!result.Value.Saved) {Warn(result.Value.Details,latitude,longitude);Show("Review the active-Trip warning and confirm this correction before saving.",false);return;}
            Response.Redirect(ResolveUrl("~/Admin/Routes/StopCoordinates.aspx?id="+Id.ToString(CultureInfo.InvariantCulture)+"&saved=1"),true);
        }
        private void Warn(StopCoordinateDetails details,decimal? latitude,decimal? longitude) { pnlWarning.Visible=true;chkAcknowledge.Checked=false;ViewState["ReviewedCoordinates"]=Number(latitude)+"|"+Number(longitude);
            litWarning.Text=Server.HtmlEncode("Changing coordinates immediately affects "+details.ActiveTripCodes.Count+" started, unfinished Trip(s): "+string.Join(", ",details.ActiveTripCodes)+". Current: "+Pair(details.Latitude,details.Longitude)+". Proposed: "+Pair(latitude,longitude)+"."); }
        protected override void OnPreRender(EventArgs e) {if(!IsPostBack&&Request.QueryString["saved"]=="1"&&pnlEditor.Visible)Show("Stop coordinates saved.",true);base.OnPreRender(e);}
        private void Show(string message,bool success) {pnlMessage.Visible=true;pnlMessage.CssClass=success?"alert alert-success":"alert alert-danger";litMessage.Text=Server.HtmlEncode(message);}
        private static string Number(decimal? value) {return value.HasValue?value.Value.ToString("0.######",CultureInfo.InvariantCulture):"";}
        private static string Pair(decimal? a,decimal? b) {return a.HasValue?Number(a)+", "+Number(b):"not supplied";}
        private static bool Parse(string text,out decimal? value) {value=null;if(string.IsNullOrWhiteSpace(text))return true;decimal number;if(!decimal.TryParse(text.Trim(),NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out number))return false;value=number;return true;}
    }
}
