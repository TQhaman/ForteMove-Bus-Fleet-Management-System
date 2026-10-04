using System;
using System.Globalization;
using ForteMove.Models.Scheduling;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Admin.Scheduling
{
    public partial class ScheduleDetailsPage : AdminPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack) { ShowFlashMessage(); BindDetails(); }
        }

        protected string FormatHistoryPeriod(object startValue, object superseded, object declaredEnd)
        {
            DateTime start=Convert.ToDateTime(startValue).Date;
            if(superseded!=null && superseded!=DBNull.Value && Convert.ToDateTime(superseded).Date<=start)
                return "Replaced before service began (planned start "+start.ToString("dd MMM yyyy",CultureInfo.CurrentCulture)+")";
            DateTime end=superseded==null || superseded==DBNull.Value?Convert.ToDateTime(declaredEnd):Convert.ToDateTime(superseded).AddDays(-1);
            return string.Format(CultureInfo.CurrentCulture,"Effective {0:dd MMM yyyy} to {1:dd MMM yyyy}",start,end);
        }

        private void BindDetails()
        {
            long id; if (!long.TryParse(Request.QueryString["id"],NumberStyles.None,CultureInfo.InvariantCulture,out id)) { pnlNotFound.Visible=true; return; }
            ScheduleDetails details=ServiceFactory.CreateSchedulingService().GetScheduleDetails(id);
            if (details==null) { pnlNotFound.Visible=true; return; }
            pnlDetails.Visible=true;
            litScheduleCode.Text=Server.HtmlEncode(details.ScheduleCode);
            litRoute.Text=Server.HtmlEncode(details.RouteCode+" - "+details.RouteName);
            litJourney.Text=Server.HtmlEncode(details.OriginName+" to "+details.DestinationName);
            litDays.Text=Server.HtmlEncode(details.CurrentVersion.OperatingDaysDisplay);
            litTimes.Text=Server.HtmlEncode(details.CurrentVersion.DepartureTimesDisplay);
            litPeriod.Text=Server.HtmlEncode(string.Format(CultureInfo.CurrentCulture,"{0:dd MMM yyyy} - {1:dd MMM yyyy}",details.CurrentVersion.EffectiveStartDate,details.CurrentVersion.EffectiveEndDate));
            litCategory.Text=Server.HtmlEncode(string.IsNullOrWhiteSpace(details.CurrentVersion.PreferredBusCategoryName)?"No preference":details.CurrentVersion.PreferredBusCategoryName);
            litCapacity.Text=details.CurrentVersion.ExpectedCapacity.HasValue?details.CurrentVersion.ExpectedCapacity.Value.ToString("N0",CultureInfo.CurrentCulture):"Not specified";
            litTripCount.Text=details.GeneratedTripCount.ToString("N0",CultureInfo.CurrentCulture);
            lblStatus.Text=details.Status.ToString(); lblStatus.CssClass=details.Status==ScheduleStatus.Active?"status-badge status-success":details.Status==ScheduleStatus.Upcoming?"status-badge status-info":"status-badge status-neutral";
            lnkChange.NavigateUrl="~/Admin/Scheduling/ChangeSchedule.aspx?id="+id.ToString(CultureInfo.InvariantCulture);
            rptHistory.DataSource=details.History; rptHistory.DataBind(); pnlNoHistory.Visible=details.History.Count==0;
        }

        private void ShowFlashMessage()
        {
            FlashMessage message=FlashMessageStore.Take(Session); if(message==null||string.IsNullOrWhiteSpace(message.Text))return;
            pnlFlash.CssClass=message.IsWarning?"alert alert-warning app-alert":"alert alert-success app-alert"; lblFlash.Text=Server.HtmlEncode(message.Text); pnlFlash.Visible=true;
        }
    }
}
