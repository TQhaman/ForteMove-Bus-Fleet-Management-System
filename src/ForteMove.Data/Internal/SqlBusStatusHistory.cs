using System;
using System.Data.SqlClient;
namespace ForteMove.Data.Internal
{
    internal static class SqlBusStatusHistory
    {
        internal static void Write(SqlConnection c,SqlTransaction tx,long bus,string from,string to,string reason,long? order,long actor,DateTime utc)
        {
            if(from==to)return;
            using(var cmd=new SqlCommand(@"INSERT dbo.BusVehicleStatusHistory(BusId,FromStatus,ToStatus,Reason,MaintenanceWorkOrderId,OccurredUtc,ActorUserAccountId)
VALUES(@Bus,@From,@To,@Reason,@Order,@Utc,@Actor);",c,tx))
            {
                SqlMaintenanceLifecycle.Id(cmd,"@Bus",bus);SqlMaintenanceLifecycle.Text(cmd,"@From",30,from);SqlMaintenanceLifecycle.Text(cmd,"@To",30,to);
                SqlMaintenanceLifecycle.Text(cmd,"@Reason",1000,reason);SqlMaintenanceLifecycle.NullableId(cmd,"@Order",order);SqlMaintenanceLifecycle.Id(cmd,"@Actor",actor);SqlMaintenanceLifecycle.Utc(cmd,"@Utc",utc);cmd.ExecuteNonQuery();
            }
        }
    }
}
