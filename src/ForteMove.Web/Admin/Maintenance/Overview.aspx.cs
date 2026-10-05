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
 public partial class OverviewPage : MaintenanceAdminPage
 {
protected void Page_Load(object sender,EventArgs e){if(!IsPostBack){BindFlash();var s=ServiceFactory.CreateMaintenanceService();var x=s.GetOverview();litMetrics.Text=string.Join("",new[]{Metric("Due plans",x.DuePlans),Metric("Overdue plans",x.OverduePlans),Metric("Buses blocked by overdue service",x.BlockingBuses),Metric("Open work orders",x.OpenOrders),Metric("Work in progress",x.InProgressOrders)});litAttention.Text=P.Table(new[]{"Fleet","Attention","Reason","Action"},s.GetAttention(null).Select(a=>new[]{P.Cell(a.FleetNumber),P.Cell(a.Reference),P.Cell(a.Reason),P.Cell("Review",Link("Due",0)+"&busId="+a.BusId)}),"No current maintenance attention.");litOrders.Text=P.Table(new[]{"Order","Fleet","Provider","Status"},s.GetWorkOrders(null).Where(w=>w.Status==MaintenanceOrderStatus.Open||w.Status==MaintenanceOrderStatus.InProgress).Select(w=>new[]{P.Cell(w.WorkOrderCode,Link("WorkOrderDetails",w.MaintenanceWorkOrderId)),P.Cell(w.FleetNumber),P.Cell(w.ProviderNameSnapshot),P.Cell(w.Status)}),"No current work orders.");}}
private static string Metric(string label,long value){return "<div class=\"surface-card\"><span>"+P.Cell(label)+"</span><strong>"+value.ToString(CultureInfo.InvariantCulture)+"</strong></div>";}
 }
}

