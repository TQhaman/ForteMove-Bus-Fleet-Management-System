using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Identifiers;
using ForteMove.Business.Maintenance;
using ForteMove.Business.Time;
using ForteMove.Data.Internal;
using ForteMove.Models.Maintenance;
namespace ForteMove.Data.Repositories
{
    public sealed class SqlRepairProviderRepository:IRepairProviderRepository
    {
        private readonly string connectionString;private readonly IClock clock;
        public SqlRepairProviderRepository(string connectionString):this(connectionString,new SystemClock()){}
        public SqlRepairProviderRepository(string connectionString,IClock clock){if(string.IsNullOrWhiteSpace(connectionString))throw new ArgumentException("A connection string is required.");if(clock==null)throw new ArgumentNullException("clock");this.connectionString=connectionString;this.clock=clock;}
        public IList<RepairProvider> GetProviders(){var rows=new List<RepairProvider>();using(var c=new SqlConnection(connectionString))using(var cmd=new SqlCommand("SELECT * FROM dbo.RepairProviders ORDER BY ProviderName;",c)){c.Open();using(var r=cmd.ExecuteReader())while(r.Read())rows.Add(Map(r));}return rows;}
        public RepairProvider GetProvider(long id){using(var c=new SqlConnection(connectionString)){c.Open();return Read(c,null,id);}}
        internal static RepairProvider Read(SqlConnection c,SqlTransaction tx,long id)
        {using(var cmd=new SqlCommand("SELECT * FROM dbo.RepairProviders"+(tx==null?"":" WITH(UPDLOCK,HOLDLOCK)")+" WHERE RepairProviderId=@Id;",c,tx)){SqlMaintenanceLifecycle.Id(cmd,"@Id",id);using(var r=cmd.ExecuteReader())return r.Read()?Map(r):null;}}
        private static RepairProvider Map(SqlDataReader r){return new RepairProvider{RepairProviderId=SqlMaintenanceLifecycle.Long(r,"RepairProviderId"),ProviderCode=SqlMaintenanceLifecycle.String(r,"ProviderCode"),ProviderName=SqlMaintenanceLifecycle.String(r,"ProviderName"),AreaDescription=SqlMaintenanceLifecycle.String(r,"AreaDescription"),Phone=SqlMaintenanceLifecycle.String(r,"Phone"),Email=SqlMaintenanceLifecycle.String(r,"Email"),IsActive=(bool)r["IsActive"],RowVersion=(byte[])r["RowVersion"]};}
        public long SaveProvider(SaveRepairProviderRequest r,long actor)
        {
            try{using(var c=new SqlConnection(connectionString)){c.Open();using(var tx=c.BeginTransaction(IsolationLevel.Serializable))
            {SqlMaintenanceLifecycle.AcquireLock(c,tx);SqlMaintenanceLifecycle.Authorise(c,tx,actor);var id=SaveInTransaction(c,tx,r,actor,clock.UtcNow);tx.Commit();return id;}}}
            catch(MaintenancePersistenceException){throw;}catch(SqlException x){throw new MaintenancePersistenceException("The repair provider could not be saved. Refresh and try again.",x);}
        }
        internal static long SaveInTransaction(SqlConnection c,SqlTransaction tx,SaveRepairProviderRequest r,long actor,DateTime utc)
        {
            SqlMaintenanceLifecycle.Errors(MaintenancePolicy.ValidateProvider(r));
            var prior=r.RepairProviderId>0?Read(c,tx,r.RepairProviderId):null;
            if(r.RepairProviderId>0 && (prior==null || !SqlMaintenanceLifecycle.Same(r.RowVersion,prior.RowVersion)))throw new MaintenancePersistenceException("The provider changed. Refresh before saving.");
            var code=prior==null?IdentifierCodePolicy.FormatRepairProviderCode(SqlMaintenanceLifecycle.Next(c,tx,"RepairProviders","ProviderCode",4)):prior.ProviderCode;
            var sql=prior==null?@"INSERT dbo.RepairProviders(ProviderCode,ProviderName,AreaDescription,Phone,Email,IsActive,CreatedByUserAccountId,UpdatedByUserAccountId,CreatedUtc,UpdatedUtc) VALUES(@Code,@Name,@Area,@Phone,@Email,@Active,@Actor,@Actor,@Utc,@Utc);SELECT CONVERT(bigint,SCOPE_IDENTITY());":@"UPDATE dbo.RepairProviders SET ProviderName=@Name,AreaDescription=@Area,Phone=@Phone,Email=@Email,IsActive=@Active,UpdatedByUserAccountId=@Actor,UpdatedUtc=@Utc WHERE RepairProviderId=@Id AND RowVersion=@Rv;IF @@ROWCOUNT<>1 THROW 51203,'The provider changed.',1;SELECT @Id;";
            long id;
            using(var cmd=new SqlCommand(sql,c,tx))
            {
                SqlMaintenanceLifecycle.Text(cmd,"@Code",30,code);SqlMaintenanceLifecycle.Text(cmd,"@Name",150,r.ProviderName);SqlMaintenanceLifecycle.Text(cmd,"@Area",250,r.AreaDescription);SqlMaintenanceLifecycle.Text(cmd,"@Phone",30,r.Phone);SqlMaintenanceLifecycle.Text(cmd,"@Email",254,r.Email);
                cmd.Parameters.Add("@Active",SqlDbType.Bit).Value=r.IsActive;SqlMaintenanceLifecycle.Id(cmd,"@Actor",actor);SqlMaintenanceLifecycle.Utc(cmd,"@Utc",utc);SqlMaintenanceLifecycle.Id(cmd,"@Id",r.RepairProviderId);SqlMaintenanceLifecycle.Version(cmd,"@Rv",r.RowVersion);id=(long)cmd.ExecuteScalar();
            }
            SqlAuditWriter.Write(c,tx,actor,prior==null?"RepairProviderCreated":"RepairProviderUpdated","RepairProvider",id.ToString(CultureInfo.InvariantCulture),"Code="+code+";Active="+r.IsActive,null,utc);return id;
        }
    }
}
