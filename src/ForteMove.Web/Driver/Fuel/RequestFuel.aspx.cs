using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using ForteMove.Business.Fuel;
using ForteMove.Models.Fuel;
using ForteMove.Web.Infrastructure;
namespace ForteMove.Web.Driver.Fuel
{
    public partial class RequestFuelPage : DriverPage
    {
        private long Actor { get { return CurrentPrincipalContext.UserAccountId; } }
        private long Id { get { return FuelPresentation.Id(Request.QueryString["id"]); } }
        private void Missing() { Response.StatusCode=404;pnlMessage.Visible=true;litMessage.Text="This Fuel item is unavailable."; }
protected void Page_Load(object sender,EventArgs e)
        {
            if(IsPostBack) return;
            ViewState["Submission"]=Guid.NewGuid();
            ddlTrips.Items.Add(new ListItem("Select your Trip",""));
            foreach(var trip in ServiceFactory.CreateDriverFuelService().GetEligibleTrips(Actor))
                ddlTrips.Items.Add(new ListItem(trip.TripCode+" - "+trip.RouteName+" - "+trip.FleetNumber,trip.TripId.ToString(CultureInfo.InvariantCulture)));
            if(Id>0 && ddlTrips.Items.FindByValue(Id.ToString(CultureInfo.InvariantCulture))!=null) ddlTrips.SelectedValue=Id.ToString(CultureInfo.InvariantCulture);
            Bind();
        }
        protected void SelectTrip(object sender,EventArgs e) { ViewState["Submission"]=Guid.NewGuid();Bind(); }
        private void Bind()
        {
            var trip=ServiceFactory.CreateDriverFuelService().GetTrip(FuelPresentation.Id(ddlTrips.SelectedValue),Actor);
            btnSubmit.Enabled=FuelPolicy.Eligible(trip);litContext.Text=trip==null?"":FuelPresentation.Context(trip);
            if(trip==null) { ViewState.Remove("TripRv");ViewState.Remove("AssignmentRv");pnlWarnings.Visible=false;return; }
            ViewState["TripRv"]=trip.TripRowVersion;ViewState["AssignmentRv"]=trip.AssignmentRowVersion;
            litWarnings.Text=FuelPresentation.Encode(FuelPresentation.Warnings(trip));pnlWarnings.Visible=litWarnings.Text.Length>0;
        }
        protected void Submit(object sender,EventArgs e)
        {
            var result=ServiceFactory.CreateDriverFuelService().Submit(new SubmitFuelRequest {
                TripId=FuelPresentation.Id(ddlTrips.SelectedValue),Justification=txtReason.Text,SubmissionToken=ViewState["Submission"] is Guid?(Guid)ViewState["Submission"]:Guid.Empty,
                TripRowVersion=ViewState["TripRv"] as byte[],AssignmentRowVersion=ViewState["AssignmentRv"] as byte[] },Actor);
            if(result.Succeeded) { Response.Redirect(ResolveUrl("~/Driver/Fuel/RequestDetails.aspx?id="+result.Value.FuelRequestId+"&submitted=1"),true);return; }
            FuelPresentation.Feedback(result,pnlMessage,litMessage,"Request submitted.");Bind();
        }
    }
}
