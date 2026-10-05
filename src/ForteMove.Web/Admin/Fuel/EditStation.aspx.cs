using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using ForteMove.Business.Fuel;
using ForteMove.Models.Fuel;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Admin.Fuel
{
    public partial class EditStationPage : AdminPage
    {
        private long Actor { get { return CurrentPrincipalContext.UserAccountId; } }
        private long Id { get { return FuelPresentation.Id(Request.QueryString["id"]); } }
        private void Missing() { Response.StatusCode=404;pnlMessage.Visible=true;litMessage.Text="This Fuel item is unavailable."; }
protected void Page_Load(object sender,EventArgs e)
        {
            if(IsPostBack) return;var item=ServiceFactory.CreateFuelStationService().GetStation(Id,Actor);if(item==null) { Missing();btnSave.Enabled=false;return; }
            litCode.Text=FuelPresentation.Encode(item.StationCode);txtName.Text=item.StationName;txtArea.Text=item.AreaDescription;chkActive.Checked=item.IsActive;
            chkDiesel.Checked=item.SupportsDiesel;chkElectric.Checked=item.SupportsElectricCharging;ViewState["Rv"]=item.RowVersion;
        }
        protected void Save(object sender,EventArgs e)
        {
            var result=ServiceFactory.CreateFuelStationService().Save(new SaveFuelStationRequest { FuelStationId=Id,StationName=txtName.Text,AreaDescription=txtArea.Text,IsActive=chkActive.Checked,
                SupportsDiesel=chkDiesel.Checked,SupportsElectricCharging=chkElectric.Checked,RowVersion=ViewState["Rv"] as byte[] },Actor);
            if(result.Succeeded) { Response.Redirect(ResolveUrl("~/Admin/Fuel/Stations.aspx?saved=1"),true);return; }
            FuelPresentation.Feedback(result,pnlMessage,litMessage,"Station saved.");
        }
    }
}
