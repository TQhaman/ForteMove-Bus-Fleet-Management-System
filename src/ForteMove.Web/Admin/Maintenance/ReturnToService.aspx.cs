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
 public partial class ReturnToServicePage : MaintenanceAdminPage
 {
protected void Page_Load(object sender,EventArgs e){if(!IsPostBack){var s=ServiceFactory.CreateMaintenanceService();var r=s.GetReturnToServiceReview(Id);if(r==null){Message("The bus was not found.");btnReturn.Enabled=false;return;}ViewState["BusRv"]=r.Bus.RowVersion;lnkHistory.NavigateUrl=Link("ServiceHistory",Id);var blocks=s.GetReturnBlockers(r);bool valid=r.Bus.BaseOperationalState==ForteMove.Models.Fleet.BusOperationalState.OutOfService||r.Bus.BaseOperationalState==ForteMove.Models.Fleet.BusOperationalState.UnderMaintenance;btnReturn.Enabled=valid&&blocks.Count==0;litReview.Text="<h2>"+P.Cell(r.Bus.FleetNumber)+"</h2><p>Current status: "+P.Cell(r.Bus.BaseOperationalState)+"</p>"+(blocks.Count>0?"<div class=\"alert alert-warning\">"+P.Cell(string.Join(" ",blocks))+"</div>":valid?"<p>Current safety checks allow return to service. They will be checked again when you confirm.</p>":"<p>This vehicle is not eligible for this return-to-service workflow.</p>");}}
protected void Return_Click(object sender,EventArgs e){var r=ServiceFactory.CreateMaintenanceService().ReturnToService(new ReturnToServiceRequest{BusId=Id,BusRowVersion=Token("BusRv"),Note=txtNote.Text},Actor);if(Result(r))Saved("ServiceHistory",Id,"Bus returned to service. Resolve affected assignment reviews separately.");}
 }
}

