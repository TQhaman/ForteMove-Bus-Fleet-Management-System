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
 public partial class PlansPage : MaintenanceAdminPage
 {
protected void Page_Load(object sender,EventArgs e){if(!IsPostBack){Buses(ddlBus);Choose(ddlBus,QueryId("busId"));BindFlash();Bind();var p=Id>0?ServiceFactory.CreateMaintenanceService().GetPlan(Id):null;litEditor.Text=p==null?"Add preventive plan":P.Cell("Edit "+p.PlanCode);if(Id>0&&p==null){Message("The plan was not found.");btnSave.Enabled=false;return;}if(p!=null){Choose(ddlBus,p.BusId);ddlBus.Enabled=false;txtName.Text=p.ServiceName;txtDays.Text=Convert.ToString(p.IntervalDays,CultureInfo.InvariantCulture);txtKm.Text=Convert.ToString(p.IntervalKilometres,CultureInfo.InvariantCulture);txtLastDate.Text=p.LastServiceDate.HasValue?p.LastServiceDate.Value.ToString("yyyy-MM-dd"):"";txtLastOdo.Text=Convert.ToString(p.LastServiceOdometer,CultureInfo.InvariantCulture);txtNextDate.Text=p.NextDueDate.HasValue?p.NextDueDate.Value.ToString("yyyy-MM-dd"):"";txtNextOdo.Text=Convert.ToString(p.NextDueOdometer,CultureInfo.InvariantCulture);chkVerified.Checked=p.LastServiceDate.HasValue||p.LastServiceOdometer.HasValue;chkBlocking.Checked=p.BlocksOperationWhenOverdue;chkActive.Checked=p.IsActive;ViewState["Rv"]=p.RowVersion;if(p.LastCompletedWorkOrderId.HasValue){chkVerified.Checked=true;chkVerified.Enabled=false;txtLastDate.ReadOnly=true;txtLastOdo.ReadOnly=true;txtNextDate.ReadOnly=true;txtNextOdo.ReadOnly=true;}}}}
private void Bind(){litPlans.Text=P.Table(new[]{"Plan","Fleet","Service","Next date","Next odometer","Status","Operational effect"},ServiceFactory.CreateMaintenanceService().GetPlans(null).Select(p=>new[]{P.Cell(p.PlanCode,Link("Plans",p.MaintenancePlanId)),P.Cell(p.FleetNumber,Link("ServiceHistory",p.BusId)),P.Cell(p.ServiceName),P.Cell(P.Date(p.NextDueDate)),P.Cell(P.Odo(p.NextDueOdometer)),P.Cell(p.IsActive?P.Label(p.DueState):"Inactive"),P.Cell(p.BlocksOperation?"New operations blocked":p.BlocksOperationWhenOverdue?"Blocks only when overdue":"Advisory")}),"No plans yet. Add an approved preventive plan below.");}
protected void Save_Click(object sender,EventArgs e){try{var r=ServiceFactory.CreateMaintenanceService().SavePlan(new SaveMaintenancePlanRequest{MaintenancePlanId=Id,RowVersion=Token("Rv"),BusId=Selected(ddlBus),ServiceName=txtName.Text,IntervalDays=Integer(txtDays),IntervalKilometres=Number(txtKm),LastServiceDate=DateInput(txtLastDate),LastServiceOdometer=Number(txtLastOdo),NextDueDate=DateInput(txtNextDate),NextDueOdometer=Number(txtNextOdo),UseVerifiedBaseline=chkVerified.Checked,IsActive=chkActive.Checked,BlocksOperationWhenOverdue=chkBlocking.Checked},Actor);if(Result(r))Saved("Plans",r.Value,"Preventive plan saved.");}catch(FormatException x){Message(x.Message);}}
 }
}

