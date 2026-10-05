using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using ForteMove.Models.Passengers;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Passenger
{
    public partial class Journeys : PassengerPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                var service=ServiceFactory.CreatePassengerService();
                txtDate.Text=service.OperationalToday.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
                ddlRoute.Items.Add(new ListItem("All Routes",string.Empty));
                foreach(var route in service.GetJourneyOptions()) ddlRoute.Items.Add(new ListItem(route.RouteCode+" - "+route.RouteName,route.RouteId.ToString(CultureInfo.InvariantCulture)));
                BindJourneys();
            }
        }

        protected void btnFind_Click(object sender, EventArgs e) { BindJourneys(); }

        private void BindJourneys()
        {
            DateTime date; long routeId;
            DateTime? selectedDate=DateTime.TryParseExact(txtDate.Text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date)?date:(DateTime?)null;
            long? selectedRoute=long.TryParse(ddlRoute.SelectedValue,NumberStyles.None,CultureInfo.InvariantCulture,out routeId)?routeId:(long?)null;
            var result=ServiceFactory.CreatePassengerService().FindJourneys(new JourneyQuery{ServiceDate=selectedDate,RouteId=selectedRoute,Search=txtSearch.Text});
            pnlErrors.Visible=!result.Succeeded;
            if(!result.Succeeded){rptErrors.DataSource=result.Errors;rptErrors.DataBind();rptJourneys.DataSource=null;rptJourneys.DataBind();pnlEmpty.Visible=false;return;}
            rptJourneys.DataSource=result.Value;rptJourneys.DataBind();pnlEmpty.Visible=result.Value.Count==0;
        }

        protected string FormatTime(object value){return ((TimeSpan)value).ToString("hh\\:mm");}
        protected string FormatStatus(object value){return Convert.ToString(value).Replace("InProgress","In progress");}
        protected string DelayText(object value){return value==null||value==DBNull.Value?string.Empty:"Estimated delay: "+Convert.ToInt32(value,CultureInfo.InvariantCulture).ToString(CultureInfo.CurrentCulture)+" minutes";}
    }
}
