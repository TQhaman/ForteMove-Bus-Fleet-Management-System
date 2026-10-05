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
 public partial class RepairProvidersPage : MaintenanceAdminPage
 {
protected void Page_Load(object sender,EventArgs e){if(!IsPostBack){BindFlash();Bind();var p=Id>0?ServiceFactory.CreateRepairProviderService().GetProvider(Id):null;litEditor.Text=p==null?"Add provider":P.Cell("Edit "+p.ProviderCode);if(Id>0&&p==null){Message("The provider was not found.");btnSave.Enabled=false;return;}if(p!=null){txtName.Text=p.ProviderName;txtArea.Text=p.AreaDescription;txtPhone.Text=p.Phone;txtEmail.Text=p.Email;chkActive.Checked=p.IsActive;ViewState["Rv"]=p.RowVersion;}}}
private void Bind(){litProviders.Text=P.Table(new[]{"Code","Provider","Area","Phone","Email","Status"},ServiceFactory.CreateRepairProviderService().GetProviders().Select(p=>new[]{P.Cell(p.ProviderCode,Link("RepairProviders",p.RepairProviderId)),P.Cell(p.ProviderName),P.Cell(p.AreaDescription),P.Cell(p.Phone),P.Cell(p.Email),P.Cell(p.IsActive?"Active":"Inactive")}),"No providers yet. Add an approved repair provider below.");}
protected void Save_Click(object sender,EventArgs e){var r=ServiceFactory.CreateRepairProviderService().SaveProvider(new SaveRepairProviderRequest{RepairProviderId=Id,RowVersion=Token("Rv"),ProviderName=txtName.Text,AreaDescription=txtArea.Text,Phone=txtPhone.Text,Email=txtEmail.Text,IsActive=chkActive.Checked},Actor);if(Result(r))Saved("RepairProviders",r.Value,"Repair provider saved.");}
 }
}

