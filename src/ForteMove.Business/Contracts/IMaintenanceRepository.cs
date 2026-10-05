using System.Collections.Generic;
using ForteMove.Models.Fleet;
using ForteMove.Models.Maintenance;
namespace ForteMove.Business.Contracts
{
    public interface IMaintenanceRepository
    {
        IList<MaintenancePlan> GetPlans(MaintenanceQuery query);
        MaintenancePlan GetPlan(long id);
        IList<BusDetails> GetBuses();
        BusSafetyContext GetBusSafety(long id);
        long SavePlan(SaveMaintenancePlanRequest request,long actor);
        IList<MaintenanceWorkOrder> GetWorkOrders(MaintenanceQuery query);
        MaintenanceWorkOrder GetWorkOrder(long id);
        long CreateWorkOrder(CreateWorkOrderRequest request,long actor);
        MaintenanceStartReview PreviewStart(long id);
        void StartMaintenance(StartMaintenanceRequest request,long actor);
        void RecordProgress(MaintenanceActionRequest request,long actor);
        void CompleteWorkOrder(CompleteMaintenanceRequest request,long actor);
        void CancelWorkOrder(MaintenanceActionRequest request,long actor);
        void ReturnToService(ReturnToServiceRequest request,long actor);
        IList<MaintenanceAttention> GetOtherAttention();
        IList<MaintenanceHistoryEntry> GetHistory(long busId);
    }
}
