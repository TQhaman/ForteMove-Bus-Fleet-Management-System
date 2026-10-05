using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ForteMove.Business.Fleet;
using ForteMove.Business.Maintenance;
using ForteMove.Business.Identifiers;
using ForteMove.Business.Services;
using ForteMove.Business.Time;
using ForteMove.Models.Maintenance;
using ForteMove.Models.Fleet;
using ForteMove.Models.Assignments;
using ForteMove.Models.Passengers;
using ForteMove.Models.Drivers;
using ForteMove.Models.Scheduling;
internal static class Slice9BusinessVerification
{
 static int checks;static DateTime Today=new DateTime(2026,10,6);
 static void Check(bool x,string label){if(!x)throw new Exception(label);checks++;}
 static MaintenancePlan Plan(){return new MaintenancePlan{BusId=1,PlanCode="MP-000001",ServiceName="Verification only",IsActive=true,BlocksOperationWhenOverdue=true,NextDueDate=Today,NextDueOdometer=100};}
 public static int Main(){try{
 var p=Plan();
 foreach(int days in new[]{-1,0,1}){p.NextDueDate=Today.AddDays(days);p.NextDueOdometer=null;Check(MaintenancePolicy.Evaluate(p,Today,100)==(days<0?MaintenanceDueState.Overdue:days==0?MaintenanceDueState.Due:MaintenanceDueState.NotDue),"Date boundary "+days);}
 foreach(decimal odo in new[]{99.9m,100m,100.1m}){p.NextDueDate=null;p.NextDueOdometer=100;Check(MaintenancePolicy.Evaluate(p,Today,odo)==(odo<100?MaintenanceDueState.NotDue:odo==100?MaintenanceDueState.Due:MaintenanceDueState.Overdue),"Odometer boundary "+odo);}
 p=Plan();p.CurrentOdometer=101;MaintenancePolicy.Describe(p,Today);Check(p.DueState==MaintenanceDueState.Overdue&&p.DueReason.Contains("date")&&p.DueReason.Contains("odometer"),"Combined urgency and both reasons");Check(p.BlocksOperation,"Only overdue blocks");p.CurrentOdometer=100;MaintenancePolicy.Describe(p,Today);Check(!p.BlocksOperation,"Due equality does not block");
 p.IsActive=false;Check(MaintenancePolicy.Evaluate(p,Today.AddDays(100),999)==MaintenanceDueState.NotDue,"Inactive ignored");p.IsActive=true;p.BlocksOperationWhenOverdue=false;Check(BusSafetyPolicy.MaintenanceBlocks(new[]{p},Today.AddDays(1),200).Count==0,"Advisory not blocking");p.BlocksOperationWhenOverdue=true;Check(BusSafetyPolicy.MaintenanceBlocks(new[]{p},Today.AddDays(1),200).Count==1,"Shared hard blocker");
 var r=new SaveMaintenancePlanRequest{BusId=1,ServiceName="Oil service",IntervalDays=30,IntervalKilometres=1000,UseVerifiedBaseline=true,LastServiceDate=Today.AddDays(-10),LastServiceOdometer=50,IsActive=true};Check(MaintenancePolicy.ValidatePlan(r,null,100,Today).Count==0&&r.NextDueDate==Today.AddDays(20)&&r.NextDueOdometer==1050,"Verified baseline arithmetic");
 r.UseVerifiedBaseline=false;r.NextDueDate=Today.AddDays(1);r.NextDueOdometer=200;Check(MaintenancePolicy.ValidatePlan(r,null,100,Today).Count==0&&!r.LastServiceDate.HasValue&&!r.LastServiceOdometer.HasValue,"First-due is not past-service history");
 var prior=new MaintenancePlan{BusId=1,LastCompletedWorkOrderId=4,LastServiceDate=Today,LastServiceOdometer=100};r.UseVerifiedBaseline=false;r.LastServiceDate=Today.AddDays(-50);r.LastServiceOdometer=0;Check(MaintenancePolicy.ValidatePlan(r,prior,100,Today).Count==0&&r.LastServiceDate==Today&&r.NextDueOdometer==1100,"Completed baseline cannot be invented/replaced");
 r.IntervalDays=int.MaxValue;Check(MaintenancePolicy.ValidatePlan(r,prior,100,Today).Count>0,"Date overflow");r.IntervalDays=1;r.IntervalKilometres=MaintenancePolicy.MaximumOdometer;Check(MaintenancePolicy.ValidatePlan(r,prior,100,Today).Count>0,"Decimal threshold overflow");
 foreach(decimal invalid in new[]{-1m,0.01m,MaintenancePolicy.MaximumOdometer+1})Check(!MaintenancePolicy.Odometer(invalid),"Odometer range/scale "+invalid);
 foreach(int? days in new int?[]{null,0,-1}){var request=new SaveMaintenancePlanRequest{BusId=1,ServiceName="Service",IntervalDays=days,NextDueDate=Today};Check(MaintenancePolicy.ValidatePlan(request,null,100,Today).Count>0,"Required positive interval "+days);}
 var s=new BusSafetyContext{Bus=new BusDetails{BaseOperationalState=BusOperationalState.OutOfService,GrossVehicleMassKg=8000,PassengerCapacity=20,LicenceExpiryDate=Today,RoadworthyExpiryDate=Today,InsuranceExpiryDate=Today},CategoryActive=true};
 Check(BusSafetyPolicy.OperationalBlocks(s,Today).Count==0,"Expiry equals today valid");Check(BusSafetyPolicy.OperationalBlocks(s,Today.AddDays(1)).Count==3,"Every expired compliance reason");s.HasCriticalDefect=true;Check(BusSafetyPolicy.OperationalBlocks(s,Today).Any(x=>x.Contains("Critical")),"Critical Return block");s.HasCriticalDefect=false;s.HasInProgressMaintenance=true;Check(BusSafetyPolicy.OperationalBlocks(s,Today).Count==1,"Work in progress Return block");s.HasInProgressMaintenance=false;s.Bus.BaseOperationalState=BusOperationalState.Retired;Check(BusSafetyPolicy.OperationalBlocks(s,Today).Count==1,"Retired Return block");
 var completion=new CompleteMaintenanceRequest{WorkPerformed="Verified work",Note="Verified completion",BusRowVersion=new byte[8],ServiceOdometer=100,CompletedCost=0};Check(MaintenancePolicy.ValidateCompletion(completion).Count==0,"Optional cost accepts zero");completion.CompletedCost=1.001m;Check(MaintenancePolicy.ValidateCompletion(completion).Count>0,"Cost scale");completion.CompletedCost=null;completion.ResolveLinkedDefect=true;Check(MaintenancePolicy.ValidateCompletion(completion).Count>0,"Explicit resolution requires note");
 var create=new CreateWorkOrderRequest{BusId=1,RepairProviderId=1,MaintenanceType=MaintenanceType.Corrective,TriggerType=MaintenanceTrigger.Manual,RequestedWork="Verified need",CreationToken=Guid.NewGuid()};Check(MaintenancePolicy.ValidateCreate(create,Today).Count==0,"Type independent from Manual trigger");create.TriggerType=MaintenanceTrigger.Breakdown;Check(MaintenancePolicy.ValidateCreate(create,Today).Count>0,"Breakdown needs existing evidence");create.BusDefectReportId=1;Check(MaintenancePolicy.ValidateCreate(create,Today).Count==0,"Breakdown defect evidence");create.TriggerType=MaintenanceTrigger.ComplianceExpiry;Check(MaintenancePolicy.ValidateCreate(create,Today).Count>0,"Compliance specific requirement");create.ComplianceRequirement=ComplianceRequirement.Insurance;Check(MaintenancePolicy.ValidateCreate(create,Today).Count==0,"Compliance trigger");
 Check(IdentifierCodePolicy.FormatMaintenancePlanCode(1)=="MP-000001","Plan code");Check(IdentifierCodePolicy.FormatMaintenanceWorkOrderCode(1000000)=="MWO-1000000","Order width expands");Check(IdentifierCodePolicy.FormatRepairProviderCode(1000)=="RP-1000","Provider width expands");
 var passenger=new PassengerJourneyCandidate{RouteIsActive=true,ScheduleIsActive=true,ServiceDate=Today.AddDays(1),ScheduledDepartureTime=new TimeSpan(9,0,0),ExpectedFinishLocal=Today.AddDays(1).AddHours(10),TripAssignmentId=1,TripStatus=TripStatus.Scheduled,DriverAccountIsActive=true,DriverRoleIsActive=true,DriverEmploymentStatus="Active",DriverAvailability=DriverAvailabilityStatus.Available,DriverDateOfBirth=Today.AddYears(-40),DriverLicenceCode=DriverLicenceCode.EC,DriverLicenceExpiryDate=Today.AddYears(1),DriverPrdpExpiryDate=Today.AddYears(1),BusOperationalState="Operational",BusGrossVehicleMassKg=8000,BusPassengerCapacity=20,BusLicenceExpiryDate=Today.AddYears(1),BusRoadworthyExpiryDate=Today.AddYears(1),BusInsuranceExpiryDate=Today.AddYears(1),BusCurrentOdometer=100};
 var sale=typeof(PassengerService).GetMethod("EvaluateSale",BindingFlags.NonPublic|BindingFlags.Static);Check(sale.Invoke(null,new object[]{passenger,Today})==null,"Otherwise sellable");
 p=Plan();p.NextDueDate=Today.AddDays(-1);passenger.MaintenancePlans.Add(p);Check(sale.Invoke(null,new object[]{passenger,Today})!=null,"Overdue blocks new sales");p.BlocksOperationWhenOverdue=false;Check(sale.Invoke(null,new object[]{passenger,Today})==null,"Nonblocking plan leaves sales available");
 var svc=(AssignmentService)FormatterServices.GetUninitializedObject(typeof(AssignmentService));typeof(AssignmentService).GetField("clock",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(svc,new Clock());var bus=new AssignmentBusCandidate{OperationalState="Operational",GrossVehicleMassKg=8000,PassengerCapacity=20,LicenceExpiryDate=Today.AddYears(1),RoadworthyExpiryDate=Today.AddYears(1),InsuranceExpiryDate=Today.AddYears(1),CurrentOdometer=100};var trip=new AssignmentTripCandidate{ExpectedFinishLocal=Today.AddDays(1),ExpectedCapacity=20};var eligible=typeof(AssignmentService).GetMethod("IsBusEligible",BindingFlags.NonPublic|BindingFlags.Instance);Check((bool)eligible.Invoke(svc,new object[]{trip,bus}),"Assignment baseline eligible");p.BlocksOperationWhenOverdue=true;bus.MaintenancePlans.Add(p);Check(!(bool)eligible.Invoke(svc,new object[]{trip,bus}),"Overdue blocks recommendation policy");
 Console.WriteLine(checks+" Slice 9 deterministic Business checks passed.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 sealed class Clock:IClock{public DateTime UtcNow{get{return Today.AddHours(-2);}}public DateTime Today{get{return Slice9BusinessVerification.Today;}}public DateTime OperationalNow{get{return Today;}}public DateTime ToOperationalTime(DateTime utc){return utc.AddHours(2);}}
}
