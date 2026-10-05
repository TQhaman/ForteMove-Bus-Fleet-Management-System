using System;
using System.Globalization;
using ForteMove.Models.Passengers;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Passenger
{
    public partial class Tickets : PassengerPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if(!IsPostBack){var service=ServiceFactory.CreatePassengerService();var upcoming=service.GetTickets(CurrentPrincipalContext.UserAccountId,TicketListMode.Upcoming);var history=service.GetTickets(CurrentPrincipalContext.UserAccountId,TicketListMode.History);rptUpcoming.DataSource=upcoming;rptUpcoming.DataBind();pnlNoUpcoming.Visible=upcoming.Count==0;rptHistory.DataSource=history;rptHistory.DataBind();pnlNoHistory.Visible=history.Count==0;}
        }
        protected string FormatService(object date,object time){return ((DateTime)date).ToString("ddd, d MMM",CultureInfo.CurrentCulture)+" at "+((TimeSpan)time).ToString("hh\\:mm");}
        protected string HistoryLabel(object value){return Convert.ToString(value)==TicketStatus.Refunded.ToString()?"Refunded":"Past ticket";}
    }
}
