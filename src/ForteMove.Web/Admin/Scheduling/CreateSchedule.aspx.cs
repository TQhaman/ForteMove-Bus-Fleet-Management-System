using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using ForteMove.Business.Services;
using ForteMove.Models.Common;
using ForteMove.Models.Fleet;
using ForteMove.Models.Scheduling;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Admin.Scheduling
{
    public partial class CreateSchedule : AdminPage
    {
        private const string DepartureStateKey="ForteMove.ScheduleDepartures";
        private const string PreviewFingerprintKey="ForteMove.SchedulePreviewFingerprint";
        private const string PreviewSignatureKey="ForteMove.SchedulePreviewSignature";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack) { DepartureTimes=new List<TimeSpan>(); BindOptions(); BindDepartures(); }
        }

        protected void btnAddDeparture_Click(object sender, EventArgs e)
        {
            ClearErrors(); InvalidatePreview();
            TimeSpan value;
            if (!TimeSpan.TryParseExact(txtDepartureTime.Text, @"hh\:mm", CultureInfo.InvariantCulture, out value))
            { ShowErrors(new[]{new ValidationError("DepartureTimes","Enter a valid departure time.")}); return; }
            ServiceResult<TimeSpan> result=ServiceFactory.CreateSchedulingService().ValidateDepartureTimeDraft(value,DepartureTimes);
            if (!result.Succeeded) { ShowErrors(result.Errors); return; }
            List<TimeSpan> values=DepartureTimes; values.Add(result.Value); DepartureTimes=values.OrderBy(item=>item).ToList();
            txtDepartureTime.Text=string.Empty; BindDepartures();
        }

        protected void rptDepartures_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (e.CommandName!="Remove") return;
            TimeSpan value;
            if (TimeSpan.TryParseExact(Convert.ToString(e.CommandArgument), "c", CultureInfo.InvariantCulture, out value))
            { List<TimeSpan> values=DepartureTimes; values.Remove(value); DepartureTimes=values; }
            InvalidatePreview(); BindDepartures();
        }

        protected void btnPreview_Click(object sender, EventArgs e)
        {
            ClearErrors();
            IList<ValidationError> bindingErrors=new List<ValidationError>();
            CreateScheduleRequest request=BuildRequest(bindingErrors);
            if (bindingErrors.Count>0) { ShowErrors(bindingErrors); InvalidatePreview(); return; }
            ServiceResult<SchedulePreview> result=ServiceFactory.CreateSchedulingService().PreviewSchedule(request);
            if (!result.Succeeded) { ShowErrors(result.Errors); InvalidatePreview(); return; }
            BindPreview(result.Value);
        }

        protected void btnCreate_Click(object sender, EventArgs e)
        {
            ClearErrors(); IList<ValidationError> bindingErrors=new List<ValidationError>();
            CreateScheduleRequest request=BuildRequest(bindingErrors);
            request.PreviewRequestFingerprint=PreviewFingerprint;
            request.PreviewOccurrenceSignature=PreviewSignature;
            if (bindingErrors.Count>0) { ShowErrors(bindingErrors); InvalidatePreview(); return; }
            SchedulingService service=ServiceFactory.CreateSchedulingService();
            ServiceResult<ScheduleCreationResult> result=service.CreateSchedule(request,CurrentPrincipalContext.UserAccountId);
            if (!result.Succeeded)
            {
                ShowErrors(result.Errors);
                ServiceResult<SchedulePreview> refreshed=service.PreviewSchedule(request);
                if (refreshed.Succeeded) BindPreview(refreshed.Value); else InvalidatePreview();
                return;
            }
            FlashMessageStore.Put(Session,string.Format(CultureInfo.CurrentCulture,"Schedule {0} was created with {1:N0} Trips.",result.Value.ScheduleCode,result.Value.GeneratedTripCount),false);
            Response.Redirect(ResolveUrl("~/Admin/Scheduling/ScheduleDetails.aspx?id="+result.Value.RouteScheduleId.ToString(CultureInfo.InvariantCulture)),true);
        }

        private List<TimeSpan> DepartureTimes
        {
            get { return ViewState[DepartureStateKey] as List<TimeSpan> ?? new List<TimeSpan>(); }
            set { ViewState[DepartureStateKey]=value; }
        }
        private string PreviewFingerprint { get { return ViewState[PreviewFingerprintKey] as string; } set { ViewState[PreviewFingerprintKey]=value; } }
        private string PreviewSignature { get { return ViewState[PreviewSignatureKey] as string; } set { ViewState[PreviewSignatureKey]=value; } }

        private void BindOptions()
        {
            SchedulingCreationOptions options=ServiceFactory.CreateSchedulingService().GetCreationOptions();
            litScheduleCode.Text=Server.HtmlEncode(options.SuggestedScheduleCode);
            ddlRoute.Items.Clear(); ddlRoute.Items.Add(new ListItem("Select an active Route",string.Empty));
            foreach (ScheduleRouteOption route in options.Routes) ddlRoute.Items.Add(new ListItem(route.RouteCode+" - "+route.RouteName,route.RouteId.ToString(CultureInfo.InvariantCulture)));
            ddlBusCategory.Items.Clear(); ddlBusCategory.Items.Add(new ListItem("No preference",string.Empty));
            foreach (LookupOption category in options.BusCategories) ddlBusCategory.Items.Add(new ListItem(category.DisplayName,category.Id.ToString(CultureInfo.InvariantCulture)));
        }

        private void BindDepartures()
        {
            var rows=DepartureTimes.OrderBy(item=>item).Select(item=>new{Display=DateTime.Today.Add(item).ToString("HH:mm",CultureInfo.CurrentCulture),Value=item.ToString("c",CultureInfo.InvariantCulture)}).ToList();
            rptDepartures.DataSource=rows; rptDepartures.DataBind(); pnlNoDepartures.Visible=rows.Count==0;
        }

        private CreateScheduleRequest BuildRequest(IList<ValidationError> errors)
        {
            long routeId; int categoryId; int capacity; DateTime start; DateTime end;
            CreateScheduleRequest request=new CreateScheduleRequest
            {
                RouteId=long.TryParse(ddlRoute.SelectedValue,NumberStyles.None,CultureInfo.InvariantCulture,out routeId)?routeId:(long?)null,
                EffectiveStartDate=DateTime.TryParseExact(txtStartDate.Text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out start)?start:(DateTime?)null,
                EffectiveEndDate=DateTime.TryParseExact(txtEndDate.Text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out end)?end:(DateTime?)null,
                PreferredBusCategoryId=int.TryParse(ddlBusCategory.SelectedValue,NumberStyles.None,CultureInfo.InvariantCulture,out categoryId)?categoryId:(int?)null,
                DepartureTimes=DepartureTimes
            };
            if (!string.IsNullOrWhiteSpace(txtExpectedCapacity.Text))
            {
                if (int.TryParse(txtExpectedCapacity.Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out capacity)) request.ExpectedCapacity=capacity;
                else errors.Add(new ValidationError("ExpectedCapacity","Expected capacity must be a whole number."));
            }
            foreach (ListItem item in cblOperatingDays.Items) if (item.Selected) request.OperatingDays.Add((OperatingDay)byte.Parse(item.Value,CultureInfo.InvariantCulture));
            return request;
        }

        private void BindPreview(SchedulePreview preview)
        {
            PreviewFingerprint=preview.RequestFingerprint; PreviewSignature=preview.OccurrenceSignature;
            litPreviewCount.Text=preview.TripCount.ToString("N0",CultureInfo.CurrentCulture);
            litPreviewRange.Text=string.Format(CultureInfo.CurrentCulture,"First departure {0:dd MMM yyyy HH:mm}; last departure {1:dd MMM yyyy HH:mm}.",preview.FirstDepartureLocal,preview.LastDepartureLocal);
            pnlPreview.Visible=true; btnCreate.Enabled=true;
        }

        private void InvalidatePreview() { PreviewFingerprint=null; PreviewSignature=null; pnlPreview.Visible=false; btnCreate.Enabled=false; }
        private void ClearErrors() { pnlErrors.Visible=false; }
        private void ShowErrors(IEnumerable<ValidationError> errors)
        {
            IList<ValidationError> items=(errors??Enumerable.Empty<ValidationError>()).ToList();
            if (items.Count==0) items.Add(new ValidationError(string.Empty,"Review the schedule and try again."));
            rptErrors.DataSource=items; rptErrors.DataBind(); pnlErrors.Visible=true; pnlErrors.Focus();
        }
    }
}
