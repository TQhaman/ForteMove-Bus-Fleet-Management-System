using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ForteMove.Models.Common;
using ForteMove.Models.Maintenance;
namespace ForteMove.Business.Maintenance
{
    public static class MaintenancePolicy
    {
        public const decimal MaximumOdometer = 99999999999.9m;
        public const decimal MaximumCost = 9999999999.99m;
        public static MaintenanceDueState Evaluate(MaintenancePlan p,DateTime today,decimal odometer)
        {
            if (!p.IsActive) return MaintenanceDueState.NotDue;
            if ((p.NextDueDate.HasValue && today.Date>p.NextDueDate.Value.Date) || (p.NextDueOdometer.HasValue && odometer>p.NextDueOdometer.Value)) return MaintenanceDueState.Overdue;
            if ((p.NextDueDate.HasValue && today.Date==p.NextDueDate.Value.Date) || (p.NextDueOdometer.HasValue && odometer==p.NextDueOdometer.Value)) return MaintenanceDueState.Due;
            return MaintenanceDueState.NotDue;
        }
        public static void Describe(MaintenancePlan p,DateTime today)
        {
            p.DueState=Evaluate(p,today,p.CurrentOdometer);
            p.BlocksOperation=p.IsActive && p.BlocksOperationWhenOverdue && p.DueState==MaintenanceDueState.Overdue;
            var reasons=new List<string>();
            if(p.IsActive && p.NextDueDate.HasValue && today.Date>=p.NextDueDate.Value.Date) reasons.Add("Service date "+p.NextDueDate.Value.ToString("dd MMM yyyy",CultureInfo.InvariantCulture));
            if(p.IsActive && p.NextDueOdometer.HasValue && p.CurrentOdometer>=p.NextDueOdometer.Value) reasons.Add("Service odometer "+p.NextDueOdometer.Value.ToString("0.0",CultureInfo.InvariantCulture)+" km");
            p.DueReason=string.Join("; ",reasons);
        }
        public static IList<ValidationError> ValidatePlan(SaveMaintenancePlanRequest p,MaintenancePlan prior,decimal busOdometer,DateTime today)
        {
            var e=new List<ValidationError>();
            Text(e,"ServiceName",p.ServiceName,150,true);
            if(p.BusId<=0) e.Add(new ValidationError("BusId","Select a bus."));
            if(!p.IntervalDays.HasValue && !p.IntervalKilometres.HasValue) e.Add(new ValidationError("Intervals","Enter a day or kilometre interval."));
            if(p.IntervalDays.HasValue && p.IntervalDays.Value<=0) e.Add(new ValidationError("IntervalDays","The day interval must be positive."));
            if(p.IntervalKilometres.HasValue && (p.IntervalKilometres.Value<=0 || !Odometer(p.IntervalKilometres.Value))) e.Add(new ValidationError("IntervalKilometres","Enter a positive kilometre interval with at most one decimal place."));
            if(prior!=null && prior.BusId!=p.BusId) e.Add(new ValidationError("BusId","A plan cannot be moved to another bus."));
            if(prior!=null && prior.LastCompletedWorkOrderId.HasValue)
            { p.LastServiceDate=prior.LastServiceDate;p.LastServiceOdometer=prior.LastServiceOdometer;p.UseVerifiedBaseline=true; }
            if(p.UseVerifiedBaseline)
            {
                if(p.IntervalDays.HasValue && (!p.LastServiceDate.HasValue || p.LastServiceDate.Value.Date>today.Date)) e.Add(new ValidationError("LastServiceDate","Enter a verified service date no later than today."));
                if(p.IntervalKilometres.HasValue && (!p.LastServiceOdometer.HasValue || !Odometer(p.LastServiceOdometer.Value) || p.LastServiceOdometer.Value>busOdometer)) e.Add(new ValidationError("LastServiceOdometer","Enter a verified service odometer no greater than the bus reading."));
                if(e.Count==0) e.AddRange(ResetThresholds(p,p.LastServiceDate,p.LastServiceOdometer));
            }
            else
            {
                p.LastServiceDate=null;p.LastServiceOdometer=null;
                if(p.IntervalDays.HasValue && !p.NextDueDate.HasValue) e.Add(new ValidationError("NextDueDate","Enter the approved first due date."));
                if(p.IntervalKilometres.HasValue && (!p.NextDueOdometer.HasValue || !Odometer(p.NextDueOdometer.Value))) e.Add(new ValidationError("NextDueOdometer","Enter the approved first due odometer."));
            }
            if(!p.IntervalDays.HasValue)p.NextDueDate=null;
            if(!p.IntervalKilometres.HasValue)p.NextDueOdometer=null;
            return e;
        }
        public static IList<ValidationError> ResetThresholds(MaintenancePlan p,DateTime? date,decimal? odometer)
        {
            var e=new List<ValidationError>();
            p.NextDueDate=null;p.NextDueOdometer=null;
            if(p.IntervalDays.HasValue && date.HasValue)try{p.NextDueDate=date.Value.Date.AddDays(p.IntervalDays.Value);}catch(ArgumentOutOfRangeException){e.Add(new ValidationError("IntervalDays","The next service date exceeds the supported date range."));}
            if(p.IntervalKilometres.HasValue && odometer.HasValue)
            {if(p.IntervalKilometres.Value>MaximumOdometer-odometer.Value)e.Add(new ValidationError("IntervalKilometres","The next service odometer exceeds the supported range."));else p.NextDueOdometer=odometer.Value+p.IntervalKilometres.Value;}
            return e;
        }
        public static IList<ValidationError> ValidateProvider(SaveRepairProviderRequest r)
        {
            var e=new List<ValidationError>();Text(e,"ProviderName",r.ProviderName,150,true);Text(e,"AreaDescription",r.AreaDescription,250,true);Text(e,"Phone",r.Phone,30,false);Text(e,"Email",r.Email,254,false);
            if(!string.IsNullOrWhiteSpace(r.Email))try{if(new System.Net.Mail.MailAddress(r.Email).Address!=r.Email)e.Add(new ValidationError("Email","Enter a valid email address."));}catch(FormatException){e.Add(new ValidationError("Email","Enter a valid email address."));}
            if(r.RepairProviderId>0 && !Version(r.RowVersion))e.Add(new ValidationError("","Refresh the provider before saving."));
            return e;
        }
        public static IList<ValidationError> ValidateCreate(CreateWorkOrderRequest r,DateTime today)
        {
            var e=new List<ValidationError>();Text(e,"RequestedWork",r.RequestedWork,1000,true);Text(e,"DefectReviewNote",r.DefectReviewNote,1000,false);
            if(r.BusId<=0 || r.RepairProviderId<=0)e.Add(new ValidationError("","Select a bus and active repair provider."));
            if(!r.MaintenanceType.HasValue || !Enum.IsDefined(typeof(MaintenanceType),r.MaintenanceType.Value))e.Add(new ValidationError("MaintenanceType","Select a maintenance type."));
            if(!r.TriggerType.HasValue || !Enum.IsDefined(typeof(MaintenanceTrigger),r.TriggerType.Value))e.Add(new ValidationError("TriggerType","Select a maintenance trigger."));
            if((r.TriggerType==MaintenanceTrigger.ServiceDate || r.TriggerType==MaintenanceTrigger.ServiceOdometer) && !r.MaintenancePlanId.HasValue)e.Add(new ValidationError("MaintenancePlanId","Select the service plan."));
            if(r.TriggerType==MaintenanceTrigger.Defect && !r.BusDefectReportId.HasValue)e.Add(new ValidationError("BusDefectReportId","Select the defect."));
            if(r.TriggerType==MaintenanceTrigger.Breakdown && !r.BusDefectReportId.HasValue && !r.TripCannotProceedReportId.HasValue)e.Add(new ValidationError("TriggerType","Link the breakdown to its existing defect or Cannot Proceed report, or use Manual with explanatory work details."));
            if(r.TriggerType==MaintenanceTrigger.ComplianceExpiry && (!r.ComplianceRequirement.HasValue || !Enum.IsDefined(typeof(ComplianceRequirement),r.ComplianceRequirement.Value)))e.Add(new ValidationError("ComplianceRequirement","Select the compliance requirement."));
            if(r.TriggerType!=MaintenanceTrigger.ComplianceExpiry)r.ComplianceRequirement=null;
            if(r.TargetDate.HasValue && r.TargetDate.Value.Date<today.Date)e.Add(new ValidationError("TargetDate","The target date cannot be before today."));
            if(r.CreationToken==Guid.Empty)e.Add(new ValidationError("","Refresh the creation form."));
            return e;
        }
        public static IList<ValidationError> ValidateCompletion(CompleteMaintenanceRequest r)
        {
            var e=new List<ValidationError>();Text(e,"WorkPerformed",r.WorkPerformed,1000,true);Text(e,"ExternalReference",r.ExternalReference,100,false);
            if(!r.ServiceOdometer.HasValue || !Odometer(r.ServiceOdometer.Value))e.Add(new ValidationError("ServiceOdometer","Enter a nonnegative supported odometer with at most one decimal place."));
            if(r.CompletedCost.HasValue && (r.CompletedCost.Value<0 || r.CompletedCost.Value>MaximumCost || decimal.Round(r.CompletedCost.Value,2)!=r.CompletedCost.Value))e.Add(new ValidationError("CompletedCost","Enter a nonnegative Rand cost with at most two decimal places."));
            if(!Version(r.BusRowVersion))e.Add(new ValidationError("","Refresh the bus before completing work."));
            if(r.ResolveLinkedDefect)Text(e,"ResolutionNote",r.ResolutionNote,1000,true);
            return e;
        }
        public static bool Odometer(decimal value){return value>=0 && value<=MaximumOdometer && decimal.Round(value,1)==value;}
        public static void Text(IList<ValidationError> e,string field,string value,int max,bool required)
        {string label=System.Text.RegularExpressions.Regex.Replace(field,"([a-z])([A-Z])","$1 $2").ToLowerInvariant();if(required&&string.IsNullOrWhiteSpace(value))e.Add(new ValidationError(field,"Enter "+label+"."));else if(value!=null&&value.Trim().Length>max)e.Add(new ValidationError(field,"Keep "+label+" within "+max+" characters."));}
        public static bool Version(byte[] value){return value!=null && value.Length==8;}
        public static string Fingerprint(MaintenanceStartReview review)
        {
            var o=review.Order;var s=new StringBuilder();
            s.Append(o.MaintenanceWorkOrderId).Append('|').Append(Convert.ToBase64String(o.RowVersion)).Append('|').Append(Convert.ToBase64String(o.BusRowVersion));
            s.Append('|').Append(review.PendingFuelRequests).Append('|').Append(review.ActiveFuelVouchers).Append('|').Append(review.FuelAuthorizationSignature);
            foreach(var t in review.Trips.OrderBy(x=>x.TripId))s.Append('|').Append(t.TripId).Append(':').Append(Convert.ToBase64String(t.TripRowVersion)).Append(':').Append(Convert.ToBase64String(t.AssignmentRowVersion)).Append(':').Append(t.PurchasedTickets);
            using(var h=SHA256.Create())return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(s.ToString())));
        }
    }
}
