using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using ForteMove.Business.Fleet;
using ForteMove.Business.Identifiers;
using ForteMove.Business.Maintenance;
using ForteMove.Data.Internal;
using ForteMove.Models.Maintenance;
using ForteMove.Models.Operations;
using ForteMove.Models.Fleet;
namespace ForteMove.Data.Repositories
{
    public sealed partial class SqlMaintenanceRepository
    {
        public long CreateWorkOrder(CreateWorkOrderRequest r,long actor){return Write(actor,false,(c,tx,now,utc)=>CreateInTransaction(c,tx,r,actor,now,utc));}
        internal static long CreateInTransaction(SqlConnection c,SqlTransaction tx,CreateWorkOrderRequest r,long actor,DateTime now,DateTime utc)
        {
            using(var cmd=new SqlCommand("SELECT MaintenanceWorkOrderId FROM dbo.MaintenanceWorkOrders WITH(UPDLOCK,HOLDLOCK) WHERE CreationToken=@Token;",c,tx))
            {
                cmd.Parameters.Add("@Token",SqlDbType.UniqueIdentifier).Value=r.CreationToken;var v=cmd.ExecuteScalar();
                if(v!=null)
                {
                    var existing=ReadOrder(c,tx,(long)v);
                    Expect(existing.BusId==r.BusId && existing.RepairProviderId==r.RepairProviderId && existing.MaintenanceType==r.MaintenanceType && existing.TriggerType==r.TriggerType && existing.RequestedWork==r.RequestedWork && existing.MaintenancePlanId==r.MaintenancePlanId && existing.BusDefectReportId==r.BusDefectReportId && existing.TripCannotProceedReportId==r.TripCannotProceedReportId && existing.ComplianceRequirement==r.ComplianceRequirement && existing.TargetDate==r.TargetDate,"This confirmation belongs to a different work order. Refresh the creation form.");
                    return (long)v;
                }
            }
            SqlMaintenanceLifecycle.Errors(MaintenancePolicy.ValidateCreate(r,now.Date));
            var safety=SqlMaintenanceLifecycle.ReadSafety(c,tx,r.BusId);Expect(safety!=null,"The bus is unavailable.");
            var provider=SqlRepairProviderRepository.Read(c,tx,r.RepairProviderId);Expect(provider!=null&&provider.IsActive,"Select an active repair provider.");
            var plan=r.MaintenancePlanId.HasValue?ReadPlan(c,tx,r.MaintenancePlanId.Value):null;
            if(r.MaintenancePlanId.HasValue)Expect(plan!=null && plan.BusId==r.BusId && plan.IsActive,"Select an active plan belonging to this bus.");
            if(r.TriggerType==MaintenanceTrigger.ServiceDate)Expect(plan.NextDueDate.HasValue,"The selected plan has no date threshold.");
            if(r.TriggerType==MaintenanceTrigger.ServiceOdometer)Expect(plan.NextDueOdometer.HasValue,"The selected plan has no odometer threshold.");
            if(r.BusDefectReportId.HasValue)
            {
                string status=null;byte[] version=null;
                using(var cmd=new SqlCommand("SELECT DefectStatus,RowVersion FROM dbo.BusDefectReports WITH(UPDLOCK,HOLDLOCK) WHERE BusDefectReportId=@Id AND BusId=@Bus;",c,tx))
                {SqlMaintenanceLifecycle.Id(cmd,"@Id",r.BusDefectReportId.Value);SqlMaintenanceLifecycle.Id(cmd,"@Bus",r.BusId);using(var reader=cmd.ExecuteReader())if(reader.Read()){status=reader.GetString(0);version=(byte[])reader.GetValue(1);}}
                Expect(status=="Open" || status=="Reviewed","Select an unresolved defect belonging to this bus.");
                if(status=="Open")
                {
                    Expect(!string.IsNullOrWhiteSpace(r.DefectReviewNote),"Record the defect review outcome before creating work.");
                    Match(r.DefectRowVersion,version,"The defect");
                    SqlDefectLifecycle.Update(c,tx,new UpdateDefectRequest{BusDefectReportId=r.BusDefectReportId.Value,RowVersion=version,Note=r.DefectReviewNote},actor,utc,false);
                }
            }
            if(r.TripCannotProceedReportId.HasValue)
                using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.TripCannotProceedReports WHERE TripCannotProceedReportId=@Id AND BusId=@Bus;",c,tx))
                {SqlMaintenanceLifecycle.Id(cmd,"@Id",r.TripCannotProceedReportId.Value);SqlMaintenanceLifecycle.Id(cmd,"@Bus",r.BusId);Expect((int)cmd.ExecuteScalar()==1,"The linked exception does not belong to this bus.");}
            var code=IdentifierCodePolicy.FormatMaintenanceWorkOrderCode(SqlMaintenanceLifecycle.Next(c,tx,"MaintenanceWorkOrders","WorkOrderCode",5));long id;
            using(var cmd=new SqlCommand(@"INSERT dbo.MaintenanceWorkOrders(WorkOrderCode,CreationToken,BusId,RepairProviderId,ProviderCodeSnapshot,ProviderNameSnapshot,MaintenanceType,TriggerType,BusDefectReportId,MaintenancePlanId,TripCannotProceedReportId,ComplianceRequirement,RequestedWork,TargetDate,OrderStatus,OpenedUtc,PlanNextDueDateSnapshot,PlanNextDueOdometerSnapshot,PlanIntervalDaysSnapshot,PlanIntervalKilometresSnapshot,CreatedByUserAccountId,UpdatedByUserAccountId,UpdatedUtc)
VALUES(@Code,@Token,@Bus,@Provider,@ProviderCode,@ProviderName,@Type,@Trigger,@Defect,@Plan,@Exception,@Compliance,@Work,@Target,N'Open',@Utc,@NextDate,@NextOdo,@Days,@Km,@Actor,@Actor,@Utc);SELECT CONVERT(bigint,SCOPE_IDENTITY());",c,tx))
            {
                SqlMaintenanceLifecycle.Text(cmd,"@Code",30,code);cmd.Parameters.Add("@Token",SqlDbType.UniqueIdentifier).Value=r.CreationToken;
                SqlMaintenanceLifecycle.Id(cmd,"@Bus",r.BusId);SqlMaintenanceLifecycle.Id(cmd,"@Provider",r.RepairProviderId);SqlMaintenanceLifecycle.Text(cmd,"@ProviderCode",30,provider.ProviderCode);SqlMaintenanceLifecycle.Text(cmd,"@ProviderName",150,provider.ProviderName);
                SqlMaintenanceLifecycle.Text(cmd,"@Type",30,r.MaintenanceType.ToString());SqlMaintenanceLifecycle.Text(cmd,"@Trigger",30,r.TriggerType.ToString());
                SqlMaintenanceLifecycle.NullableId(cmd,"@Defect",r.BusDefectReportId);SqlMaintenanceLifecycle.NullableId(cmd,"@Plan",r.MaintenancePlanId);SqlMaintenanceLifecycle.NullableId(cmd,"@Exception",r.TripCannotProceedReportId);
                SqlMaintenanceLifecycle.Text(cmd,"@Compliance",30,r.ComplianceRequirement.HasValue?r.ComplianceRequirement.ToString():null);
                SqlMaintenanceLifecycle.Text(cmd,"@Work",1000,r.RequestedWork);SqlMaintenanceLifecycle.Date(cmd,"@Target",r.TargetDate);SqlMaintenanceLifecycle.Utc(cmd,"@Utc",utc);
                SqlMaintenanceLifecycle.Date(cmd,"@NextDate",plan==null?null:plan.NextDueDate);SqlMaintenanceLifecycle.Decimal(cmd,"@NextOdo",plan==null?null:plan.NextDueOdometer);cmd.Parameters.Add("@Days",SqlDbType.Int).Value=plan==null?DBNull.Value:(object)plan.IntervalDays??DBNull.Value;
                SqlMaintenanceLifecycle.Decimal(cmd,"@Km",plan==null?null:plan.IntervalKilometres);SqlMaintenanceLifecycle.Id(cmd,"@Actor",actor);
                id=(long)cmd.ExecuteScalar();
            }
            Audit(c,tx,actor,"MaintenanceWorkOrderCreated","MaintenanceWorkOrder",id,"Code="+code+";Bus="+r.BusId+";Type="+r.MaintenanceType+";Trigger="+r.TriggerType,utc);return id;
        }
        public void StartMaintenance(StartMaintenanceRequest r,long actor){Write(actor,true,(c,tx,now,utc)=>{StartInTransaction(c,tx,r,actor,now,utc);return true;});}
        internal static void StartInTransaction(SqlConnection c,SqlTransaction tx,StartMaintenanceRequest r,long actor,DateTime now,DateTime utc)
        {
            var review=ReadStartReview(c,tx,r.MaintenanceWorkOrderId);Expect(review!=null,"The work order is unavailable.");
            Match(r.RowVersion,review.Order.RowVersion,"The work order");
            Expect(review.Fingerprint==r.Fingerprint,"Affected assignments, tickets or fuel authorizations changed. Review the updated impact before starting.");
            Expect(review.Blockers.Count==0,string.Join(" ",review.Blockers));var w=review.Order;
            using(var cmd=new SqlCommand(@"UPDATE dbo.MaintenanceWorkOrders SET OrderStatus=N'InProgress',StartedUtc=@Utc,StartedByUserAccountId=@Actor,UpdatedUtc=@Utc,UpdatedByUserAccountId=@Actor WHERE MaintenanceWorkOrderId=@Id AND RowVersion=@Rv AND OrderStatus=N'Open';
IF @@ROWCOUNT<>1 THROW 51204,'The work order changed.',1;
UPDATE dbo.Buses SET BaseOperationalState=N'UnderMaintenance',UpdatedByUserAccountId=@Actor,UpdatedUtc=@Utc WHERE BusId=@Bus AND RowVersion=@BusRv;IF @@ROWCOUNT<>1 THROW 51205,'The bus changed.',1;",c,tx))
            {Core(cmd,r,actor,utc);SqlMaintenanceLifecycle.Id(cmd,"@Bus",w.BusId);SqlMaintenanceLifecycle.Version(cmd,"@BusRv",w.BusRowVersion);cmd.ExecuteNonQuery();}
            foreach(var t in review.Trips)
                using(var cmd=new SqlCommand(@"UPDATE dbo.Trips SET RequiresReview=1,OperationallyTouchedUtc=COALESCE(OperationallyTouchedUtc,@Utc),OperationallyTouchedByUserAccountId=COALESCE(OperationallyTouchedByUserAccountId,@Actor),UpdatedByUserAccountId=@Actor,UpdatedUtc=@Utc WHERE TripId=@Trip AND RowVersion=@Rv;",c,tx))
                {SqlMaintenanceLifecycle.Id(cmd,"@Trip",t.TripId);SqlMaintenanceLifecycle.Version(cmd,"@Rv",t.TripRowVersion);SqlMaintenanceLifecycle.Id(cmd,"@Actor",actor);SqlMaintenanceLifecycle.Utc(cmd,"@Utc",utc);Expect(cmd.ExecuteNonQuery()==1,"An affected Trip changed. Review the impact again.");}
            // Bus-scoped authorizations can include a context no longer present in the current assignment list.
            var fuelTrips=new System.Collections.Generic.List<long>();
            using(var cmd=new SqlCommand("SELECT TripId FROM dbo.FuelRequests WHERE BusId=@Bus AND RequestStatus=N'Pending' UNION SELECT TripId FROM dbo.FuelVouchers WHERE BusId=@Bus AND VoucherStatus=N'Active' ORDER BY TripId;",c,tx))
            {SqlMaintenanceLifecycle.Id(cmd,"@Bus",w.BusId);using(var reader=cmd.ExecuteReader())while(reader.Read())fuelTrips.Add(reader.GetInt64(0));}
            foreach(var trip in fuelTrips)SqlFuelLifecycle.InvalidateForBus(c,tx,trip,w.BusId,actor,"Maintenance started: "+w.WorkOrderCode,utc);
            SqlBusStatusHistory.Write(c,tx,w.BusId,w.BusStatus,"UnderMaintenance","External work started: "+w.WorkOrderCode,w.MaintenanceWorkOrderId,actor,utc);
            Audit(c,tx,actor,"MaintenanceWorkStarted","MaintenanceWorkOrder",w.MaintenanceWorkOrderId,"AffectedTrips="+review.Trips.Count+";FuelRequestsCancelled="+review.PendingFuelRequests+";FuelVouchersCancelled="+review.ActiveFuelVouchers,utc);
        }
        public void RecordProgress(MaintenanceActionRequest r,long actor){Write(actor,false,(c,tx,now,utc)=>{ProgressInTransaction(c,tx,r,actor,utc);return true;});}
        internal static void ProgressInTransaction(SqlConnection c,SqlTransaction tx,MaintenanceActionRequest r,long actor,DateTime utc)
        {
            var w=ReadOrder(c,tx,r.MaintenanceWorkOrderId);Expect(w!=null,"The work order is unavailable.");Match(r.RowVersion,w.RowVersion,"The work order");
            Expect(w.Status==MaintenanceOrderStatus.Open||w.Status==MaintenanceOrderStatus.InProgress,"Completed and Cancelled work orders cannot be changed.");
            Expect(!string.IsNullOrWhiteSpace(r.Note)&&r.Note.Trim().Length<=1000,"Enter a progress note within 1,000 characters.");
            using(var cmd=new SqlCommand(@"INSERT dbo.MaintenanceProgressEntries(MaintenanceWorkOrderId,Note,RecordedUtc,RecordedByUserAccountId) VALUES(@Id,@Note,@Utc,@Actor);
UPDATE dbo.MaintenanceWorkOrders SET UpdatedUtc=@Utc,UpdatedByUserAccountId=@Actor WHERE MaintenanceWorkOrderId=@Id;",c,tx))
            {Core(cmd,r,actor,utc);SqlMaintenanceLifecycle.Text(cmd,"@Note",1000,r.Note.Trim());cmd.ExecuteNonQuery();}
            Audit(c,tx,actor,"MaintenanceProgressRecorded","MaintenanceWorkOrder",r.MaintenanceWorkOrderId,"Note="+r.Note.Trim(),utc);
        }
        public void CompleteWorkOrder(CompleteMaintenanceRequest r,long actor){Write(actor,false,(c,tx,now,utc)=>{CompleteInTransaction(c,tx,r,actor,now,utc);return true;});}
        internal static void CompleteInTransaction(SqlConnection c,SqlTransaction tx,CompleteMaintenanceRequest r,long actor,DateTime now,DateTime utc)
        {
            SqlMaintenanceLifecycle.Errors(MaintenancePolicy.ValidateCompletion(r));Expect(!string.IsNullOrWhiteSpace(r.Note)&&r.Note.Trim().Length<=1000,"Enter the completion note.");
            var w=ReadOrder(c,tx,r.MaintenanceWorkOrderId);Expect(w!=null&&w.Status==MaintenanceOrderStatus.InProgress,"Only InProgress work can be completed.");
            Match(r.RowVersion,w.RowVersion,"The work order");
            var bus=SqlMaintenanceLifecycle.ReadSafety(c,tx,w.BusId);Match(r.BusRowVersion,bus.Bus.RowVersion,"The bus");
            Expect(r.ServiceOdometer.Value>=bus.Bus.OdometerKilometres,"The service odometer cannot be lower than the bus current reading.");
            using(var cmd=new SqlCommand("SELECT MAX(ServiceOdometer) FROM dbo.MaintenanceWorkOrders WITH(UPDLOCK,HOLDLOCK) WHERE BusId=@Bus AND OrderStatus=N'Completed';",c,tx))
            {SqlMaintenanceLifecycle.Id(cmd,"@Bus",w.BusId);var max=cmd.ExecuteScalar();Expect(max==DBNull.Value || r.ServiceOdometer.Value>=(decimal)max,"The service odometer cannot be lower than previous maintenance.");}
            var plan=w.MaintenancePlanId.HasValue?ReadPlan(c,tx,w.MaintenancePlanId.Value):null;
            if(plan!=null){Match(r.PlanRowVersion,plan.RowVersion,"The service plan");SqlMaintenanceLifecycle.Errors(MaintenancePolicy.ResetThresholds(plan,now.Date,r.ServiceOdometer));}
            if(r.ResolveLinkedDefect)
            {
                Expect(w.BusDefectReportId.HasValue,"This work order has no linked defect.");
                SqlDefectLifecycle.Update(c,tx,new UpdateDefectRequest{BusDefectReportId=w.BusDefectReportId.Value,RowVersion=r.DefectRowVersion,Note=r.ResolutionNote.Trim()},actor,utc,true);
            }
            using(var cmd=new SqlCommand(@"UPDATE dbo.MaintenanceWorkOrders SET OrderStatus=N'Completed',CompletedUtc=@Utc,CompletedByUserAccountId=@Actor,ServiceOdometer=@Odo,WorkPerformed=@Work,CompletionNote=@Note,ExternalReference=@Reference,CompletedCost=@Cost,UpdatedByUserAccountId=@Actor,UpdatedUtc=@Utc WHERE MaintenanceWorkOrderId=@Id AND RowVersion=@Rv;
IF @@ROWCOUNT<>1 THROW 51206,'The work order changed.',1;
UPDATE dbo.Buses SET OdometerKilometres=@Odo,UpdatedByUserAccountId=@Actor,UpdatedUtc=@Utc WHERE BusId=@Bus AND RowVersion=@BusRv AND OdometerKilometres<@Odo;",c,tx))
            {Core(cmd,r,actor,utc);SqlMaintenanceLifecycle.Decimal(cmd,"@Odo",r.ServiceOdometer);SqlMaintenanceLifecycle.Text(cmd,"@Work",1000,r.WorkPerformed.Trim());SqlMaintenanceLifecycle.Text(cmd,"@Note",1000,r.Note.Trim());SqlMaintenanceLifecycle.Text(cmd,"@Reference",100,r.ExternalReference);SqlMaintenanceLifecycle.Decimal(cmd,"@Cost",r.CompletedCost,2);SqlMaintenanceLifecycle.Id(cmd,"@Bus",w.BusId);SqlMaintenanceLifecycle.Version(cmd,"@BusRv",r.BusRowVersion);cmd.ExecuteNonQuery();}
            if(plan!=null)
                using(var cmd=new SqlCommand(@"UPDATE dbo.MaintenancePlans SET LastServiceDate=@Date,LastServiceOdometer=@Odo,NextDueDate=@NextDate,NextDueOdometer=@NextOdo,LastCompletedWorkOrderId=@Order,UpdatedByUserAccountId=@Actor,UpdatedUtc=@Utc WHERE MaintenancePlanId=@Plan AND RowVersion=@PlanRv;IF @@ROWCOUNT<>1 THROW 51207,'The service plan changed.',1;",c,tx))
                {SqlMaintenanceLifecycle.Date(cmd,"@Date",now.Date);SqlMaintenanceLifecycle.Decimal(cmd,"@Odo",r.ServiceOdometer);SqlMaintenanceLifecycle.Date(cmd,"@NextDate",plan.NextDueDate);SqlMaintenanceLifecycle.Decimal(cmd,"@NextOdo",plan.NextDueOdometer);SqlMaintenanceLifecycle.Id(cmd,"@Order",w.MaintenanceWorkOrderId);SqlMaintenanceLifecycle.Id(cmd,"@Plan",plan.MaintenancePlanId);SqlMaintenanceLifecycle.Version(cmd,"@PlanRv",r.PlanRowVersion);SqlMaintenanceLifecycle.Id(cmd,"@Actor",actor);SqlMaintenanceLifecycle.Utc(cmd,"@Utc",utc);cmd.ExecuteNonQuery();}
            Audit(c,tx,actor,"MaintenanceWorkCompleted","MaintenanceWorkOrder",w.MaintenanceWorkOrderId,"OdometerBefore="+bus.Bus.OdometerKilometres+";OdometerAfter="+r.ServiceOdometer+";LinkedDefectResolved="+r.ResolveLinkedDefect,utc);
        }
        public void CancelWorkOrder(MaintenanceActionRequest r,long actor){Write(actor,false,(c,tx,now,utc)=>{CancelInTransaction(c,tx,r,actor,utc);return true;});}
        internal static void CancelInTransaction(SqlConnection c,SqlTransaction tx,MaintenanceActionRequest r,long actor,DateTime utc)
        {
            var w=ReadOrder(c,tx,r.MaintenanceWorkOrderId);Expect(w!=null,"The work order is unavailable.");Match(r.RowVersion,w.RowVersion,"The work order");
            Expect(w.Status==MaintenanceOrderStatus.Open||w.Status==MaintenanceOrderStatus.InProgress,"Completed and Cancelled work orders cannot be cancelled.");
            Expect(!string.IsNullOrWhiteSpace(r.Note)&&r.Note.Trim().Length<=1000,"A cancellation reason is required.");
            using(var cmd=new SqlCommand(@"UPDATE dbo.MaintenanceWorkOrders SET OrderStatus=N'Cancelled',CancelledUtc=@Utc,CancelledByUserAccountId=@Actor,CancellationReason=@Note,UpdatedByUserAccountId=@Actor,UpdatedUtc=@Utc WHERE MaintenanceWorkOrderId=@Id AND RowVersion=@Rv;IF @@ROWCOUNT<>1 THROW 51208,'The work order changed.',1;",c,tx))
            {Core(cmd,r,actor,utc);SqlMaintenanceLifecycle.Text(cmd,"@Note",1000,r.Note.Trim());cmd.ExecuteNonQuery();}
            Audit(c,tx,actor,"MaintenanceWorkOrderCancelled","MaintenanceWorkOrder",w.MaintenanceWorkOrderId,"Reason="+r.Note.Trim(),utc);
        }
        public void ReturnToService(ReturnToServiceRequest r,long actor){Write(actor,false,(c,tx,now,utc)=>{ReturnInTransaction(c,tx,r,actor,now,utc);return true;});}
        internal static void ReturnInTransaction(SqlConnection c,SqlTransaction tx,ReturnToServiceRequest r,long actor,DateTime now,DateTime utc)
        {
            Expect(!string.IsNullOrWhiteSpace(r.Note)&&r.Note.Trim().Length<=1000,"Record the return-to-service decision.");
            var x=SqlMaintenanceLifecycle.ReadSafety(c,tx,r.BusId);Expect(x!=null,"The bus is unavailable.");Match(r.BusRowVersion,x.Bus.RowVersion,"The bus");
            Expect(x.Bus.BaseOperationalState==BusOperationalState.UnderMaintenance || x.Bus.BaseOperationalState==BusOperationalState.OutOfService,"Only an Out of Service or Under Maintenance bus can return to service.");
            var blocks=BusSafetyPolicy.OperationalBlocks(x,now.Date);Expect(blocks.Count==0,string.Join(" ",blocks));
            using(var cmd=new SqlCommand("UPDATE dbo.Buses SET BaseOperationalState=N'Operational',UpdatedByUserAccountId=@Actor,UpdatedUtc=@Utc WHERE BusId=@Bus AND RowVersion=@Rv;IF @@ROWCOUNT<>1 THROW 51209,'The bus changed.',1;",c,tx))
            {SqlMaintenanceLifecycle.Id(cmd,"@Bus",r.BusId);SqlMaintenanceLifecycle.Version(cmd,"@Rv",r.BusRowVersion);SqlMaintenanceLifecycle.Id(cmd,"@Actor",actor);SqlMaintenanceLifecycle.Utc(cmd,"@Utc",utc);cmd.ExecuteNonQuery();}
            SqlBusStatusHistory.Write(c,tx,r.BusId,x.Bus.BaseOperationalState.ToString(),"Operational",r.Note.Trim(),null,actor,utc);
            Audit(c,tx,actor,"BusReturnedToService","Bus",r.BusId,"Note="+r.Note.Trim(),utc);
        }
        private static void Core(SqlCommand c,MaintenanceActionRequest r,long actor,DateTime utc)
        {SqlMaintenanceLifecycle.Id(c,"@Id",r.MaintenanceWorkOrderId);SqlMaintenanceLifecycle.Version(c,"@Rv",r.RowVersion);SqlMaintenanceLifecycle.Id(c,"@Actor",actor);SqlMaintenanceLifecycle.Utc(c,"@Utc",utc);}
    }
}
