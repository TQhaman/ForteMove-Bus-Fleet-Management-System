using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ForteMove.Models.Common;
using ForteMove.Models.Fuel;
using ForteMove.Models.Scheduling;
namespace ForteMove.Business.Fuel
{
    public static class FuelPolicy
    {
        public const decimal MaximumQuantity=99999999.99m;
        public const decimal MaximumAmount=9999999999.99m;
        public static FuelSupplyType? Supply(string propulsion)
        {
            if(propulsion=="Diesel") return FuelSupplyType.Diesel;
            if(propulsion=="Electric") return FuelSupplyType.ElectricCharging;
            return null;
        }
        public static FuelQuantityUnit Unit(FuelSupplyType supply) { return supply==FuelSupplyType.Diesel?FuelQuantityUnit.Litres:FuelQuantityUnit.KilowattHours; }
        public static string UnitLabel(FuelQuantityUnit unit) { return unit==FuelQuantityUnit.Litres?"L":"kWh"; }
        public static string SupplyLabel(FuelSupplyType supply) { return supply==FuelSupplyType.Diesel?"Diesel":"Electric charging"; }
        public static bool Eligible(FuelTripContext context)
        {
            return context!=null && context.AssignmentIsCurrent && context.DriverAccountActive &&
                (context.TripStatus==TripStatus.Scheduled || context.TripStatus==TripStatus.Ready ||
                 context.TripStatus==TripStatus.InProgress || context.TripStatus==TripStatus.Delayed) &&
                Supply(context.PropulsionCode).HasValue;
        }
        public static bool Compatible(FuelStation station,FuelSupplyType supply)
        {
            return station!=null && station.IsActive && (supply==FuelSupplyType.Diesel?station.SupportsDiesel:station.SupportsElectricCharging);
        }
        public static bool TokenVersion(byte[] value) { return value!=null && value.Length==8; }
        public static IList<ValidationError> ValidateApproval(FuelRequestDetails item,FuelStation station,ApproveFuelRequest request,DateTime now)
        {
            var errors=new List<ValidationError>();
            if(item==null || item.Status!=FuelRequestStatus.Pending || !Eligible(item.Context) || Supply(item.Context.PropulsionCode)!=item.SupplyType)
                errors.Add(new ValidationError("","The request is no longer Pending for an eligible current assignment."));
            if(!request.Quantity.HasValue || request.Quantity.Value<=0 || request.Quantity.Value>MaximumQuantity || decimal.Round(request.Quantity.Value,2)!=request.Quantity.Value)
                errors.Add(new ValidationError("Quantity","Enter a positive quantity with no more than two decimal places."));
            if(!request.Amount.HasValue || request.Amount.Value<0 || request.Amount.Value>MaximumAmount || decimal.Round(request.Amount.Value,2)!=request.Amount.Value)
                errors.Add(new ValidationError("Amount","Enter a nonnegative Rand amount with no more than two decimal places."));
            if(item!=null)
            {
                if(!Compatible(station,item.SupplyType)) errors.Add(new ValidationError("FuelStationId","Select an active Station supporting this supply type."));
                decimal? capacity=item.SupplyType==FuelSupplyType.Diesel?item.Context.FuelTankCapacityLitres:item.Context.BatteryCapacityKwh;
                if(capacity.HasValue && request.Quantity>capacity) errors.Add(new ValidationError("Quantity","Approved quantity cannot exceed the Bus's applicable tank or battery capacity."));
                if(!request.ValidUntilLocal.HasValue || request.ValidUntilLocal.Value<=now || request.ValidUntilLocal.Value>item.Context.ExpectedFinishLocal.AddHours(2))
                    errors.Add(new ValidationError("ValidUntilLocal","Validity must be in the future and no later than the Trip's expected finish plus two hours."));
            }
            if(request.Note!=null && request.Note.Trim().Length>500) errors.Add(new ValidationError("Note","Keep the approval note within 500 characters."));
            return errors;
        }
        public static FuelVoucherDisplayState DisplayState(FuelVoucherDetails voucher,DateTime now)
        {
            if(voucher.Status==FuelVoucherStatus.Cancelled) return FuelVoucherDisplayState.Cancelled;
            if(voucher.Status==FuelVoucherStatus.Redeemed) return FuelVoucherDisplayState.Redeemed;
            return now>voucher.ValidUntilLocal?FuelVoucherDisplayState.Expired:FuelVoucherDisplayState.Active;
        }
        public static string RedemptionBlock(FuelVoucherDetails voucher,FuelStation station,DateTime now)
        {
            if(voucher==null) return "The Voucher token is invalid or unavailable.";
            if(station==null || station.FuelStationId!=voucher.FuelStationId) return "Choose the approved Fuel Station.";
            if(voucher.Status==FuelVoucherStatus.Redeemed) return "Already redeemed.";
            if(voucher.Status==FuelVoucherStatus.Cancelled) return "This Voucher was cancelled.";
            if(now>voucher.ValidUntilLocal) return "This Voucher has expired.";
            var context=voucher.Request.Context;
            if(!Eligible(context) || Supply(context.PropulsionCode)!=voucher.SupplyType)
                return "The Voucher's original assignment is no longer eligible.";
            if(!Compatible(station,voucher.SupplyType)) return "The approved Station is inactive or no longer supports this supply type.";
            if(context.HasCriticalDefect) return "An unresolved Critical Bus defect prevents redemption.";
            return null;
        }
        public static IList<string> Warnings(FuelTripContext context,DateTime now)
        {
            var result=new List<string>();
            if(context.HasCriticalDefect) result.Add("An unresolved Critical Bus defect will block redemption.");
            if(context.HasOpenCannotProceed) result.Add("Cannot Proceed is open. Fuel redemption will not resolve it.");
            if(context.RequiresReview) result.Add("This Trip requires Administrator review.");
            if(context.VehicleStatus!="Operational") result.Add("Vehicle status: "+context.VehicleStatus+". Fuel does not change vehicle status.");
            if(context.LicenceExpiryDate.Date<now.Date || context.RoadworthyExpiryDate.Date<now.Date || context.InsuranceExpiryDate.Date<now.Date)
                result.Add("Bus compliance has expired. Fuel does not authorize the Bus to operate.");
            var supply=Supply(context.PropulsionCode);
            if(supply.HasValue && !(supply==FuelSupplyType.Diesel?context.FuelTankCapacityLitres:context.BatteryCapacityKwh).HasValue)
                result.Add("Applicable Bus capacity is not recorded; current fuel or charge level is unknown.");
            return result;
        }
        public static string Fingerprint(FuelVoucherDetails voucher,FuelStation station)
        {
            string text=voucher.FuelVoucherId.ToString(CultureInfo.InvariantCulture)+"|"+Convert.ToBase64String(voucher.RowVersion)+"|"+
                Convert.ToBase64String(station.RowVersion)+"|"+voucher.ApprovedQuantity.ToString(CultureInfo.InvariantCulture)+"|"+
                voucher.ApprovedAmount.ToString(CultureInfo.InvariantCulture)+"|"+voucher.ValidUntilLocal.ToString("O",CultureInfo.InvariantCulture);
            using(var hash=SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }
    }
}

