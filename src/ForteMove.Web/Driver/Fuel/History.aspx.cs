using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using ForteMove.Business.Fuel;
using ForteMove.Models.Fuel;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Driver.Fuel
{
    public partial class HistoryPage : DriverPage
    {
        private long Actor { get { return CurrentPrincipalContext.UserAccountId; } }
        private long Id { get { return FuelPresentation.Id(Request.QueryString["id"]); } }
        private void Missing() { Response.StatusCode=404;pnlMessage.Visible=true;litMessage.Text="This Fuel item is unavailable."; }
protected void Page_Load(object sender,EventArgs e) { if(!IsPostBack) { ddlStatus.Items.Add(new ListItem("All",""));foreach(var state in Enum.GetNames(typeof(FuelRequestStatus))) ddlStatus.Items.Add(new ListItem(state,state));Bind(); } }
        protected void Filter(object sender,EventArgs e) { Bind(); }
        private void Bind()
        {
            var items=ServiceFactory.CreateDriverFuelService().GetRequests(new FuelQuery { Search=txtSearch.Text,Status=ddlStatus.SelectedValue },Actor);
            grid.DataSource=items.Where(x=>x.Status!=FuelRequestStatus.Pending).Select(x=>new { Id=x.FuelRequestId,Code=x.RequestCode,Submitted=FuelPresentation.Local(x.SubmittedUtc),
                Driver=x.Context.EmployeeNumber+" - "+x.Context.DriverName,Trip=x.Context.TripCode,Route=x.Context.RouteName,Bus=x.Context.FleetNumber,
                Supply=FuelPolicy.SupplyLabel(x.SupplyType),State=x.Status.ToString() }).ToList();grid.DataBind();
        }
    }
}
