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
 public partial class ServiceHistoryPage : MaintenanceAdminPage
 {
protected void Page_Load(object sender,EventArgs e){if(!IsPostBack){Buses(ddlBus);Choose(ddlBus,Id);BindFlash();Bind();}}
protected void Bus_Changed(object sender,EventArgs e){Bind();}
private void Bind(){long id=Selected(ddlBus);var s=ServiceFactory.CreateMaintenanceService();var b=s.GetReturnToServiceReview(id);lnkReturn.Visible=b!=null&&(b.Bus.BaseOperationalState==ForteMove.Models.Fleet.BusOperationalState.UnderMaintenance||b.Bus.BaseOperationalState==ForteMove.Models.Fleet.BusOperationalState.OutOfService);lnkReturn.NavigateUrl=Link("ReturnToService",id);lnkPlan.NavigateUrl=Link("Plans",0)+"&busId="+id;litBus.Text=b==null?"Select a bus to view its recorded service history.":"<p>"+P.Cell(b.Bus.FleetNumber)+"; "+P.Cell(b.Bus.BaseOperationalState)+"; "+P.Cell(P.Odo(b.Bus.OdometerKilometres))+"</p>";litOrders.Text=P.Table(new[]{"Order","Provider at opening","Type / trigger","Status","Opened","Started","Completed","Service odometer","Recorded cost","Work performed"},id<=0?new List<string[]>():s.GetWorkOrders(new MaintenanceQuery{BusId=id}).Select(w=>new[]{P.Cell(w.WorkOrderCode,Link("WorkOrderDetails",w.MaintenanceWorkOrderId)),P.Cell(w.ProviderCodeSnapshot+" "+w.ProviderNameSnapshot),P.Cell(P.Label(w.MaintenanceType)+" / "+P.Label(w.TriggerType)),P.Cell(w.Status),P.Cell(P.Time(w.OpenedUtc)),P.Cell(P.Time(w.StartedUtc)),P.Cell(P.Time(w.CompletedUtc)),P.Cell(P.Odo(w.ServiceOdometer)),P.Cell(P.Money(w.CompletedCost)),P.Cell(w.WorkPerformed)}),"No recorded work orders for this bus.");litHistory.Text=P.Table(new[]{"Recorded","Reference","Event"},id<=0?new List<string[]>():s.GetBusServiceHistory(id).Select(h=>new[]{P.Cell(P.Time(h.OccurredUtc)),P.Cell(h.Reference),P.Cell(h.Description)}),"No recorded progress or vehicle status transitions.");}
 }
}

