using System;
using System.Collections.Generic;
using System.Globalization;
using ForteMove.Models.Scheduling;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Admin.Scheduling
{
    public partial class ScheduleList : AdminPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack) { ShowFlashMessage(); BindSchedules(); }
        }

        protected void btnApplyFilters_Click(object sender, EventArgs e) { BindSchedules(); }
        protected void btnClearFilters_Click(object sender, EventArgs e) { Response.Redirect(ResolveUrl("~/Admin/Scheduling/ScheduleList.aspx"), true); }

        protected string FormatPeriod(object start, object end)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0:dd MMM yyyy} - {1:dd MMM yyyy}", Convert.ToDateTime(start), Convert.ToDateTime(end));
        }

        protected string FormatStatus(object value) { return Convert.ToString(value, CultureInfo.InvariantCulture).Replace("InProgress", "In progress"); }
        protected string GetStatusCss(object value)
        {
            string status = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (status == "Active") return "status-badge status-success";
            if (status == "Upcoming") return "status-badge status-info";
            return "status-badge status-neutral";
        }
        protected string GetDetailsUrl(object id) { return ResolveUrl("~/Admin/Scheduling/ScheduleDetails.aspx?id=" + Convert.ToInt64(id).ToString(CultureInfo.InvariantCulture)); }

        private void BindSchedules()
        {
            ScheduleStatus parsed;
            ScheduleStatus? status = Enum.TryParse(ddlStatus.SelectedValue, out parsed) ? parsed : (ScheduleStatus?)null;
            IList<ScheduleListItem> items = ServiceFactory.CreateSchedulingService().GetScheduleList(new ScheduleQuery { SearchTerm=txtSearch.Text, Status=status });
            bool any = items.Count > 0;
            pnlResults.Visible = any; pnlEmpty.Visible = !any;
            if (any)
            {
                rptSchedules.DataSource=items; rptSchedules.DataBind();
                litCount.Text = items.Count == 1 ? "1 schedule matches the current view." : string.Format(CultureInfo.CurrentCulture, "{0:N0} schedules match the current view.", items.Count);
            }
            else
            {
                bool filtered = !string.IsNullOrWhiteSpace(txtSearch.Text) || !string.IsNullOrWhiteSpace(ddlStatus.SelectedValue);
                litEmptyHeading.Text = filtered ? "No schedules match these filters" : "No schedules created yet";
                litEmptyMessage.Text = filtered ? "Adjust or clear the filters to return to all schedules." : "Create a recurring service pattern for an active Route.";
                lnkEmptyAction.Visible = !filtered;
            }
        }

        private void ShowFlashMessage()
        {
            FlashMessage message=FlashMessageStore.Take(Session);
            if (message==null || string.IsNullOrWhiteSpace(message.Text)) return;
            pnlFlash.CssClass=message.IsWarning?"alert alert-warning app-alert":"alert alert-success app-alert";
            lblFlash.Text=Server.HtmlEncode(message.Text); pnlFlash.Visible=true;
        }
    }
}
