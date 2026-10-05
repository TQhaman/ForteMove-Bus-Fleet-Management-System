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
 public partial class WorkOrdersPage : MaintenanceAdminPage
 {
protected void Page_Load(object sender,EventArgs e){if(!IsPostBack){Buses(ddlBus,true);EnumOptions<MaintenanceOrderStatus>(ddlStatus,true);BindFlash();Bind();}}
protected void Filter_Click(object sender,EventArgs e){Bind();}
private void Bind(){MaintenanceOrderStatus status;litOrders.Text=P.Table(new[]{"Order","Fleet","Provider","Type","Opened","Target","Status"},ServiceFactory.CreateMaintenanceService().GetWorkOrders(new MaintenanceQuery{BusId=Selected(ddlBus)>0?(long?)Selected(ddlBus):null,Search=txtSearch.Text,Status=Enum.TryParse(ddlStatus.SelectedValue,out status)?(MaintenanceOrderStatus?)status:null}).Select(w=>new[]{P.Cell(w.WorkOrderCode,Link("WorkOrderDetails",w.MaintenanceWorkOrderId)),P.Cell(w.FleetNumber),P.Cell(w.ProviderCodeSnapshot+" "+w.ProviderNameSnapshot),P.Cell(w.MaintenanceType),P.Cell(P.Time(w.OpenedUtc)),P.Cell(P.Date(w.TargetDate)),P.Cell(w.Status)}),"No matching work orders.");}
 }
}

