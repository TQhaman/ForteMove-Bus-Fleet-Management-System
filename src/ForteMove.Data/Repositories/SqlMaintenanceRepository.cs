using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Time;
using ForteMove.Data.Internal;
using ForteMove.Models.Maintenance;
namespace ForteMove.Data.Repositories
{
    public sealed partial class SqlMaintenanceRepository : IMaintenanceRepository
    {
        private readonly string connectionString;
        private readonly IClock clock;
        public SqlMaintenanceRepository(string connectionString):this(connectionString,new SystemClock()){}
        public SqlMaintenanceRepository(string connectionString,IClock clock){if(string.IsNullOrWhiteSpace(connectionString))throw new ArgumentException("A connection string is required.");if(clock==null)throw new ArgumentNullException("clock");this.connectionString=connectionString;this.clock=clock;}
        private T Write<T>(long actor,bool fuel,Func<SqlConnection,SqlTransaction,DateTime,DateTime,T> action)
        {
            try
            {
                using(var c=new SqlConnection(connectionString)){c.Open();using(var tx=c.BeginTransaction(IsolationLevel.Serializable))
                {
                    SqlFuelLifecycle.AcquireAssignmentLock(c,tx);
                    if(fuel)SqlFuelLifecycle.AcquireLock(c,tx);
                    SqlMaintenanceLifecycle.AcquireLock(c,tx);SqlMaintenanceLifecycle.Authorise(c,tx,actor);
                    var utc=clock.UtcNow;var now=clock.ToOperationalTime(utc);
                    var value=action(c,tx,now,utc);tx.Commit();return value;
                }}
            }
            catch(MaintenancePersistenceException){throw;}
            catch(TripOperationsPersistenceException x){throw new MaintenancePersistenceException(x.Message,x);}
            catch(SqlException x){throw new MaintenancePersistenceException(x.Number==2601||x.Number==2627?"An active work order already exists for this source, or the operation was submitted twice. Refresh and review the existing work.":"The maintenance action could not be saved. Refresh and try again.",x);}
        }
        private static void Audit(SqlConnection c,SqlTransaction tx,long actor,string name,string entity,long id,string detail,DateTime utc)
        {SqlAuditWriter.Write(c,tx,actor,name,entity,id.ToString(CultureInfo.InvariantCulture),detail,null,utc);}
        private static void Expect(bool condition,string message){if(!condition)throw new MaintenancePersistenceException(message);}
        private static void Match(byte[] expected,byte[] actual,string what){Expect(SqlMaintenanceLifecycle.Same(expected,actual),what+" changed. Refresh before continuing.");}
    }
}
