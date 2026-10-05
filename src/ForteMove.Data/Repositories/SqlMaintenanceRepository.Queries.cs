using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using ForteMove.Business.Maintenance;
using ForteMove.Data.Internal;
using ForteMove.Models.Fleet;
using ForteMove.Models.Maintenance;
namespace ForteMove.Data.Repositories
{
    public sealed partial class SqlMaintenanceRepository
    {
        public IList<BusDetails> GetBuses()
        {
            var result=new List<BusDetails>();
            using(var c=new SqlConnection(connectionString))using(var cmd=new SqlCommand("SELECT BusId,FleetNumber FROM dbo.Buses ORDER BY FleetNumber;",c))
            {c.Open();using(var r=cmd.ExecuteReader())while(r.Read())result.Add(new BusDetails{BusId=r.GetInt64(0),FleetNumber=r.GetString(1)});}
            return result;
        }
        public BusSafetyContext GetBusSafety(long id){using(var c=new SqlConnection(connectionString)){c.Open();return SqlMaintenanceLifecycle.ReadSafety(c,null,id);}}
        public IList<MaintenancePlan> GetPlans(MaintenanceQuery q)
        {
            var result=new List<MaintenancePlan>();q=q??new MaintenanceQuery();
            using(var c=new SqlConnection(connectionString))using(var cmd=new SqlCommand(@"SELECT p.*,b.FleetNumber,b.OdometerKilometres AS CurrentOdometer FROM dbo.MaintenancePlans p JOIN dbo.Buses b ON b.BusId=p.BusId
WHERE (@Bus IS NULL OR p.BusId=@Bus) AND (@Search IS NULL OR CHARINDEX(@Search,p.PlanCode+N' '+p.ServiceName+N' '+b.FleetNumber)>0) ORDER BY b.FleetNumber,p.PlanCode;",c))
            {SqlMaintenanceLifecycle.NullableId(cmd,"@Bus",q.BusId);Search(cmd,q.Search);c.Open();using(var r=cmd.ExecuteReader())while(r.Read())result.Add(SqlMaintenanceLifecycle.ReadPlan(r));}
            return result;
        }
        public MaintenancePlan GetPlan(long id){using(var c=new SqlConnection(connectionString)){c.Open();return ReadPlan(c,null,id);}}
        private static MaintenancePlan ReadPlan(SqlConnection c,SqlTransaction tx,long id)
        {
            using(var cmd=new SqlCommand("SELECT p.*,b.FleetNumber,b.OdometerKilometres AS CurrentOdometer FROM dbo.MaintenancePlans p"+(tx==null?"":" WITH(UPDLOCK,HOLDLOCK)")+" JOIN dbo.Buses b ON b.BusId=p.BusId WHERE p.MaintenancePlanId=@Id;",c,tx))
            {SqlMaintenanceLifecycle.Id(cmd,"@Id",id);using(var r=cmd.ExecuteReader())return r.Read()?SqlMaintenanceLifecycle.ReadPlan(r):null;}
        }
        private const string OrderSelect=@"SELECT w.*,b.FleetNumber,b.BaseOperationalState AS BusStatus,b.OdometerKilometres AS CurrentOdometer,b.RowVersion AS BusRv,
p.PlanCode,p.RowVersion AS PlanRv,d.DefectCode,d.DefectStatus,d.RowVersion AS DefectRv FROM dbo.MaintenanceWorkOrders w";
        private const string OrderJoins=@" JOIN dbo.Buses b ON b.BusId=w.BusId LEFT JOIN dbo.MaintenancePlans p ON p.MaintenancePlanId=w.MaintenancePlanId LEFT JOIN dbo.BusDefectReports d ON d.BusDefectReportId=w.BusDefectReportId ";
        public IList<MaintenanceWorkOrder> GetWorkOrders(MaintenanceQuery q)
        {
            var rows=new List<MaintenanceWorkOrder>();q=q??new MaintenanceQuery();
            using(var c=new SqlConnection(connectionString))using(var cmd=new SqlCommand(OrderSelect+OrderJoins+@"WHERE (@Bus IS NULL OR w.BusId=@Bus) AND (@Status IS NULL OR w.OrderStatus=@Status)
AND (@Search IS NULL OR CHARINDEX(@Search,w.WorkOrderCode+N' '+b.FleetNumber+N' '+w.ProviderNameSnapshot+N' '+w.RequestedWork)>0) ORDER BY w.OpenedUtc DESC,w.MaintenanceWorkOrderId DESC;",c))
            {SqlMaintenanceLifecycle.NullableId(cmd,"@Bus",q.BusId);SqlMaintenanceLifecycle.Text(cmd,"@Status",20,q.Status.HasValue?q.Status.ToString():null);Search(cmd,q.Search);c.Open();using(var r=cmd.ExecuteReader())while(r.Read())rows.Add(MapOrder(r));}
            return rows;
        }
        public MaintenanceWorkOrder GetWorkOrder(long id){using(var c=new SqlConnection(connectionString)){c.Open();return ReadOrder(c,null,id);}}
        private static MaintenanceWorkOrder ReadOrder(SqlConnection c,SqlTransaction tx,long id)
        {
            using(var cmd=new SqlCommand(OrderSelect+(tx==null?"":" WITH(UPDLOCK,HOLDLOCK)")+OrderJoins+"WHERE w.MaintenanceWorkOrderId=@Id;",c,tx))
            {SqlMaintenanceLifecycle.Id(cmd,"@Id",id);using(var r=cmd.ExecuteReader())return r.Read()?MapOrder(r):null;}
        }
        private static MaintenanceWorkOrder MapOrder(SqlDataReader r)
        {
            return new MaintenanceWorkOrder{
                MaintenanceWorkOrderId=SqlMaintenanceLifecycle.Long(r,"MaintenanceWorkOrderId"),WorkOrderCode=SqlMaintenanceLifecycle.String(r,"WorkOrderCode"),CreationToken=(Guid)r["CreationToken"],
                BusId=SqlMaintenanceLifecycle.Long(r,"BusId"),RepairProviderId=SqlMaintenanceLifecycle.Long(r,"RepairProviderId"),FleetNumber=SqlMaintenanceLifecycle.String(r,"FleetNumber"),
                ProviderCodeSnapshot=SqlMaintenanceLifecycle.String(r,"ProviderCodeSnapshot"),ProviderNameSnapshot=SqlMaintenanceLifecycle.String(r,"ProviderNameSnapshot"),
                MaintenanceType=(MaintenanceType)Enum.Parse(typeof(MaintenanceType),(string)r["MaintenanceType"]),TriggerType=(MaintenanceTrigger)Enum.Parse(typeof(MaintenanceTrigger),(string)r["TriggerType"]),
                BusDefectReportId=SqlMaintenanceLifecycle.NullableLong(r,"BusDefectReportId"),MaintenancePlanId=SqlMaintenanceLifecycle.NullableLong(r,"MaintenancePlanId"),TripCannotProceedReportId=SqlMaintenanceLifecycle.NullableLong(r,"TripCannotProceedReportId"),
                ComplianceRequirement=r["ComplianceRequirement"]==DBNull.Value?(ComplianceRequirement?)null:(ComplianceRequirement)Enum.Parse(typeof(ComplianceRequirement),(string)r["ComplianceRequirement"]),
                RequestedWork=SqlMaintenanceLifecycle.String(r,"RequestedWork"),TargetDate=SqlMaintenanceLifecycle.NullableDate(r,"TargetDate"),Status=(MaintenanceOrderStatus)Enum.Parse(typeof(MaintenanceOrderStatus),(string)r["OrderStatus"]),
                OpenedUtc=(DateTime)r["OpenedUtc"],StartedUtc=SqlMaintenanceLifecycle.NullableDate(r,"StartedUtc"),CompletedUtc=SqlMaintenanceLifecycle.NullableDate(r,"CompletedUtc"),CancelledUtc=SqlMaintenanceLifecycle.NullableDate(r,"CancelledUtc"),
                CancellationReason=SqlMaintenanceLifecycle.String(r,"CancellationReason"),ServiceOdometer=SqlMaintenanceLifecycle.NullableDecimal(r,"ServiceOdometer"),WorkPerformed=SqlMaintenanceLifecycle.String(r,"WorkPerformed"),CompletionNote=SqlMaintenanceLifecycle.String(r,"CompletionNote"),ExternalReference=SqlMaintenanceLifecycle.String(r,"ExternalReference"),CompletedCost=SqlMaintenanceLifecycle.NullableDecimal(r,"CompletedCost"),
                RowVersion=(byte[])r["RowVersion"],BusRowVersion=(byte[])r["BusRv"],PlanRowVersion=r["PlanRv"]==DBNull.Value?null:(byte[])r["PlanRv"],DefectRowVersion=r["DefectRv"]==DBNull.Value?null:(byte[])r["DefectRv"],
                CurrentOdometer=(decimal)r["CurrentOdometer"],BusStatus=SqlMaintenanceLifecycle.String(r,"BusStatus"),DefectCode=SqlMaintenanceLifecycle.String(r,"DefectCode"),DefectStatus=SqlMaintenanceLifecycle.String(r,"DefectStatus"),PlanCode=SqlMaintenanceLifecycle.String(r,"PlanCode")};
        }
        public MaintenanceStartReview PreviewStart(long id)
        {using(var c=new SqlConnection(connectionString)){c.Open();return ReadStartReview(c,null,id);}}
        private static MaintenanceStartReview ReadStartReview(SqlConnection c,SqlTransaction tx,long id)
        {
            long bus;
            using(var cmd=new SqlCommand("SELECT BusId FROM dbo.MaintenanceWorkOrders WHERE MaintenanceWorkOrderId=@Id;",c,tx))
            {SqlMaintenanceLifecycle.Id(cmd,"@Id",id);var v=cmd.ExecuteScalar();if(v==null)return null;bus=(long)v;}
            var review=new MaintenanceStartReview();
            using(var cmd=new SqlCommand(@"SELECT t.TripId,t.TripCode,t.ServiceDate,t.ScheduledDepartureTime,t.RowVersion,a.TripAssignmentId,a.RowVersion,
(SELECT COUNT(*) FROM dbo.Tickets k WHERE k.TripId=t.TripId AND k.TicketStatus=N'Purchased') AS PurchasedTickets
FROM dbo.Trips t"+(tx==null?"":" WITH(UPDLOCK,HOLDLOCK)")+@" JOIN dbo.TripAssignments a"+(tx==null?"":" WITH(UPDLOCK,HOLDLOCK)")+@" ON a.TripId=t.TripId AND a.IsCurrent=1
WHERE a.BusId=@Bus AND t.TripStatus NOT IN(N'Completed',N'Cancelled') AND NOT EXISTS(SELECT 1 FROM dbo.TripExecutions e WHERE e.TripId=t.TripId) ORDER BY t.TripId;",c,tx))
            {SqlMaintenanceLifecycle.Id(cmd,"@Bus",bus);using(var r=cmd.ExecuteReader())while(r.Read())review.Trips.Add(new MaintenanceAffectedTrip{TripId=r.GetInt64(0),TripCode=r.GetString(1),DepartureLocal=r.GetDateTime(2).Date.Add(r.GetTimeSpan(3)),TripRowVersion=(byte[])r.GetValue(4),AssignmentId=r.GetInt64(5),AssignmentRowVersion=(byte[])r.GetValue(6),PurchasedTickets=r.GetInt32(7)});}
            review.Order=ReadOrder(c,tx,id);
            var safety=SqlMaintenanceLifecycle.ReadSafety(c,tx,bus);
            if(review.Order.Status!=MaintenanceOrderStatus.Open)review.Blockers.Add("Only an Open work order can be started.");
            if(safety.Bus.BaseOperationalState==BusOperationalState.Retired)review.Blockers.Add("A retired bus cannot enter maintenance.");
            if(safety.HasActiveExecution)review.Blockers.Add("The bus is still operating a Trip. Resolve the Trip or operational exception first.");
            if(safety.HasInProgressMaintenance)review.Blockers.Add("Another work order is already in progress for this bus.");
            using(var cmd=new SqlCommand(@"SELECT (SELECT COUNT(*) FROM dbo.FuelRequests WHERE BusId=@Bus AND RequestStatus=N'Pending'),
(SELECT COUNT(*) FROM dbo.FuelVouchers WHERE BusId=@Bus AND VoucherStatus=N'Active');",c,tx))
            {SqlMaintenanceLifecycle.Id(cmd,"@Bus",bus);using(var r=cmd.ExecuteReader())if(r.Read()){review.PendingFuelRequests=r.GetInt32(0);review.ActiveFuelVouchers=r.GetInt32(1);}}
            var fuelSignature=new List<string>();
            using(var cmd=new SqlCommand(@"SELECT N'R',FuelRequestId,RowVersion FROM dbo.FuelRequests WHERE BusId=@Bus AND RequestStatus=N'Pending'
UNION ALL SELECT N'V',FuelVoucherId,RowVersion FROM dbo.FuelVouchers WHERE BusId=@Bus AND VoucherStatus=N'Active' ORDER BY 1,2;",c,tx)){SqlMaintenanceLifecycle.Id(cmd,"@Bus",bus);using(var r=cmd.ExecuteReader())while(r.Read())fuelSignature.Add(r.GetString(0)+":"+r.GetInt64(1)+":"+Convert.ToBase64String((byte[])r.GetValue(2)));}
            review.FuelAuthorizationSignature=string.Join("|",fuelSignature);
            review.Fingerprint=MaintenancePolicy.Fingerprint(review);return review;
        }
        public IList<MaintenanceAttention> GetOtherAttention()
        {
            var rows=new List<MaintenanceAttention>();
            using(var c=new SqlConnection(connectionString))using(var cmd=new SqlCommand(@"SELECT b.BusId,b.FleetNumber,d.DefectCode,d.Severity,d.Description,d.BusDefectReportId FROM dbo.BusDefectReports d JOIN dbo.Buses b ON b.BusId=d.BusId WHERE d.DefectStatus<>N'Resolved';",c))
            {c.Open();using(var r=cmd.ExecuteReader())while(r.Read())rows.Add(new MaintenanceAttention{BusId=r.GetInt64(0),FleetNumber=r.GetString(1),Kind="Defect",Reference=r.GetString(2),Reason=r.GetString(3)+": "+r.GetString(4),DefectId=r.GetInt64(5),Blocking=r.GetString(3)=="Critical"});}
            foreach(var b in GetBuses())
            {
                var x=GetBusSafety(b.BusId);var v=x.Bus;var today=clock.Today;
                if(v.LicenceExpiryDate.Date<today)rows.Add(Compliance(v,"Licence"));
                if(v.RoadworthyExpiryDate.Date<today)rows.Add(Compliance(v,"Roadworthy"));
                if(v.InsuranceExpiryDate.Date<today)rows.Add(Compliance(v,"Insurance"));
                if(v.BaseOperationalState==BusOperationalState.UnderMaintenance)rows.Add(new MaintenanceAttention{BusId=v.BusId,FleetNumber=v.FleetNumber,Kind="Under maintenance",Reason="Bus is unavailable until deliberate Return to Service.",Blocking=true});
            }
            return rows;
        }
        private static MaintenanceAttention Compliance(BusDetails b,string name){return new MaintenanceAttention{BusId=b.BusId,FleetNumber=b.FleetNumber,Kind="Compliance",Reference=name,Reason=name+" has expired. Update the fleet compliance information.",Blocking=true};}
        public IList<MaintenanceHistoryEntry> GetHistory(long busId)
        {
            var rows=new List<MaintenanceHistoryEntry>();
            using(var c=new SqlConnection(connectionString))using(var cmd=new SqlCommand(@"SELECT w.WorkOrderCode,p.RecordedUtc,p.Note FROM dbo.MaintenanceProgressEntries p JOIN dbo.MaintenanceWorkOrders w ON w.MaintenanceWorkOrderId=p.MaintenanceWorkOrderId WHERE w.BusId=@Bus
UNION ALL SELECT COALESCE(w.WorkOrderCode,N'Fleet'),h.OccurredUtc,h.FromStatus+N' -> '+h.ToStatus+N': '+h.Reason FROM dbo.BusVehicleStatusHistory h LEFT JOIN dbo.MaintenanceWorkOrders w ON w.MaintenanceWorkOrderId=h.MaintenanceWorkOrderId WHERE h.BusId=@Bus ORDER BY 2 DESC;",c))
            {SqlMaintenanceLifecycle.Id(cmd,"@Bus",busId);c.Open();using(var r=cmd.ExecuteReader())while(r.Read())rows.Add(new MaintenanceHistoryEntry{Reference=r.GetString(0),OccurredUtc=r.GetDateTime(1),Description=r.GetString(2)});}
            return rows;
        }
        private static void Search(SqlCommand cmd,string text){text=string.IsNullOrWhiteSpace(text)?null:text.Trim();if(text!=null&&text.Length>100)text=text.Substring(0,100);SqlMaintenanceLifecycle.Text(cmd,"@Search",100,text);}
    }
}
