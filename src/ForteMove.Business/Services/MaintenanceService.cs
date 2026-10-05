using System;
using System.Collections.Generic;
using System.Linq;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Fleet;
using ForteMove.Business.Maintenance;
using ForteMove.Business.Time;
using ForteMove.Models.Common;
using ForteMove.Models.Fleet;
using ForteMove.Models.Maintenance;
namespace ForteMove.Business.Services
{
    public sealed class MaintenanceService
    {
        private readonly IMaintenanceRepository repository;
        private readonly IClock clock;
        public MaintenanceService(IMaintenanceRepository repository):this(repository,new SystemClock()){}
        public MaintenanceService(IMaintenanceRepository repository,IClock clock){if(repository==null)throw new ArgumentNullException("repository");if(clock==null)throw new ArgumentNullException("clock");this.repository=repository;this.clock=clock;}
        public IList<BusDetails> GetBuses(){return repository.GetBuses();}
        public IList<MaintenancePlan> GetPlans(MaintenanceQuery query)
        {
            var rows=repository.GetPlans(query??new MaintenanceQuery());
            foreach(var p in rows)MaintenancePolicy.Describe(p,clock.Today);
            return rows.Where(p=>query==null || !query.DueState.HasValue || p.DueState==query.DueState.Value).ToList();
        }
        public MaintenancePlan GetPlan(long id){var p=id>0?repository.GetPlan(id):null;if(p!=null)MaintenancePolicy.Describe(p,clock.Today);return p;}
        public IList<MaintenanceWorkOrder> GetWorkOrders(MaintenanceQuery q){return repository.GetWorkOrders(q??new MaintenanceQuery());}
        public MaintenanceWorkOrder GetWorkOrder(long id){return id>0?repository.GetWorkOrder(id):null;}
        public BusSafetyContext GetReturnToServiceReview(long busId){return busId>0?repository.GetBusSafety(busId):null;}
        public IList<string> GetReturnBlockers(BusSafetyContext value){return BusSafetyPolicy.OperationalBlocks(value,clock.Today);}
        public IList<MaintenanceHistoryEntry> GetBusServiceHistory(long busId){return repository.GetHistory(busId);}
        public DateTime ToOperationalTime(DateTime utc){return clock.ToOperationalTime(utc);}
        public MaintenanceOverview GetOverview()
        {
            var p=GetPlans(null);var w=GetWorkOrders(null);
            return new MaintenanceOverview{DuePlans=p.Count(x=>x.IsActive&&x.DueState==MaintenanceDueState.Due),OverduePlans=p.Count(x=>x.IsActive&&x.DueState==MaintenanceDueState.Overdue),BlockingBuses=p.Where(x=>x.BlocksOperation).Select(x=>x.BusId).Distinct().Count(),OpenOrders=w.Count(x=>x.Status==MaintenanceOrderStatus.Open),InProgressOrders=w.Count(x=>x.Status==MaintenanceOrderStatus.InProgress)};
        }
        public IList<MaintenanceAttention> GetAttention(MaintenanceQuery q)
        {
            var a=repository.GetOtherAttention();
            foreach(var p in GetPlans(q).Where(x=>x.IsActive&&x.DueState!=MaintenanceDueState.NotDue))a.Add(new MaintenanceAttention{BusId=p.BusId,FleetNumber=p.FleetNumber,Kind=p.DueState.ToString(),Reference=p.PlanCode,Reason=p.ServiceName+": "+p.DueReason,Blocking=p.BlocksOperation,PlanId=p.MaintenancePlanId});
            return a.Where(x=>q==null || !q.BusId.HasValue || q.BusId==x.BusId).Where(x=>q==null || string.IsNullOrWhiteSpace(q.Search) || (x.FleetNumber+" "+x.Reason+" "+x.Reference).IndexOf(q.Search.Trim(),StringComparison.OrdinalIgnoreCase)>=0).OrderByDescending(x=>x.Blocking).ThenBy(x=>x.FleetNumber).ToList();
        }
        public ServiceResult<long> SavePlan(SaveMaintenancePlanRequest r,long actor)
        {
            if(r==null)return ServiceResult<long>.Failure("","Enter a service plan.");
            r.ServiceName=(r.ServiceName??"").Trim();
            var bus=repository.GetBusSafety(r.BusId);
            if(bus==null)return ServiceResult<long>.Failure("BusId","Select an existing bus.");
            var prior=r.MaintenancePlanId>0?repository.GetPlan(r.MaintenancePlanId):null;
            var e=MaintenancePolicy.ValidatePlan(r,prior,bus.Bus.OdometerKilometres,clock.Today);
            if(r.MaintenancePlanId>0 && (prior==null || !MaintenancePolicy.Version(r.RowVersion)))e.Add(new ValidationError("","Refresh the plan before saving."));
            return Save(e,()=>repository.SavePlan(r,actor));
        }
        public ServiceResult<long> CreateWorkOrder(CreateWorkOrderRequest r,long actor)
        {
            if(r==null)return ServiceResult<long>.Failure("","Enter a work order.");
            r.RequestedWork=(r.RequestedWork??"").Trim();r.DefectReviewNote=Trim(r.DefectReviewNote);
            return Save(MaintenancePolicy.ValidateCreate(r,clock.Today),()=>repository.CreateWorkOrder(r,actor));
        }
        public MaintenanceStartReview PreviewStart(long id){return repository.PreviewStart(id);}
        public ServiceResult<bool> StartMaintenance(StartMaintenanceRequest r,long actor)
        {
            var e=ActionErrors(r,false);
            if(r==null || string.IsNullOrEmpty(r.Fingerprint))e.Add(new ValidationError("","Review the affected assignments and fuel authorizations first."));
            return Act(e,()=>repository.StartMaintenance(r,actor));
        }
        public ServiceResult<bool> RecordProgress(MaintenanceActionRequest r,long actor){return Act(ActionErrors(r,true),()=>repository.RecordProgress(r,actor));}
        public ServiceResult<bool> CancelWorkOrder(MaintenanceActionRequest r,long actor){return Act(ActionErrors(r,true),()=>repository.CancelWorkOrder(r,actor));}
        public ServiceResult<bool> CompleteWorkOrder(CompleteMaintenanceRequest r,long actor)
        {
            var e=ActionErrors(r,true);
            if(r!=null)
            {
                foreach(var error in MaintenancePolicy.ValidateCompletion(r))e.Add(error);
                if(r.ResolveLinkedDefect && !MaintenancePolicy.Version(r.DefectRowVersion))e.Add(new ValidationError("","Refresh the linked defect before resolving it."));
            }
            return Act(e,()=>repository.CompleteWorkOrder(r,actor));
        }
        public ServiceResult<bool> ReturnToService(ReturnToServiceRequest r,long actor)
        {
            var e=new List<ValidationError>();MaintenancePolicy.Text(e,"Note",r==null?null:r.Note,1000,true);
            if(r==null || r.BusId<=0 || !MaintenancePolicy.Version(r.BusRowVersion))e.Add(new ValidationError("","Refresh the bus before confirming."));
            return Act(e,()=>repository.ReturnToService(r,actor));
        }
        private static IList<ValidationError> ActionErrors(MaintenanceActionRequest r,bool note)
        {
            var e=new List<ValidationError>();if(r==null || r.MaintenanceWorkOrderId<=0 || !MaintenancePolicy.Version(r.RowVersion))e.Add(new ValidationError("","Refresh the work order before continuing."));
            MaintenancePolicy.Text(e,"Note",r==null?null:r.Note,1000,note);if(r!=null)r.Note=Trim(r.Note);return e;
        }
        private static string Trim(string s){return string.IsNullOrWhiteSpace(s)?null:s.Trim();}
        private static ServiceResult<long> Save(IList<ValidationError> e,Func<long> action){if(e.Count>0)return ServiceResult<long>.Failure(e);try{return ServiceResult<long>.Success(action());}catch(MaintenancePersistenceException x){return ServiceResult<long>.Failure("",x.Message);}}
        private static ServiceResult<bool> Act(IList<ValidationError> e,Action action){if(e.Count>0)return ServiceResult<bool>.Failure(e);try{action();return ServiceResult<bool>.Success(true);}catch(MaintenancePersistenceException x){return ServiceResult<bool>.Failure("",x.Message);}}
    }
}
