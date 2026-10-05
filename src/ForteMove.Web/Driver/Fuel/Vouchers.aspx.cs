using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using ForteMove.Business.Fuel;
using ForteMove.Models.Fuel;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Driver.Fuel
{
    public partial class VouchersPage : DriverPage
    {
        private long Actor { get { return CurrentPrincipalContext.UserAccountId; } }
        private long Id { get { return FuelPresentation.Id(Request.QueryString["id"]); } }
        private void Missing() { Response.StatusCode=404;pnlMessage.Visible=true;litMessage.Text="This Fuel item is unavailable."; }
protected void Page_Load(object sender,EventArgs e) { if(!IsPostBack) { ddlStatus.Items.Add(new ListItem("All",""));foreach(var state in Enum.GetNames(typeof(FuelVoucherDisplayState))) ddlStatus.Items.Add(new ListItem(state,state));ddlStatus.SelectedValue="Active";Bind(); } }
        protected void Filter(object sender,EventArgs e) { Bind(); }
        private void Bind()
        {
            var items=ServiceFactory.CreateDriverFuelService().GetVouchers(new FuelQuery { Search=txtSearch.Text },Actor).Where(x=>ddlStatus.SelectedValue=="" || x.DisplayState.ToString()==ddlStatus.SelectedValue);
            grid.DataSource=items.Select(x=>new { Id=x.FuelVoucherId,Code=x.VoucherCode,Trip=x.Request.Context.TripCode,Bus=x.FleetNumber,Supply=FuelPolicy.SupplyLabel(x.SupplyType),
                Quantity=FuelPresentation.Quantity(x.ApprovedQuantity,x.Unit),Amount=FuelPresentation.Money(x.ApprovedAmount),Station=x.StationName,Until=x.ValidUntilLocal.ToString("dd MMM yyyy HH:mm"),State=x.DisplayState.ToString() }).ToList();grid.DataBind();
        }
    }
}
