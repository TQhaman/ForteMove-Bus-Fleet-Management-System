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
 public partial class CompleteWorkOrderPage : MaintenanceAdminPage
 {
protected void Page_Load(object sender,EventArgs e){if(!IsPostBack){var w=ServiceFactory.CreateMaintenanceService().GetWorkOrder(Id);if(w==null||w.Status!=MaintenanceOrderStatus.InProgress){Message("Only work in progress can be completed.");btnComplete.Enabled=false;return;}ViewState["Rv"]=w.RowVersion;ViewState["BusRv"]=w.BusRowVersion;ViewState["PlanRv"]=w.PlanRowVersion;ViewState["DefectRv"]=w.DefectRowVersion;txtOdo.Text=w.CurrentOdometer.ToString(CultureInfo.InvariantCulture);pnlDefect.Visible=w.BusDefectReportId.HasValue&&w.DefectStatus!="Resolved";litContext.Text="<h2>"+P.Cell(w.WorkOrderCode)+"</h2><p>"+P.Cell(w.FleetNumber)+"; current odometer "+P.Cell(P.Odo(w.CurrentOdometer))+"; provider "+P.Cell(w.ProviderNameSnapshot)+"</p><p>Linked plan: "+P.Cell(w.PlanCode)+"; linked defect: "+P.Cell(w.DefectCode)+" "+P.Cell(w.DefectStatus)+"</p>";}}
protected void Complete_Click(object sender,EventArgs e){try{var r=ServiceFactory.CreateMaintenanceService().CompleteWorkOrder(new CompleteMaintenanceRequest{MaintenanceWorkOrderId=Id,RowVersion=Token("Rv"),BusRowVersion=Token("BusRv"),PlanRowVersion=Token("PlanRv"),DefectRowVersion=Token("DefectRv"),ServiceOdometer=Number(txtOdo),WorkPerformed=txtWork.Text,Note=txtNote.Text,ExternalReference=txtReference.Text,CompletedCost=Number(txtCost),ResolveLinkedDefect=chkResolve.Checked,ResolutionNote=txtResolution.Text},Actor);if(Result(r))Saved("WorkOrderDetails",Id,"Completion recorded. The bus remains Under maintenance.");}catch(FormatException x){Message(x.Message);}}
 }
}

