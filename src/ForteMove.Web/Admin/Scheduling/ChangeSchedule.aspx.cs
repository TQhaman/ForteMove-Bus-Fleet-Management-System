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
    public partial class ChangeSchedule : AdminPage
    {
        private const string DeparturesKey="ForteMove.ChangeDepartures";
        protected void Page_Load(object sender,EventArgs e){if(!IsPostBack)BindInitial();}

        protected void btnAddDeparture_Click(object sender,EventArgs e)
        {
            ClearErrors();InvalidatePreview();TimeSpan value;
            if(!TimeSpan.TryParseExact(txtDepartureTime.Text,@"hh\:mm",CultureInfo.InvariantCulture,out value)){ShowErrors(new[]{new ValidationError("DepartureTimes","Enter a valid departure time.")});return;}
            ServiceResult<TimeSpan> result=ServiceFactory.CreateSchedulingService().ValidateDepartureTimeDraft(value,DepartureTimes);
            if(!result.Succeeded){ShowErrors(result.Errors);return;}List<TimeSpan> values=DepartureTimes;values.Add(value);DepartureTimes=values.OrderBy(item=>item).ToList();txtDepartureTime.Text=string.Empty;BindDepartures();
        }
        protected void rptDepartures_ItemCommand(object source,RepeaterCommandEventArgs e)
        {
            if(e.CommandName!="Remove")return;TimeSpan value;if(TimeSpan.TryParseExact(Convert.ToString(e.CommandArgument),"c",CultureInfo.InvariantCulture,out value)){List<TimeSpan> values=DepartureTimes;values.Remove(value);DepartureTimes=values;}InvalidatePreview();BindDepartures();
        }
        protected void btnReview_Click(object sender,EventArgs e)
        {
            ClearErrors();IList<ValidationError> errors=new List<ValidationError>();ChangeScheduleRequest request=BuildRequest(errors);
            if(errors.Count>0){ShowErrors(errors);InvalidatePreview();return;}ServiceResult<ScheduleChangeImpact> result=ServiceFactory.CreateSchedulingService().PreviewScheduleChange(request);
            if(!result.Succeeded){ShowErrors(result.Errors);InvalidatePreview();return;}BindImpact(result.Value);
        }
        protected void btnApply_Click(object sender,EventArgs e)
        {
            ClearErrors();IList<ValidationError> errors=new List<ValidationError>();ChangeScheduleRequest request=BuildRequest(errors);request.PreviewRequestFingerprint=PreviewFingerprint;request.PreviewOccurrenceSignature=PreviewSignature;
            if(errors.Count>0){ShowErrors(errors);InvalidatePreview();return;}SchedulingService service=ServiceFactory.CreateSchedulingService();ServiceResult<ScheduleChangeResult> result=service.ApplyScheduleChange(request,CurrentPrincipalContext.UserAccountId);
            if(!result.Succeeded){ShowErrors(result.Errors);ServiceResult<ScheduleChangeImpact> refreshed=service.PreviewScheduleChange(request);if(refreshed.Succeeded)BindImpact(refreshed.Value);else InvalidatePreview();return;}
            FlashMessageStore.Put(Session,string.Format(CultureInfo.CurrentCulture,"Schedule change applied. {0:N0} Trips generated; {1:N0} retained for review.",result.Value.GeneratedTripCount,result.Value.ProtectedTripCount),result.Value.ProtectedTripCount>0);
            Response.Redirect(ResolveUrl("~/Admin/Scheduling/ScheduleDetails.aspx?id="+ScheduleId.ToString(CultureInfo.InvariantCulture)),true);
        }

        private long ScheduleId{get{return ViewState["ScheduleId"]==null?0:(long)ViewState["ScheduleId"];}set{ViewState["ScheduleId"]=value;}}
        private long VersionId{get{return ViewState["VersionId"]==null?0:(long)ViewState["VersionId"];}set{ViewState["VersionId"]=value;}}
        private byte[] ScheduleRowVersion{get{return ViewState["ScheduleRV"] as byte[];}set{ViewState["ScheduleRV"]=value;}}
        private byte[] VersionRowVersion{get{return ViewState["VersionRV"] as byte[];}set{ViewState["VersionRV"]=value;}}
        private List<TimeSpan> DepartureTimes{get{return ViewState[DeparturesKey] as List<TimeSpan>??new List<TimeSpan>();}set{ViewState[DeparturesKey]=value;}}
        private string PreviewFingerprint{get{return ViewState["ChangeFingerprint"] as string;}set{ViewState["ChangeFingerprint"]=value;}}
        private string PreviewSignature{get{return ViewState["ChangeSignature"] as string;}set{ViewState["ChangeSignature"]=value;}}

        private void BindInitial()
        {
            long id;if(!long.TryParse(Request.QueryString["id"],NumberStyles.None,CultureInfo.InvariantCulture,out id)){pnlNotFound.Visible=true;return;}
            ScheduleChangeOptions options=ServiceFactory.CreateSchedulingService().GetChangeOptions(id);if(options==null){pnlNotFound.Visible=true;return;}
            pnlEditor.Visible=true;ScheduleId=options.RouteScheduleId;VersionId=options.RouteScheduleVersionId;ScheduleRowVersion=options.ScheduleRowVersion;VersionRowVersion=options.VersionRowVersion;
            litScheduleCode.Text=Server.HtmlEncode(options.ScheduleCode);litRoute.Text=Server.HtmlEncode(options.RouteCode+" - "+options.RouteName);lnkCancel.NavigateUrl="~/Admin/Scheduling/ScheduleDetails.aspx?id="+id.ToString(CultureInfo.InvariantCulture);
            txtEndDate.Text=options.EffectiveEndDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);txtExpectedCapacity.Text=options.ExpectedCapacity.HasValue?options.ExpectedCapacity.Value.ToString(CultureInfo.InvariantCulture):string.Empty;
            HashSet<byte> days=new HashSet<byte>(options.OperatingDays.Select(item=>(byte)item));foreach(ListItem item in cblOperatingDays.Items)item.Selected=days.Contains(byte.Parse(item.Value,CultureInfo.InvariantCulture));
            DepartureTimes=options.DepartureTimes.OrderBy(item=>item).ToList();BindDepartures();
            ddlBusCategory.Items.Add(new ListItem("No preference",string.Empty));foreach(LookupOption category in options.BusCategories)ddlBusCategory.Items.Add(new ListItem(category.DisplayName,category.Id.ToString(CultureInfo.InvariantCulture)));
            if(options.PreferredBusCategoryId.HasValue)ddlBusCategory.SelectedValue=options.PreferredBusCategoryId.Value.ToString(CultureInfo.InvariantCulture);
        }
        private void BindDepartures(){var rows=DepartureTimes.Select(item=>new{Display=DateTime.Today.Add(item).ToString("HH:mm",CultureInfo.CurrentCulture),Value=item.ToString("c",CultureInfo.InvariantCulture)}).ToList();rptDepartures.DataSource=rows;rptDepartures.DataBind();pnlNoDepartures.Visible=rows.Count==0;}
        private ChangeScheduleRequest BuildRequest(IList<ValidationError> errors)
        {
            DateTime changeDate;DateTime endDate;int category;int capacity;ChangeScheduleRequest request=new ChangeScheduleRequest{RouteScheduleId=ScheduleId,RouteScheduleVersionId=VersionId,ScheduleRowVersion=ScheduleRowVersion,VersionRowVersion=VersionRowVersion,ChangeEffectiveDate=DateTime.TryParseExact(txtChangeDate.Text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out changeDate)?changeDate:(DateTime?)null,EffectiveEndDate=DateTime.TryParseExact(txtEndDate.Text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out endDate)?endDate:(DateTime?)null,PreferredBusCategoryId=int.TryParse(ddlBusCategory.SelectedValue,NumberStyles.None,CultureInfo.InvariantCulture,out category)?category:(int?)null,DepartureTimes=DepartureTimes};
            if(!string.IsNullOrWhiteSpace(txtExpectedCapacity.Text)){if(int.TryParse(txtExpectedCapacity.Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out capacity))request.ExpectedCapacity=capacity;else errors.Add(new ValidationError("ExpectedCapacity","Expected capacity must be a whole number."));}
            foreach(ListItem item in cblOperatingDays.Items)if(item.Selected)request.OperatingDays.Add((OperatingDay)byte.Parse(item.Value,CultureInfo.InvariantCulture));return request;
        }
        private void BindImpact(ScheduleChangeImpact impact){PreviewFingerprint=impact.RequestFingerprint;PreviewSignature=impact.OccurrenceSignature;litReplaceCount.Text=impact.FutureTripsToReplace.ToString("N0",CultureInfo.CurrentCulture);litGenerateCount.Text=impact.NewTripsToGenerate.ToString("N0",CultureInfo.CurrentCulture);litProtectedCount.Text=impact.ProtectedTripsRequiringReview.ToString("N0",CultureInfo.CurrentCulture);litTicketProtectedCount.Text=impact.TicketProtectedTripCount.ToString("N0",CultureInfo.CurrentCulture);litPurchasedTicketCount.Text=impact.PurchasedTicketCount.ToString("N0",CultureInfo.CurrentCulture);pnlImpact.Visible=true;btnApply.Enabled=true;}
        private void InvalidatePreview(){PreviewFingerprint=null;PreviewSignature=null;pnlImpact.Visible=false;btnApply.Enabled=false;}
        private void ClearErrors(){pnlErrors.Visible=false;}
        private void ShowErrors(IEnumerable<ValidationError> errors){IList<ValidationError> items=(errors??Enumerable.Empty<ValidationError>()).ToList();if(items.Count==0)items.Add(new ValidationError(string.Empty,"Review the schedule change and try again."));rptErrors.DataSource=items;rptErrors.DataBind();pnlErrors.Visible=true;pnlErrors.Focus();}
    }
}
