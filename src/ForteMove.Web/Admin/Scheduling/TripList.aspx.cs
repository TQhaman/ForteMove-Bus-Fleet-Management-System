using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.UI.WebControls;
using ForteMove.Models.Scheduling;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Admin.Scheduling
{
    public partial class TripList : AdminPage
    {
        protected void Page_Load(object sender, EventArgs e) { if(!IsPostBack){BindOptions();BindTrips();} }
        protected void btnApply_Click(object sender,EventArgs e){BindTrips();}
        protected void btnClear_Click(object sender,EventArgs e){Response.Redirect(ResolveUrl("~/Admin/Scheduling/TripList.aspx"),true);}
        protected string FormatDate(object value){return Convert.ToDateTime(value).ToString("dd MMM yyyy",CultureInfo.CurrentCulture);}
        protected string FormatTime(object value){return DateTime.Today.Add((TimeSpan)value).ToString("HH:mm",CultureInfo.CurrentCulture);}
        protected string FormatFinish(object serviceDate,object finish)
        {
            DateTime service=Convert.ToDateTime(serviceDate).Date; DateTime expected=Convert.ToDateTime(finish);
            return expected.Date==service?expected.ToString("HH:mm",CultureInfo.CurrentCulture):expected.ToString("dd MMM HH:mm",CultureInfo.CurrentCulture);
        }
        protected string FormatStatus(object status,object review){return Convert.ToBoolean(review)?"Review required":Convert.ToString(status).Replace("InProgress","In progress");}
        protected string GetStatusCss(object status,object review)
        {
            if(Convert.ToBoolean(review))return "status-badge status-warning";
            string value=Convert.ToString(status); return value=="Completed"?"status-badge status-success":value=="Cancelled"?"status-badge status-neutral":"status-badge status-info";
        }
        protected string FormatAssignment(object dataItem)
        {
            TripListItem trip = dataItem as TripListItem;
            if (trip == null || string.IsNullOrWhiteSpace(trip.AssignedFleetNumber)) return "Unassigned";
            return trip.AssignedFleetNumber + " · " + trip.AssignedEmployeeNumber + " · " + trip.AssignedDriverName;
        }
        private void BindOptions()
        {
            SchedulingCreationOptions options=ServiceFactory.CreateSchedulingService().GetCreationOptions();
            ddlRoute.Items.Add(new ListItem("All Routes",string.Empty)); foreach(ScheduleRouteOption route in options.Routes)ddlRoute.Items.Add(new ListItem(route.RouteCode+" - "+route.RouteName,route.RouteId.ToString(CultureInfo.InvariantCulture)));
            ddlStatus.Items.Add(new ListItem("All statuses",string.Empty)); foreach(TripStatus status in Enum.GetValues(typeof(TripStatus)))ddlStatus.Items.Add(new ListItem(status.ToString().Replace("InProgress","In progress"),status.ToString()));
        }
        private void BindTrips()
        {
            DateTime date; long route; TripStatus status;
            TripQuery query=new TripQuery
            {
                ServiceDate=DateTime.TryParseExact(txtServiceDate.Text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date)?date:(DateTime?)null,
                RouteId=long.TryParse(ddlRoute.SelectedValue,NumberStyles.None,CultureInfo.InvariantCulture,out route)?route:(long?)null,
                Status=Enum.TryParse(ddlStatus.SelectedValue,out status)?status:(TripStatus?)null
            };
            IList<TripListItem> items=ServiceFactory.CreateSchedulingService().GetTripList(query); bool any=items.Count>0;
            pnlResults.Visible=any;pnlEmpty.Visible=!any; if(any){rptTrips.DataSource=items;rptTrips.DataBind();litCount.Text=items.Count==1?"1 Trip matches the current view.":string.Format(CultureInfo.CurrentCulture,"{0:N0} Trips match the current view.",items.Count);}
        }
    }
}
