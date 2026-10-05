using System;
using System.Globalization;
using ForteMove.Models.Passengers;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Passenger
{
    public partial class Home : PassengerPage
    {
        protected void Page_Load(object sender, EventArgs e) { if (!IsPostBack) BindPage(); }

        private void BindPage()
        {
            PassengerHomeDetails value = ServiceFactory.CreatePassengerService().GetHome(CurrentPrincipalContext.UserAccountId);
            if (value == null) { Response.Redirect(ResolveUrl("~/Errors/Unexpected.aspx"), true); return; }
            litName.Text = Server.HtmlEncode(value.DisplayName);
            litBalance.Text = value.WalletBalance.ToString("C", CultureInfo.CurrentCulture);
            pnlNext.Visible = value.NextTicket != null; pnlNoNext.Visible = value.NextTicket == null;
            if (value.NextTicket != null)
            {
                lnkNext.HRef = ResolveUrl("~/Passenger/TicketDetails.aspx?id=" + value.NextTicket.TicketId.ToString(CultureInfo.InvariantCulture));
                litNextDate.Text = Server.HtmlEncode(value.NextTicket.ServiceDate.ToString("ddd, d MMM", CultureInfo.CurrentCulture) + " at " + value.NextTicket.ScheduledDepartureTime.ToString("hh\\:mm"));
                litNextRoute.Text = Server.HtmlEncode(value.NextTicket.RouteCode + " - " + value.NextTicket.RouteName);
                litNextEndpoints.Text = Server.HtmlEncode(value.NextTicket.OriginName + " to " + value.NextTicket.DestinationName);
            }
            rptActivity.DataSource = value.RecentTransactions; rptActivity.DataBind(); pnlNoActivity.Visible = value.RecentTransactions.Count == 0;
        }

        protected string FormatTransaction(object value) { return Convert.ToString(value).Replace("SimulatedTopUp", "Wallet top-up").Replace("TicketPurchase", "Ticket purchase").Replace("TripCancellationRefund", "Trip cancellation refund"); }
        protected string FormatOccurred(object value) { return ServiceFactory.CreatePassengerService().ToOperationalTime((DateTime)value).ToString("g", CultureInfo.CurrentCulture); }
        protected string FormatAmount(object type, object amount) { decimal value=(decimal)amount; bool debit=Convert.ToString(type)==WalletTransactionType.TicketPurchase.ToString(); return (debit?"- ":"+ ")+value.ToString("C",CultureInfo.CurrentCulture); }
    }
}
