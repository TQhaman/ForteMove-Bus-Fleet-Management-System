using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using ForteMove.Business.Fuel;
using ForteMove.Models.Fuel;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Admin.Fuel
{
    public partial class TransactionsPage : AdminPage
    {
        private long Actor { get { return CurrentPrincipalContext.UserAccountId; } }
        private long Id { get { return FuelPresentation.Id(Request.QueryString["id"]); } }
        private void Missing() { Response.StatusCode=404;pnlMessage.Visible=true;litMessage.Text="This Fuel item is unavailable."; }
protected void Page_Load(object sender,EventArgs e) { if(!IsPostBack) Bind(); }
        protected void Filter(object sender,EventArgs e) { Bind(); }
        private void Bind()
        { grid.DataSource=ServiceFactory.CreateAdminFuelService().GetTransactions(new FuelQuery { Search=txtSearch.Text },Actor).Select(x=>new { Id=x.FuelTransactionId,Code=x.TransactionCode,Voucher=x.VoucherCode,Trip=x.TripCode,Bus=x.FleetNumber,Station=x.StationName,Quantity=FuelPresentation.Quantity(x.Quantity,x.Unit),Amount=FuelPresentation.Money(x.Amount),Time=FuelPresentation.Local(x.RedeemedUtc) }).ToList();grid.DataBind(); }
    }
}
