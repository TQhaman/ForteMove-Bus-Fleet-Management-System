using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using ForteMove.Business.Fuel;
using ForteMove.Business.Identifiers;
using ForteMove.Business.Services;
using ForteMove.Business.Time;
using ForteMove.Models.Fuel;
using ForteMove.Models.Scheduling;

internal static class Slice8BusinessVerification
{
    static int checks;
    static readonly DateTime Now=new DateTime(2026,10,6,10,0,0);
    static byte[] Rv=new byte[8];
    public static int Main()
    {
        try
        {
            Check(FuelPolicy.Supply("Diesel")==FuelSupplyType.Diesel,"Diesel mapping");
            Check(FuelPolicy.Supply("Electric")==FuelSupplyType.ElectricCharging,"Electric mapping");
            foreach(string propulsion in new[]{"Petrol","Hybrid","Hydrogen","",null})
                Check(!FuelPolicy.Supply(propulsion).HasValue,"Unsupported propulsion is not guessed");
            Check(FuelPolicy.Unit(FuelSupplyType.Diesel)==FuelQuantityUnit.Litres,"Diesel litres");
            Check(FuelPolicy.Unit(FuelSupplyType.ElectricCharging)==FuelQuantityUnit.KilowattHours,"Electric kWh");
            foreach(TripStatus state in Enum.GetValues(typeof(TripStatus)))
            {
                var c=Context();c.TripStatus=state;
                Check(FuelPolicy.Eligible(c)==(state==TripStatus.Scheduled||state==TripStatus.Ready||state==TripStatus.InProgress||state==TripStatus.Delayed),"Controlled request state "+state);
            }
            var context=Context();context.AssignmentIsCurrent=false;Check(!FuelPolicy.Eligible(context),"Historical assignment denied");
            context=Context();context.DriverAccountActive=false;Check(!FuelPolicy.Eligible(context),"Inactive Driver denied");
            context=Context();context.HasCriticalDefect=true;context.HasOpenCannotProceed=true;context.RequiresReview=true;
            Check(FuelPolicy.Eligible(context),"Critical/exception/review does not suppress request");
            Check(!FuelPolicy.Eligible(null),"Missing context denied");
            var request=Request();var station=Station();var item=Item();
            Check(FuelPolicy.ValidateApproval(item,station,request,Now).Count==0,"Valid zero-money approval");
            foreach(decimal? quantity in new decimal?[]{null,0,-1,1.001m,FuelPolicy.MaximumQuantity+0.01m,101})
            {request=Request();request.Quantity=quantity;Check(FuelPolicy.ValidateApproval(item,station,request,Now).Any(x=>x.Field=="Quantity"),"Quantity boundary "+quantity);}
            foreach(decimal? amount in new decimal?[]{null,-1,1.001m,FuelPolicy.MaximumAmount+0.01m})
            {request=Request();request.Amount=amount;Check(FuelPolicy.ValidateApproval(item,station,request,Now).Any(x=>x.Field=="Amount"),"Amount boundary "+amount);}
            request=Request();request.Amount=FuelPolicy.MaximumAmount;
            Check(FuelPolicy.ValidateApproval(item,station,request,Now).Count==0,"Maximum money supported");
            request=Request();request.Quantity=100;Check(FuelPolicy.ValidateApproval(item,station,request,Now).Count==0,"Capacity inclusive");
            item.Context.FuelTankCapacityLitres=null;request.Quantity=FuelPolicy.MaximumQuantity;
            Check(FuelPolicy.ValidateApproval(item,station,request,Now).Count==0,"Missing capacity permits supported quantity with warning");
            Check(FuelPolicy.Warnings(item.Context,Now).Any(x=>x.Contains("not recorded")),"Missing capacity warning");
            item=Item();item.SupplyType=FuelSupplyType.ElectricCharging;item.Context.PropulsionCode="Electric";item.Context.BatteryCapacityKwh=50;
            request=Request();request.Quantity=51;Check(FuelPolicy.ValidateApproval(item,station,request,Now).Any(x=>x.Field=="Quantity"),"Electric capacity authoritative");
            request.Quantity=50;Check(FuelPolicy.ValidateApproval(item,station,request,Now).Count==0,"Electric capacity inclusive");
            item=Item();request=Request();
            foreach(DateTime end in new[]{Now,Now.AddSeconds(-1),item.Context.ExpectedFinishLocal.AddHours(2).AddSeconds(1)})
            {request.ValidUntilLocal=end;Check(FuelPolicy.ValidateApproval(item,station,request,Now).Any(x=>x.Field=="ValidUntilLocal"),"Validity boundary");}
            request=Request();request.ValidUntilLocal=item.Context.ExpectedFinishLocal.AddHours(2);
            Check(FuelPolicy.ValidateApproval(item,station,request,Now).Count==0,"Max validity inclusive");
            station.IsActive=false;Check(FuelPolicy.ValidateApproval(item,station,request,Now).Any(x=>x.Field=="FuelStationId"),"Inactive station");
            station=Station();station.SupportsDiesel=false;Check(FuelPolicy.ValidateApproval(item,station,request,Now).Count>0,"Wrong station capability");
            item.Status=FuelRequestStatus.Rejected;Check(FuelPolicy.ValidateApproval(item,Station(),Request(),Now).Count>0,"Already decided request");
            item=Item();item.Context.PropulsionCode="Electric";Check(FuelPolicy.ValidateApproval(item,Station(),Request(),Now).Count>0,"Supply change invalidates approval");
            var v=Voucher();station=Station();
            Check(FuelPolicy.RedemptionBlock(v,station,Now)==null,"Valid redemption");
            Check(FuelPolicy.RedemptionBlock(v,station,v.ValidUntilLocal)==null,"Deadline equality redeemable");
            Check(FuelPolicy.DisplayState(v,v.ValidUntilLocal.AddSeconds(1))==FuelVoucherDisplayState.Expired,"Derived expiry strictly later");
            Check(FuelPolicy.RedemptionBlock(v,station,v.ValidUntilLocal.AddSeconds(1)).Contains("expired"),"Expired denied");
            v.Request.Context.HasOpenCannotProceed=true;Check(FuelPolicy.RedemptionBlock(v,station,Now)==null,"Cannot Proceed does not block redemption");
            v.Request.Context.VehicleStatus="OutOfService";v.Request.Context.LicenceExpiryDate=Now.AddDays(-1);
            Check(FuelPolicy.RedemptionBlock(v,station,Now)==null && FuelPolicy.Warnings(v.Request.Context,Now).Count>=3,"Compliance/status warning only");
            v.Request.Context.HasCriticalDefect=true;Check(FuelPolicy.RedemptionBlock(v,station,Now).Contains("Critical"),"Critical defect hard block");
            v=Voucher();v.Request.Context.AssignmentIsCurrent=false;Check(FuelPolicy.RedemptionBlock(v,station,Now)!=null,"Changed assignment invalid");
            foreach(TripStatus s in new[]{TripStatus.Completed,TripStatus.Cancelled,TripStatus.Unassigned})
            {v=Voucher();v.Request.Context.TripStatus=s;Check(FuelPolicy.RedemptionBlock(v,station,Now)!=null,"Terminal/unassigned redemption block");}
            v=Voucher();v.Status=FuelVoucherStatus.Redeemed;Check(FuelPolicy.RedemptionBlock(v,station,Now).Contains("Already"),"Already redeemed");
            Check(FuelPolicy.DisplayState(v,Now.AddYears(1))==FuelVoucherDisplayState.Redeemed,"Terminal state wins over expiry");
            v.Status=FuelVoucherStatus.Cancelled;Check(FuelPolicy.RedemptionBlock(v,station,Now).Contains("cancelled"),"Cancelled voucher");
            station.FuelStationId=2;Check(FuelPolicy.RedemptionBlock(Voucher(),station,Now).Contains("approved"),"Wrong station");
            v=Voucher();station=Station();string fp=FuelPolicy.Fingerprint(v,station);
            foreach(string culture in new[]{"en-ZA","fr-FR","ar-SA"})
            {System.Threading.Thread.CurrentThread.CurrentCulture=new CultureInfo(culture);Check(FuelPolicy.Fingerprint(v,station)==fp,"Fingerprint culture independence");}
            v.ApprovedAmount++;Check(FuelPolicy.Fingerprint(v,station)!=fp,"Amount bound to review");
            v=Voucher();station.RowVersion=new byte[]{1,0,0,0,0,0,0,0};
            Check(FuelPolicy.Fingerprint(v,station)!=fp,"Station rowversion bound to review");
            var tokens=new HashSet<string>();
            for(int i=0;i<256;i++){string token=FuelRedemptionToken.Create();tokens.Add(token);if(FuelRedemptionToken.Hash(token)==null)throw new Exception("Token syntax");}
            Check(tokens.Count==256,"Random tokens unique in sample");
            Check(FuelRedemptionToken.Hash(tokens.First()).Length==32,"SHA256 lookup");
            foreach(string t in new[]{null,"","FV-000001","FMFV1.short",tokens.First()+"x"})
                Check(FuelRedemptionToken.Hash(t)==null,"Malformed token rejected");
            Check(IdentifierCodePolicy.FormatFuelRequestCode(1)=="FR-000001","FR code");
            Check(IdentifierCodePolicy.FormatFuelVoucherCode(1000000)=="FV-1000000","FV expands width");
            Check(IdentifierCodePolicy.FormatFuelTransactionCode(1)=="FTX-000001","FTX code");
            Check(IdentifierCodePolicy.FormatFuelStationCode(1)=="FS-001","Station code");
            var driver=new DriverFuelService(null,null,new FixedClock());
            Check(!driver.Submit(null,1).Succeeded,"Missing submission");
            Check(!driver.Submit(new SubmitFuelRequest{TripId=1,TripRowVersion=Rv,AssignmentRowVersion=Rv,SubmissionToken=Guid.NewGuid(),Justification=" "},1).Succeeded,"Blank justification");
            Check(!driver.Submit(new SubmitFuelRequest{TripId=1,TripRowVersion=Rv,AssignmentRowVersion=Rv,SubmissionToken=Guid.NewGuid(),Justification=new string('a',501)},1).Succeeded,"Long justification");
            var admin=new AdminFuelService(null,null,null,new FixedClock());
            Check(!admin.Approve(null,1).Succeeded,"Missing approval tokens");
            Check(!admin.Approve(new ApproveFuelRequest{FuelStationId=0},1).Succeeded,"Choose a Station before approval");
            Check(!admin.Redeem(null,1).Succeeded,"No review cannot redeem");
            Check(!admin.Reject(new RejectFuelRequest{RowVersion=Rv,Reason=" "},1).Succeeded,"Rejection reason required");
            var stations=new FuelStationService(null,new FixedClock());
            Check(!stations.Save(new SaveFuelStationRequest{StationName="x",AreaDescription="y"},1).Succeeded,"Capability required");
            Check(!stations.Save(new SaveFuelStationRequest{StationName="",AreaDescription="y",SupportsDiesel=true},1).Succeeded,"Station name required");
            Console.WriteLine(checks+" Slice 8 Business checks passed. No database writes.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    public static FuelTripContext Context(){return new FuelTripContext{TripId=1,TripAssignmentId=1,BusId=1,DriverProfileId=1,DriverAccountActive=true,AssignmentIsCurrent=true,TripStatus=TripStatus.Scheduled,PropulsionCode="Diesel",ExpectedFinishLocal=Now.AddHours(1),FuelTankCapacityLitres=100,VehicleStatus="Operational",LicenceExpiryDate=Now.AddYears(1),RoadworthyExpiryDate=Now.AddYears(1),InsuranceExpiryDate=Now.AddYears(1)};}
    static FuelStation Station(){return new FuelStation{FuelStationId=1,IsActive=true,SupportsDiesel=true,SupportsElectricCharging=true,RowVersion=Rv};}
    static FuelRequestDetails Item(){return new FuelRequestDetails{FuelRequestId=1,Context=Context(),SupplyType=FuelSupplyType.Diesel,Status=FuelRequestStatus.Pending,RowVersion=Rv};}
    static ApproveFuelRequest Request(){return new ApproveFuelRequest{FuelRequestId=1,FuelStationId=1,Quantity=40,Amount=0,ValidUntilLocal=Now.AddHours(2)};}
    static FuelVoucherDetails Voucher(){return new FuelVoucherDetails{FuelVoucherId=1,Request=Item(),FuelStationId=1,SupplyType=FuelSupplyType.Diesel,Status=FuelVoucherStatus.Active,ApprovedAmount=0,ApprovedQuantity=40,ValidUntilLocal=Now.AddHours(2),RowVersion=Rv};}
    static void Check(bool result,string name){if(!result)throw new InvalidOperationException(name);checks++;}
    sealed class FixedClock:IClock
    {public DateTime UtcNow{get{return Now.AddHours(-2);}} public DateTime Today{get{return Now.Date;}} public DateTime OperationalNow{get{return Now;}}public DateTime ToOperationalTime(DateTime utc){return utc.AddHours(2);}}
}
