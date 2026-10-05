using System;
using System.Globalization;
using ForteMove.Business.Time;
using ForteMove.Models.Tracking;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Admin.Tracking
{
    public partial class LiveTrackingPage : AdminPage
    {
        protected void Page_Load(object sender,EventArgs e) { if(!IsPostBack) { txtDate.Text=new SystemClock().Today.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);Bind(); } }
        protected void btnRefresh_Click(object sender,EventArgs e) { Bind(); }
        private void Bind() { DateTime date;if(!DateTime.TryParseExact(txtDate.Text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date)) { lblMessage.Text="Select a valid service date.";return; }
            lblMessage.Text="";var rows=ServiceFactory.CreateTrackingService().GetAdminActiveTrips(new TrackingQuery {ServiceDate=date,IncludeCompleted=chkCompleted.Checked},CurrentPrincipalContext.UserAccountId);rptTrips.DataSource=rows;rptTrips.DataBind();pnlEmpty.Visible=rows.Count==0; }
    }
}
