using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Identifiers;
using ForteMove.Data.Internal;
using ForteMove.Models.Fuel;
namespace ForteMove.Data.Repositories
{
    public sealed class SqlFuelStationRepository : IFuelStationRepository
    {
        private readonly string connectionString;
        public SqlFuelStationRepository(string connectionString) { this.connectionString=connectionString; }
        internal const string StationSelect=@"SELECT s.FuelStationId,s.StationCode,s.StationName,s.AreaDescription,s.IsActive,s.RowVersion,
CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.FuelStationCapabilities cap WHERE cap.FuelStationId=s.FuelStationId AND cap.FuelSupplyType=N'Diesel') THEN 1 ELSE 0 END AS bit),
CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.FuelStationCapabilities cap WHERE cap.FuelStationId=s.FuelStationId AND cap.FuelSupplyType=N'ElectricCharging') THEN 1 ELSE 0 END AS bit)
FROM dbo.FuelStations s ";
        public IList<FuelStation> GetStations(long actor)
        {
            using(var connection=new SqlConnection(connectionString))
            {
                connection.Open();SqlFuelLifecycle.Authorise(connection,null,actor,false);
                var items=new List<FuelStation>();
                using(var command=SqlFuelLifecycle.Command(connection,null,StationSelect+"ORDER BY s.StationCode;"))
                using(var reader=command.ExecuteReader()) while(reader.Read()) items.Add(Read(reader));
                return items;
            }
        }
        public FuelStation GetStation(long id,long actor)
        {
            using(var connection=new SqlConnection(connectionString))
            { connection.Open();SqlFuelLifecycle.Authorise(connection,null,actor,false);return ReadStation(connection,null,id); }
        }
        internal static FuelStation ReadStation(SqlConnection connection,SqlTransaction transaction,long id)
        {
            string sql=StationSelect.Replace("FROM dbo.FuelStations s ",transaction==null?"FROM dbo.FuelStations s ":"FROM dbo.FuelStations s WITH(UPDLOCK,HOLDLOCK) ");
            using(var command=SqlFuelLifecycle.Command(connection,transaction,sql+"WHERE s.FuelStationId=@Id;"))
            {
                SqlFuelLifecycle.Id(command,"@Id",id);
                using(var reader=command.ExecuteReader()) return reader.Read()?Read(reader):null;
            }
        }
        private static FuelStation Read(SqlDataReader reader)
        {
            return new FuelStation { FuelStationId=reader.GetInt64(0),StationCode=reader.GetString(1),StationName=reader.GetString(2),
                AreaDescription=reader.GetString(3),IsActive=reader.GetBoolean(4),RowVersion=(byte[])reader.GetValue(5),
                SupportsDiesel=reader.GetBoolean(6),SupportsElectricCharging=reader.GetBoolean(7) };
        }
        public long Save(SaveFuelStationRequest request,long actor,DateTime utc)
        {
            try
            {
                using(var connection=new SqlConnection(connectionString))
                { connection.Open();using(var transaction=connection.BeginTransaction(IsolationLevel.Serializable))
                    { long id=SaveInTransaction(connection,transaction,request,actor,utc);transaction.Commit();return id; } }
            }
            catch(SqlException error) { throw new FuelPersistenceException("The Station could not be saved. Reload it and try again.",error); }
        }
        internal static long SaveInTransaction(SqlConnection connection,SqlTransaction transaction,SaveFuelStationRequest request,long actor,DateTime utc)
        {
            SqlFuelLifecycle.AcquireLock(connection,transaction);SqlFuelLifecycle.Authorise(connection,transaction,actor,false);
            if(!request.SupportsDiesel && !request.SupportsElectricCharging) throw new FuelPersistenceException("Choose at least one supported supply type.");
            long id=request.FuelStationId;
            string code=id==0?IdentifierCodePolicy.FormatFuelStationCode(SqlFuelLifecycle.Next(connection,transaction,"FuelStations","StationCode",4)):null;
            string sql=id==0?@"INSERT dbo.FuelStations(StationCode,StationName,AreaDescription,IsActive,CreatedByUserAccountId,UpdatedByUserAccountId,CreatedUtc,UpdatedUtc)
VALUES(@Code,@Name,@Area,@Active,@Actor,@Actor,@Utc,@Utc);SELECT CONVERT(bigint,SCOPE_IDENTITY());":@"UPDATE dbo.FuelStations SET StationName=@Name,AreaDescription=@Area,IsActive=@Active,UpdatedByUserAccountId=@Actor,UpdatedUtc=@Utc WHERE FuelStationId=@Id AND RowVersion=@Rv;
IF @@ROWCOUNT<>1 THROW 51102,'The Station changed. Reload it before saving.',1;
DELETE dbo.FuelStationCapabilities WHERE FuelStationId=@Id;SELECT @Id;";
            using(var command=SqlFuelLifecycle.Command(connection,transaction,sql))
            {
                SqlFuelLifecycle.Id(command,"@Id",id);SqlFuelLifecycle.Text(command,"@Code",20,code);SqlFuelLifecycle.Text(command,"@Name",150,request.StationName);
                SqlFuelLifecycle.Text(command,"@Area",250,request.AreaDescription);command.Parameters.Add("@Active",SqlDbType.Bit).Value=request.IsActive;
                SqlFuelLifecycle.Id(command,"@Actor",actor);SqlFuelLifecycle.Utc(command,"@Utc",utc);SqlFuelLifecycle.Version(command,"@Rv",request.RowVersion);
                id=Convert.ToInt64(command.ExecuteScalar(),CultureInfo.InvariantCulture);
            }
            foreach(string supply in new[]{"Diesel","ElectricCharging"})
            {
                if(supply=="Diesel"?!request.SupportsDiesel:!request.SupportsElectricCharging) continue;
                using(var command=SqlFuelLifecycle.Command(connection,transaction,"INSERT dbo.FuelStationCapabilities(FuelStationId,FuelSupplyType) VALUES(@Id,@Supply);"))
                { SqlFuelLifecycle.Id(command,"@Id",id);SqlFuelLifecycle.Text(command,"@Supply",30,supply);command.ExecuteNonQuery(); }
            }
            SqlAuditWriter.Write(connection,transaction,actor,request.FuelStationId==0?"FuelStationCreated":"FuelStationUpdated","FuelStation",id.ToString(CultureInfo.InvariantCulture),
                "Active="+request.IsActive+";Diesel="+request.SupportsDiesel+";ElectricCharging="+request.SupportsElectricCharging,null,utc);
            return id;
        }
    }
}
