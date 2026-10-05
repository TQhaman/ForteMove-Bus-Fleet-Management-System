using System;
using System.Data.SqlClient;
using System.Globalization;
using ForteMove.Business.Exceptions;
using ForteMove.Models.Operations;
namespace ForteMove.Data.Internal
{
    internal static class SqlDefectLifecycle
    {
        internal static void Update(SqlConnection c,SqlTransaction tx,UpdateDefectRequest r,long actor,DateTime utc,bool resolve)
        {
            try{SqlMaintenanceLifecycle.Authorise(c,tx,actor);}catch(MaintenancePersistenceException e){throw new TripOperationsPersistenceException(e.Message,e);}
            if(string.IsNullOrWhiteSpace(r.Note)||r.Note.Trim().Length>1000)throw new TripOperationsPersistenceException("Record an administrator note within 1,000 characters.",null);
            var sql=resolve?@"UPDATE dbo.BusDefectReports SET DefectStatus=N'Resolved',ResolvedByUserAccountId=@Actor,ResolvedUtc=@Utc,ResolutionNote=@Note,UpdatedUtc=@Utc WHERE BusDefectReportId=@Id AND RowVersion=@Rv AND DefectStatus IN(N'Open',N'Reviewed');":@"UPDATE dbo.BusDefectReports SET DefectStatus=N'Reviewed',ReviewedByUserAccountId=@Actor,ReviewedUtc=@Utc,ReviewNote=@Note,UpdatedUtc=@Utc WHERE BusDefectReportId=@Id AND RowVersion=@Rv AND DefectStatus=N'Open';";
            using(var cmd=new SqlCommand(sql,c,tx))
            {
                SqlMaintenanceLifecycle.Id(cmd,"@Id",r.BusDefectReportId);SqlMaintenanceLifecycle.Version(cmd,"@Rv",r.RowVersion);SqlMaintenanceLifecycle.Id(cmd,"@Actor",actor);SqlMaintenanceLifecycle.Utc(cmd,"@Utc",utc);SqlMaintenanceLifecycle.Text(cmd,"@Note",1000,r.Note);
                if(cmd.ExecuteNonQuery()!=1)throw new TripOperationsPersistenceException("The defect report changed. Refresh it and try again.",null);
            }
            SqlAuditWriter.Write(c,tx,actor,resolve?"DefectResolved":"DefectReviewed","BusDefectReport",r.BusDefectReportId.ToString(CultureInfo.InvariantCulture),"Note="+r.Note,null,utc);
        }
    }
}
