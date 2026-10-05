using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using ForteMove.Business.Identifiers;
using ForteMove.Business.Maintenance;
using ForteMove.Data.Internal;
using ForteMove.Models.Maintenance;
namespace ForteMove.Data.Repositories
{
    public sealed partial class SqlMaintenanceRepository
    {
        public long SavePlan(SaveMaintenancePlanRequest r,long actor){return Write(actor,false,(c,tx,now,utc)=>SavePlanInTransaction(c,tx,r,actor,now,utc));}
        internal static long SavePlanInTransaction(SqlConnection c,SqlTransaction tx,SaveMaintenancePlanRequest r,long actor,DateTime now,DateTime utc)
        {
            var safety=SqlMaintenanceLifecycle.ReadSafety(c,tx,r.BusId);Expect(safety!=null,"Select an existing bus.");
            var prior=r.MaintenancePlanId>0?ReadPlan(c,tx,r.MaintenancePlanId):null;
            if(r.MaintenancePlanId>0)
            {
                Expect(prior!=null,"The plan is unavailable.");Match(r.RowVersion,prior.RowVersion,"The plan");
                using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.MaintenanceWorkOrders WITH(UPDLOCK,HOLDLOCK) WHERE MaintenancePlanId=@Id AND OrderStatus IN(N'Open',N'InProgress');",c,tx))
                {SqlMaintenanceLifecycle.Id(cmd,"@Id",r.MaintenancePlanId);Expect((int)cmd.ExecuteScalar()==0,"Complete or cancel the linked active work order before editing this plan.");}
            }
            SqlMaintenanceLifecycle.Errors(MaintenancePolicy.ValidatePlan(r,prior,safety.Bus.OdometerKilometres,now.Date));
            var code=prior==null?IdentifierCodePolicy.FormatMaintenancePlanCode(SqlMaintenanceLifecycle.Next(c,tx,"MaintenancePlans","PlanCode",4)):prior.PlanCode;
            var sql=prior==null?@"INSERT dbo.MaintenancePlans(PlanCode,BusId,ServiceName,IntervalDays,IntervalKilometres,LastServiceDate,LastServiceOdometer,NextDueDate,NextDueOdometer,BlocksOperationWhenOverdue,IsActive,CreatedByUserAccountId,UpdatedByUserAccountId,CreatedUtc,UpdatedUtc)
VALUES(@Code,@Bus,@Name,@Days,@Km,@LastDate,@LastOdo,@NextDate,@NextOdo,@Blocks,@Active,@Actor,@Actor,@Utc,@Utc);SELECT CONVERT(bigint,SCOPE_IDENTITY());":@"UPDATE dbo.MaintenancePlans SET ServiceName=@Name,IntervalDays=@Days,IntervalKilometres=@Km,LastServiceDate=@LastDate,LastServiceOdometer=@LastOdo,NextDueDate=@NextDate,NextDueOdometer=@NextOdo,BlocksOperationWhenOverdue=@Blocks,IsActive=@Active,UpdatedByUserAccountId=@Actor,UpdatedUtc=@Utc WHERE MaintenancePlanId=@Id AND RowVersion=@Rv;IF @@ROWCOUNT<>1 THROW 51202,'The plan changed.',1;SELECT @Id;";
            long id;
            using(var cmd=new SqlCommand(sql,c,tx))
            {
                SqlMaintenanceLifecycle.Text(cmd,"@Code",30,code);SqlMaintenanceLifecycle.Id(cmd,"@Bus",r.BusId);SqlMaintenanceLifecycle.Text(cmd,"@Name",150,r.ServiceName);
                cmd.Parameters.Add("@Days",SqlDbType.Int).Value=(object)r.IntervalDays??DBNull.Value;SqlMaintenanceLifecycle.Decimal(cmd,"@Km",r.IntervalKilometres);
                SqlMaintenanceLifecycle.Date(cmd,"@LastDate",r.LastServiceDate);SqlMaintenanceLifecycle.Decimal(cmd,"@LastOdo",r.LastServiceOdometer);SqlMaintenanceLifecycle.Date(cmd,"@NextDate",r.NextDueDate);SqlMaintenanceLifecycle.Decimal(cmd,"@NextOdo",r.NextDueOdometer);
                cmd.Parameters.Add("@Blocks",SqlDbType.Bit).Value=r.BlocksOperationWhenOverdue;cmd.Parameters.Add("@Active",SqlDbType.Bit).Value=r.IsActive;
                SqlMaintenanceLifecycle.Id(cmd,"@Actor",actor);SqlMaintenanceLifecycle.Utc(cmd,"@Utc",utc);SqlMaintenanceLifecycle.Id(cmd,"@Id",r.MaintenancePlanId);SqlMaintenanceLifecycle.Version(cmd,"@Rv",r.RowVersion);
                id=(long)cmd.ExecuteScalar();
            }
            var detail="Code="+code+";NextDate="+r.NextDueDate+";NextOdometer="+r.NextDueOdometer+";Blocks="+r.BlocksOperationWhenOverdue+";Active="+r.IsActive;
            if(prior!=null)detail+=";PreviousDate="+prior.NextDueDate+";PreviousOdometer="+prior.NextDueOdometer+";PreviousBlocks="+prior.BlocksOperationWhenOverdue+";PreviousActive="+prior.IsActive;
            Audit(c,tx,actor,prior==null?"MaintenancePlanCreated":"MaintenancePlanUpdated","MaintenancePlan",id,detail,utc);
            return id;
        }
    }
}
