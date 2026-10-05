using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Identifiers;
using ForteMove.Business.Services;

namespace ForteMove.Data.Internal
{
    internal static class SqlPassengerCommerce
    {
        internal static void AcquireLock(SqlConnection connection, SqlTransaction transaction)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
DECLARE @Result int;
EXEC @Result=sys.sp_getapplock
    @Resource=N'ForteMove.PassengerCommerce',
    @LockMode=N'Exclusive',
    @LockOwner=N'Transaction',
    @LockTimeout=15000;
IF @Result < 0 THROW 51052, 'Could not acquire the Passenger commerce lock.', 1;"))
            {
                command.ExecuteNonQuery();
            }
        }

        internal static long GetNextTicketSequence(SqlConnection connection, SqlTransaction transaction)
        {
            using (SqlCommand command = CreateCommand(connection, transaction,
                "SELECT ISNULL(MAX(TRY_CONVERT(bigint,SUBSTRING(TicketCode,5,30))),0)+1 FROM dbo.Tickets WITH(UPDLOCK,HOLDLOCK) WHERE TicketCode LIKE N'TKT-%';"))
                return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        internal static long GetNextWalletTransactionSequence(SqlConnection connection, SqlTransaction transaction)
        {
            using (SqlCommand command = CreateCommand(connection, transaction,
                "SELECT ISNULL(MAX(TRY_CONVERT(bigint,SUBSTRING(WalletTransactionCode,5,30))),0)+1 FROM dbo.WalletTransactions WITH(UPDLOCK,HOLDLOCK) WHERE WalletTransactionCode LIKE N'WTX-%';"))
                return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        internal static PassengerRefundSummary RefundPurchasedTickets(
            SqlConnection connection, SqlTransaction transaction, long tripId,
            long actorUserAccountId, string reason, DateTime utcNow)
        {
            List<RefundRow> rows = new List<RefundRow>();
            using (SqlCommand command = CreateCommand(connection, transaction, @"
SELECT t.TicketId,t.TicketCode,t.FareAmount,w.PassengerWalletId,w.CurrentBalance
FROM dbo.Tickets AS t WITH(UPDLOCK,HOLDLOCK)
INNER JOIN dbo.PassengerWallets AS w WITH(UPDLOCK,HOLDLOCK)
    ON w.PassengerProfileId=t.PassengerProfileId
WHERE t.TripId=@TripId AND t.TicketStatus=N'Purchased'
ORDER BY w.PassengerWalletId,t.TicketId;"))
            {
                command.Parameters.Add("@TripId", SqlDbType.BigInt).Value = tripId;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(new RefundRow
                        {
                            TicketId = reader.GetInt64(0), TicketCode = reader.GetString(1),
                            FareAmount = reader.GetDecimal(2), WalletId = reader.GetInt64(3),
                            Balance = reader.GetDecimal(4)
                        });
                    }
                }
            }

            long transactionSequence = rows.Count == 0 ? 0 : GetNextWalletTransactionSequence(connection, transaction);
            decimal total = 0m;
            foreach (RefundRow row in rows)
            {
                if (row.FareAmount > PassengerService.MaximumSupportedBalance - row.Balance)
                    throw new PassengerPersistenceException("A Passenger wallet cannot accept the required refund within the supported monetary range. Nothing was cancelled.", string.Empty, null);
                decimal after = row.Balance + row.FareAmount;
                string transactionCode = IdentifierCodePolicy.FormatWalletTransactionCode(transactionSequence++);

                using (SqlCommand command = CreateCommand(connection, transaction, @"
UPDATE dbo.Tickets
SET TicketStatus=N'Refunded',RefundedUtc=@Utc,RefundedByUserAccountId=@Actor,
    RefundReason=@Reason,UpdatedUtc=@Utc
WHERE TicketId=@TicketId AND TicketStatus=N'Purchased';
IF @@ROWCOUNT<>1 THROW 51053,'A Passenger Ticket changed during cancellation.',1;

UPDATE dbo.PassengerWallets
SET CurrentBalance=@BalanceAfter,UpdatedUtc=@Utc
WHERE PassengerWalletId=@WalletId AND CurrentBalance=@BalanceBefore;
IF @@ROWCOUNT<>1 THROW 51054,'A Passenger wallet changed during cancellation.',1;

INSERT dbo.WalletTransactions
    (WalletTransactionCode,PassengerWalletId,TransactionType,Amount,BalanceBefore,
     BalanceAfter,TicketId,InitiatedByUserAccountId,OperationToken,OccurredUtc)
VALUES
    (@TransactionCode,@WalletId,N'TripCancellationRefund',@Amount,@BalanceBefore,
     @BalanceAfter,@TicketId,@Actor,@OperationToken,@Utc);"))
                {
                    AddString(command, "@TransactionCode", 20, transactionCode);
                    command.Parameters.Add("@WalletId", SqlDbType.BigInt).Value = row.WalletId;
                    AddMoney(command, "@Amount", row.FareAmount);
                    AddMoney(command, "@BalanceBefore", row.Balance);
                    AddMoney(command, "@BalanceAfter", after);
                    command.Parameters.Add("@TicketId", SqlDbType.BigInt).Value = row.TicketId;
                    command.Parameters.Add("@Actor", SqlDbType.BigInt).Value = actorUserAccountId;
                    command.Parameters.Add("@OperationToken", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
                    AddString(command, "@Reason", 500, reason);
                    AddUtc(command, "@Utc", utcNow);
                    command.ExecuteNonQuery();
                }

                string detail = "TicketCode=" + row.TicketCode + ";Amount=" + row.FareAmount.ToString("0.00", CultureInfo.InvariantCulture) + ";Reason=" + reason;
                SqlAuditWriter.Write(connection, transaction, actorUserAccountId, "TicketRefunded", "Ticket", row.TicketId.ToString(CultureInfo.InvariantCulture), detail, null, utcNow);
                SqlAuditWriter.Write(connection, transaction, actorUserAccountId, "WalletRefunded", "WalletTransaction", transactionCode, detail, null, utcNow);
                row.Balance = after;
                total += row.FareAmount;
            }
            return new PassengerRefundSummary { TicketCount = rows.Count, TotalAmount = total };
        }

        private static SqlCommand CreateCommand(SqlConnection connection, SqlTransaction transaction, string sql)
        {
            SqlCommand command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql; return command;
        }

        private static void AddString(SqlCommand command, string name, int size, string value)
        {
            command.Parameters.Add(name, SqlDbType.NVarChar, size).Value = value;
        }

        private static void AddMoney(SqlCommand command, string name, decimal value)
        {
            SqlParameter parameter = command.Parameters.Add(name, SqlDbType.Decimal); parameter.Precision = 12; parameter.Scale = 2; parameter.Value = value;
        }

        private static void AddUtc(SqlCommand command, string name, DateTime value)
        {
            SqlParameter parameter = command.Parameters.Add(name, SqlDbType.DateTime2); parameter.Scale = 0; parameter.Value = value;
        }

        private sealed class RefundRow
        {
            public long TicketId { get; set; }
            public string TicketCode { get; set; }
            public decimal FareAmount { get; set; }
            public long WalletId { get; set; }
            public decimal Balance { get; set; }
        }
    }

    internal sealed class PassengerRefundSummary
    {
        public int TicketCount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
