using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using ForteMove.Business.Fuel;
using ForteMove.Models.Fuel;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Admin.Fuel
{
    public partial class StationsPage : AdminPage
    {
        private long Actor { get { return CurrentPrincipalContext.UserAccountId; } }
        private long Id { get { return FuelPresentation.Id(Request.QueryString["id"]); } }
        private void Missing() { Response.StatusCode=404;pnlMessage.Visible=true;litMessage.Text="This Fuel item is unavailable."; }
        protected void Page_Load(object sender,EventArgs e)
        {
            grid.DataSource=ServiceFactory.CreateFuelStationService().GetStations(Actor).Select(x=>new { Id=x.FuelStationId,Code=x.StationCode,Name=x.StationName,Area=x.AreaDescription,Supplies=(x.SupportsDiesel?"Diesel":"")+(x.SupportsDiesel&&x.SupportsElectricCharging?" / ":"")+(x.SupportsElectricCharging?"Electric charging":""),State=x.IsActive?"Active":"Inactive" }).ToList();grid.DataBind();
            if(Request.QueryString["saved"]=="1") { pnlMessage.Visible=true;pnlMessage.CssClass="alert alert-success";litMessage.Text="Fuel Station saved."; }
        }
    }
}
