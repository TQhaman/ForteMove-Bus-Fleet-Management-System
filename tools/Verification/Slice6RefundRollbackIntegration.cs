using System;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;

internal static class Slice6RefundRollbackIntegration
{
    public static int Main()
    {
        const string connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=ForteMove;Integrated Security=True;Application Name=ForteMove.Slice6RefundVerification";
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    long userId; long profileId; long walletId; long ticketId; long tripId; long actorId;
                    using (SqlCommand command = Command(connection, transaction, @"
DECLARE @RoleId int=(SELECT RoleId FROM dbo.Roles WHERE RoleCode=N'Passenger');
DECLARE @ActorId bigint=(SELECT MIN(ua.UserAccountId) FROM dbo.UserAccounts ua JOIN dbo.Roles r ON r.RoleId=ua.RoleId WHERE r.RoleCode=N'TransportAdministrator');
DECLARE @TripId bigint=(SELECT MIN(TripId) FROM dbo.Trips);
INSERT dbo.UserAccounts(RoleId,Email,NormalizedEmail,PasswordAlgorithm,PasswordHash,PasswordSalt,PasswordIterations,IsActive,MustChangePassword,FailedLoginCount,CreatedUtc,UpdatedUtc)
VALUES(@RoleId,N'slice6.refund.rollback@example.test',N'SLICE6.REFUND.ROLLBACK@EXAMPLE.TEST',N'PBKDF2-HMAC-SHA256',CONVERT(varbinary(32),REPLICATE('H',32)),CONVERT(varbinary(32),REPLICATE('S',32)),600000,1,0,0,SYSUTCDATETIME(),SYSUTCDATETIME());
DECLARE @UserId bigint=SCOPE_IDENTITY();
INSERT dbo.PassengerProfiles(UserAccountId,FirstName,LastName,CreatedUtc,UpdatedUtc) VALUES(@UserId,N'Refund',N'Rollback',SYSUTCDATETIME(),SYSUTCDATETIME());
DECLARE @ProfileId bigint=SCOPE_IDENTITY();
INSERT dbo.PassengerWallets(PassengerProfileId,CurrentBalance,CreatedUtc,UpdatedUtc) VALUES(@ProfileId,25.00,SYSUTCDATETIME(),SYSUTCDATETIME());
DECLARE @WalletId bigint=SCOPE_IDENTITY();
INSERT dbo.Tickets(TicketCode,PassengerProfileId,TripId,FareAmount,TicketStatus,PurchasedUtc,UpdatedUtc) VALUES(N'TKT-RF-ROLLBACK',@ProfileId,@TripId,15.00,N'Purchased',SYSUTCDATETIME(),SYSUTCDATETIME());
DECLARE @TicketId bigint=SCOPE_IDENTITY();
INSERT dbo.WalletTransactions(WalletTransactionCode,PassengerWalletId,TransactionType,Amount,BalanceBefore,BalanceAfter,TicketId,InitiatedByUserAccountId,OperationToken,OccurredUtc)
VALUES(N'WTX-RF-PURCHASE',@WalletId,N'TicketPurchase',15.00,40.00,25.00,@TicketId,@UserId,NEWID(),SYSUTCDATETIME());
SELECT @UserId,@ProfileId,@WalletId,@TicketId,@TripId,@ActorId;"))
                    using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (!reader.Read()) throw new InvalidOperationException("Refund verification setup failed.");
                        userId=reader.GetInt64(0);profileId=reader.GetInt64(1);walletId=reader.GetInt64(2);ticketId=reader.GetInt64(3);tripId=reader.GetInt64(4);actorId=reader.GetInt64(5);
                    }

                    Type commerce = typeof(ForteMove.Data.Repositories.SqlPassengerRepository).Assembly.GetType("ForteMove.Data.Internal.SqlPassengerCommerce", true);
                    MethodInfo acquire = commerce.GetMethod("AcquireLock", BindingFlags.Static|BindingFlags.NonPublic);
                    MethodInfo refund = commerce.GetMethod("RefundPurchasedTickets", BindingFlags.Static|BindingFlags.NonPublic);
                    acquire.Invoke(null,new object[]{connection,transaction});
                    DateTime utc=DateTime.UtcNow;
                    refund.Invoke(null,new object[]{connection,transaction,tripId,actorId,"Rollback cancellation",utc});

                    using (SqlCommand command = Command(connection, transaction, @"
SELECT t.TicketStatus,t.FareAmount,w.CurrentBalance,
 (SELECT COUNT(*) FROM dbo.WalletTransactions x WHERE x.TicketId=t.TicketId AND x.TransactionType=N'TripCancellationRefund'),
 (SELECT COUNT(*) FROM dbo.AuditEntries a WHERE a.EventType=N'TicketRefunded' AND a.EntityId=CONVERT(nvarchar(100),t.TicketId)),
 (SELECT COUNT(*) FROM dbo.AuditEntries a WHERE a.EventType=N'WalletRefunded' AND a.Detail LIKE N'%TicketCode='+t.TicketCode+N'%')
FROM dbo.Tickets t JOIN dbo.PassengerWallets w ON w.PassengerProfileId=t.PassengerProfileId WHERE t.TicketId=@TicketId;"))
                    {
                        command.Parameters.Add("@TicketId",SqlDbType.BigInt).Value=ticketId;
                        using(SqlDataReader reader=command.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if(!reader.Read()||reader.GetString(0)!="Refunded"||reader.GetDecimal(1)!=15m||reader.GetDecimal(2)!=40m||reader.GetInt32(3)!=1||reader.GetInt32(4)!=1||reader.GetInt32(5)!=1)
                                throw new InvalidOperationException("Atomic refund values or audits are incorrect.");
                        }
                    }

                    refund.Invoke(null,new object[]{connection,transaction,tripId,actorId,"Repeated rollback cancellation",utc.AddSeconds(1)});
                    using(SqlCommand command=Command(connection,transaction,"SELECT COUNT(*) FROM dbo.WalletTransactions WHERE TicketId=@TicketId AND TransactionType=N'TripCancellationRefund';"))
                    {
                        command.Parameters.Add("@TicketId",SqlDbType.BigInt).Value=ticketId;
                        if(Convert.ToInt32(command.ExecuteScalar())!=1)throw new InvalidOperationException("Duplicate refund protection failed.");
                    }

                    transaction.Rollback();
                    Console.WriteLine("Slice 6 refund integration passed: exact refund, Ticket/Wallet audits, duplicate prevention, rollback.");
                    return 0;
                }
                catch(Exception exception)
                {
                    if(transaction.Connection!=null)transaction.Rollback();
                    Console.Error.WriteLine(exception is TargetInvocationException&&exception.InnerException!=null?exception.InnerException.ToString():exception.ToString());
                    return 1;
                }
            }
        }
    }

    private static SqlCommand Command(SqlConnection connection,SqlTransaction transaction,string sql)
    {
        SqlCommand command=connection.CreateCommand();command.Transaction=transaction;command.CommandText=sql;return command;
    }
}
