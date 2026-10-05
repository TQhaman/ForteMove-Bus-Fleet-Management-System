using System;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls;
using ForteMove.Business.Fuel;
using ForteMove.Business.Time;
using ForteMove.Models.Common;
using ForteMove.Models.Fuel;
namespace ForteMove.Web.Infrastructure
{
    public static class FuelPresentation
    {
        public static long Id(string value) { long id;return long.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out id)&&id>0?id:0; }
        public static decimal? Decimal(string value) { decimal number;return decimal.TryParse(value,NumberStyles.Number,CultureInfo.CurrentCulture,out number)?(decimal?)number:null; }
        public static DateTime? Date(string value) { DateTime date;return DateTime.TryParseExact(value,"yyyy-MM-ddTHH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out date)?(DateTime?)date:null; }
        public static string Encode(object value) { return HttpUtility.HtmlEncode(Convert.ToString(value,CultureInfo.CurrentCulture)); }
        public static string Local(DateTime utc) { return new SystemClock().ToOperationalTime(utc).ToString("dd MMM yyyy HH:mm",CultureInfo.CurrentCulture); }
        public static string Money(decimal value) { return "R"+value.ToString("N2",CultureInfo.CurrentCulture); }
        public static string Quantity(decimal value,FuelQuantityUnit unit) { return value.ToString("N2",CultureInfo.CurrentCulture)+" "+FuelPolicy.UnitLabel(unit); }
        public static void Feedback<T>(ServiceResult<T> result,Panel panel,Literal message,string success)
        { panel.Visible=true;panel.CssClass=result.Succeeded?"alert alert-success":"alert alert-danger";message.Text=Encode(result.Succeeded?success:string.Join(" ",result.Errors.Select(e=>e.Message))); }
        public static string Context(FuelTripContext item)
        {
            return "<dl class='fuel-detail-grid'>"+Pair("Trip",item.TripCode)+Pair("Route",item.RouteName)+Pair("Driver",item.EmployeeNumber+" - "+item.DriverName)+
                Pair("Bus",item.FleetNumber)+Pair("Propulsion",item.PropulsionCode)+Pair("Trip status",item.TripStatus.ToString().Replace("InProgress","In progress"))+
                Pair("Bus odometer",item.OdometerKilometres.ToString("N1",CultureInfo.CurrentCulture)+" km")+Pair("Rated capacity (not current level)",Capacity(item))+Pair("Expected finish",item.ExpectedFinishLocal.ToString("dd MMM yyyy HH:mm"))+"</dl>";
        }
        private static string Capacity(FuelTripContext item)
        {
            var supply=FuelPolicy.Supply(item.PropulsionCode);
            if(!supply.HasValue) return "Unsupported supply type";
            decimal? value=supply.Value==FuelSupplyType.Diesel?item.FuelTankCapacityLitres:item.BatteryCapacityKwh;
            return value.HasValue?Quantity(value.Value,FuelPolicy.Unit(supply.Value)):"Not recorded";
        }
        public static string Voucher(FuelVoucherDetails item)
        {
            return "<dl class='fuel-detail-grid'>"+Pair("Voucher",item.VoucherCode)+Pair("Status",item.DisplayState)+Pair("Trip",item.Request.Context.TripCode)+
                Pair("Bus",item.FleetNumber)+Pair("Supply",FuelPolicy.SupplyLabel(item.SupplyType))+Pair("Authorized quantity",Quantity(item.ApprovedQuantity,item.Unit))+
                Pair("Authorized value",Money(item.ApprovedAmount))+Pair("Approved Station",item.StationCode+" - "+item.StationName)+Pair("Station area",item.StationArea)+
                Pair("Valid until",item.ValidUntilLocal.ToString("dd MMM yyyy HH:mm"))+Pair("Approved",Local(item.ApprovedUtc))+
                (item.RedeemedUtc.HasValue?Pair("Redeemed",Local(item.RedeemedUtc.Value)):"")+(string.IsNullOrWhiteSpace(item.CancellationReason)?"":Pair("Cancellation reason",item.CancellationReason))+"</dl>";
        }
        public static string Pair(string label,object value) { return "<div><dt>"+Encode(label)+"</dt><dd>"+Encode(value)+"</dd></div>"; }
        public static string Warnings(FuelTripContext context) { return string.Join(" ",FuelPolicy.Warnings(context,new SystemClock().OperationalNow)); }
    }
}
