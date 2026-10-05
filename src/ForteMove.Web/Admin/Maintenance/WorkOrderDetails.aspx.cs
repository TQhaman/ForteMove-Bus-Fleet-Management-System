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
 public partial class WorkOrderDetailsPage : MaintenanceAdminPage
 {
protected void Page_Load(object sender,EventArgs e){if(!IsPostBack){BindFlash();Bind();}}
private void Bind(){var s=ServiceFactory.CreateMaintenanceService();var w=s.GetWorkOrder(Id);if(w==null){Message("The work order was not found.");pnlActions.Visible=pnlStart.Visible=false;lnkComplete.Visible=lnkHistory.Visible=false;return;}ViewState["Rv"]=w.RowVersion;lnkHistory.NavigateUrl=Link("ServiceHistory",w.BusId);lnkComplete.NavigateUrl=Link("CompleteWorkOrder",Id);lnkComplete.Visible=w.Status==MaintenanceOrderStatus.InProgress;pnlActions.Visible=w.Status==MaintenanceOrderStatus.Open||w.Status==MaintenanceOrderStatus.InProgress;pnlStart.Visible=w.Status==MaintenanceOrderStatus.Open;litContext.Text=P.Table(new[]{"Order","Fleet","Provider at opening","Type","Trigger","Status","Target","Opened","Started","Completed","Cancelled","Requested work"},new[]{new[]{P.Cell(w.WorkOrderCode),P.Cell(w.FleetNumber),P.Cell(w.ProviderCodeSnapshot+" "+w.ProviderNameSnapshot),P.Cell(w.MaintenanceType),P.Cell(w.TriggerType),P.Cell(w.Status),P.Cell(P.Date(w.TargetDate)),P.Cell(P.Time(w.OpenedUtc)),P.Cell(P.Time(w.StartedUtc)),P.Cell(P.Time(w.CompletedUtc)),P.Cell(P.Time(w.CancelledUtc)),P.Cell(w.RequestedWork)}},"");
if(w.Status==MaintenanceOrderStatus.Completed)litContext.Text+="<p><strong>Work performed:</strong> "+P.Cell(w.WorkPerformed)+"</p><p>"+P.Cell(w.CompletionNote)+"</p><p>Verified odometer: "+P.Cell(P.Odo(w.ServiceOdometer))+"; recorded cost: "+P.Cell(P.Money(w.CompletedCost))+"; reference: "+P.Cell(w.ExternalReference)+"</p>";
if(w.Status==MaintenanceOrderStatus.Cancelled)litContext.Text+="<p>Cancellation: "+P.Cell(w.CancellationReason)+"</p>";
if(pnlStart.Visible){var r=s.PreviewStart(Id);ViewState["Fingerprint"]=r.Fingerprint;litImpact.Text="<p>Current vehicle status: "+P.Cell(P.Label(w.BusStatus))+". Pending fuel requests: "+r.PendingFuelRequests+". Unused vouchers: "+r.ActiveFuelVouchers+".</p>"+P.Table(new[]{"Trip","Departure","Purchased tickets"},r.Trips.Select(t=>new[]{P.Cell(t.TripCode,AssignmentLink(t.TripId)),P.Cell(t.DepartureLocal.ToString("dd MMM yyyy HH:mm")),P.Cell(t.PurchasedTickets)}),"No assigned, unstarted Trips are affected.");btnStart.Enabled=r.Blockers.Count==0;if(r.Blockers.Count>0)litImpact.Text+="<div class=\"alert alert-warning\">"+P.Cell(string.Join(" ",r.Blockers))+"</div>";}
litHistory.Text=P.Table(new[]{"Recorded","Reference","Event"},s.GetBusServiceHistory(w.BusId).Where(h=>h.Reference==w.WorkOrderCode||h.Reference==w.FleetNumber).Select(h=>new[]{P.Cell(P.Time(h.OccurredUtc)),P.Cell(h.Reference),P.Cell(h.Description)}),"No progress or vehicle status changes recorded.");}
protected void Start_Click(object sender,EventArgs e){if(!chkConfirm.Checked){Message("Confirm that you have reviewed the impact before starting.");return;}var r=ServiceFactory.CreateMaintenanceService().StartMaintenance(new StartMaintenanceRequest{MaintenanceWorkOrderId=Id,RowVersion=Token("Rv"),Fingerprint=ViewState["Fingerprint"] as string},Actor);if(Result(r))Saved("WorkOrderDetails",Id,"Maintenance started. Review the affected assignments before restoring service.");else{Bind();chkConfirm.Checked=false;}}
protected void Progress_Click(object sender,EventArgs e){var r=ServiceFactory.CreateMaintenanceService().RecordProgress(Action(),Actor);if(Result(r))Saved("WorkOrderDetails",Id,"Progress recorded.");}
protected void Cancel_Click(object sender,EventArgs e){var r=ServiceFactory.CreateMaintenanceService().CancelWorkOrder(Action(),Actor);if(Result(r))Saved("WorkOrderDetails",Id,"Work order cancelled. Vehicle status has not changed.");}
private MaintenanceActionRequest Action(){return new MaintenanceActionRequest{MaintenanceWorkOrderId=Id,RowVersion=Token("Rv"),Note=txtNote.Text};}
 }
}

