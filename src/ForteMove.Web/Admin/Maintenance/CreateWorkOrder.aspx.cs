using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using ForteMove.Models.Maintenance;
using ForteMove.Web.Infrastructure;
using P = ForteMove.Web.Infrastructure.MaintenancePresentation;
namespace ForteMove.Web.Admin.Maintenance
{
 public partial class CreateWorkOrderPage : MaintenanceAdminPage
 {
protected void Page_Load(object sender,EventArgs e){if(!IsPostBack){Buses(ddlBus);Choose(ddlBus,QueryId("busId"));ddlProvider.Items.Add(new ListItem("Select a provider",""));foreach(var p in ServiceFactory.CreateRepairProviderService().GetProviders().Where(x=>x.IsActive))ddlProvider.Items.Add(new ListItem(p.ProviderCode+" "+p.ProviderName,p.RepairProviderId.ToString()));EnumOptions<MaintenanceType>(ddlType,true);EnumOptions<MaintenanceTrigger>(ddlTrigger,true);EnumOptions<ComplianceRequirement>(ddlCompliance,true);ddlType.Items[0].Text="Select a type";ddlTrigger.Items[0].Text="Select a trigger";ddlCompliance.Items[0].Text="Not applicable";ViewState["CreationToken"]=Guid.NewGuid();LoadSources();Choose(ddlPlan,QueryId("planId"));Choose(ddlDefect,QueryId("defectId"));CaptureDefectVersion();if(QueryId("defectId")>0){Choose(ddlTrigger,MaintenanceTrigger.Defect);Choose(ddlType,MaintenanceType.Corrective);}else if(QueryId("planId")>0){var plan=ServiceFactory.CreateMaintenanceService().GetPlan(QueryId("planId"));Choose(ddlTrigger,plan!=null&&!plan.NextDueDate.HasValue?MaintenanceTrigger.ServiceOdometer:MaintenanceTrigger.ServiceDate);Choose(ddlType,MaintenanceType.Preventive);}if(QueryId("exceptionId")>0){ViewState["ExceptionId"]=QueryId("exceptionId");litException.Text="<p>Linked operational exception "+P.Cell(QueryId("exceptionId"))+".</p>";Choose(ddlTrigger,MaintenanceTrigger.Breakdown);Choose(ddlType,MaintenanceType.Breakdown);}}}
protected void Bus_Changed(object sender,EventArgs e){ViewState.Remove("ExceptionId");litException.Text="";LoadSources();}
protected void Defect_Changed(object sender,EventArgs e){CaptureDefectVersion();}
private void CaptureDefectVersion(){var d=Selected(ddlDefect)>0?ServiceFactory.CreateOperationsAdminService().GetDefectDetails(Selected(ddlDefect)):null;ViewState["DefectRv"]=d==null?null:d.RowVersion;}
private void LoadSources(){ViewState.Remove("DefectRv");ddlPlan.Items.Clear();ddlPlan.Items.Add(new ListItem("None",""));foreach(var p in ServiceFactory.CreateMaintenanceService().GetPlans(new MaintenanceQuery{BusId=Selected(ddlBus)}).Where(x=>x.IsActive))ddlPlan.Items.Add(new ListItem(p.PlanCode+" "+p.ServiceName,p.MaintenancePlanId.ToString()));ddlDefect.Items.Clear();ddlDefect.Items.Add(new ListItem("None",""));foreach(var d in ServiceFactory.CreateOperationsAdminService().GetDefectReports(null).Where(x=>x.BusId==Selected(ddlBus)&&x.Status.ToString()!="Resolved"))ddlDefect.Items.Add(new ListItem(d.DefectCode+" "+d.Description,d.BusDefectReportId.ToString()));}
protected void Create_Click(object sender,EventArgs e){try{MaintenanceType type;MaintenanceTrigger trigger;ComplianceRequirement compliance;var defect=Selected(ddlDefect)>0?ServiceFactory.CreateOperationsAdminService().GetDefectDetails(Selected(ddlDefect)):null;var r=ServiceFactory.CreateMaintenanceService().CreateWorkOrder(new CreateWorkOrderRequest{BusId=Selected(ddlBus),RepairProviderId=Selected(ddlProvider),MaintenanceType=Enum.TryParse(ddlType.SelectedValue,out type)?(MaintenanceType?)type:null,TriggerType=Enum.TryParse(ddlTrigger.SelectedValue,out trigger)?(MaintenanceTrigger?)trigger:null,MaintenancePlanId=Selected(ddlPlan)>0?(long?)Selected(ddlPlan):null,BusDefectReportId=defect==null?null:(long?)defect.BusDefectReportId,TripCannotProceedReportId=ViewState["ExceptionId"] as long?,ComplianceRequirement=Enum.TryParse(ddlCompliance.SelectedValue,out compliance)?(ComplianceRequirement?)compliance:null,RequestedWork=txtWork.Text,TargetDate=DateInput(txtTarget),DefectReviewNote=txtReview.Text,DefectRowVersion=Token("DefectRv"),CreationToken=(Guid)ViewState["CreationToken"]},Actor);if(Result(r))Saved("WorkOrderDetails",r.Value,"Work order created. Bus status has not changed.");}catch(FormatException x){Message(x.Message);}}
 }
}

