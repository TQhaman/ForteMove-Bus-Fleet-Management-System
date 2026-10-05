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
 public partial class DuePage : MaintenanceAdminPage
 {
protected void Page_Load(object sender,EventArgs e){if(!IsPostBack){Buses(ddlBus,true);EnumOptions<MaintenanceDueState>(ddlDue,true);Choose(ddlBus,QueryId("busId"));Bind();}}
protected void Filter_Click(object sender,EventArgs e){Bind();}
private void Bind(){MaintenanceDueState state;var q=new MaintenanceQuery{BusId=Selected(ddlBus)>0?(long?)Selected(ddlBus):null,Search=txtSearch.Text,DueState=Enum.TryParse(ddlDue.SelectedValue,out state)?(MaintenanceDueState?)state:null};var s=ServiceFactory.CreateMaintenanceService();litAttention.Text=P.Table(new[]{"Fleet","Attention","Reason","Operational effect","Next action"},s.GetAttention(q).Select(a=>new[]{P.Cell(a.FleetNumber,Link("ServiceHistory",a.BusId)),P.Cell(a.Reference),P.Cell(a.Reason),P.Cell(a.Blocking?"New operations blocked":"Review required"),P.Cell("Create work order",Link("CreateWorkOrder",0)+"&busId="+a.BusId+(a.PlanId.HasValue?"&planId="+a.PlanId.Value:"")+(a.DefectId.HasValue?"&defectId="+a.DefectId.Value:""))}),"No matching maintenance attention.");}
 }
}

