using System;
using System.Data;
using System.Data.SqlClient;

namespace ForteMove.Data.Internal
{
    internal static class SqlAuditWriter
    {
        private const string InsertAuditSql = @"
INSERT dbo.AuditEntries
(
    ActorUserAccountId,
    EventType,
    EntityType,
    EntityId,
    Detail,
    ClientIpAddress,
    OccurredUtc
)
VALUES
(
    @ActorUserAccountId,
    @EventType,
    @EntityType,
    @EntityId,
    @Detail,
    @ClientIpAddress,
    @OccurredUtc
);";

        public static void Write(
            SqlConnection connection,
            SqlTransaction transaction,
            long? actorUserAccountId,
            string eventType,
            string entityType,
            string entityId,
            string detail,
            string clientIpAddress,
            DateTime occurredUtc)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.Text;
                command.CommandText = InsertAuditSql;

                SqlParameter actorParameter = command.Parameters.Add(
                    "@ActorUserAccountId",
                    SqlDbType.BigInt);
                actorParameter.Value = actorUserAccountId.HasValue
                    ? (object)actorUserAccountId.Value
                    : DBNull.Value;

                SqlParameter eventTypeParameter = command.Parameters.Add(
                    "@EventType",
                    SqlDbType.NVarChar,
                    100);
                eventTypeParameter.Value = eventType;

                SqlParameter entityTypeParameter = command.Parameters.Add(
                    "@EntityType",
                    SqlDbType.NVarChar,
                    100);
                entityTypeParameter.Value = entityType;

                SqlParameter entityIdParameter = command.Parameters.Add(
                    "@EntityId",
                    SqlDbType.NVarChar,
                    100);
                entityIdParameter.Value = (object)entityId ?? DBNull.Value;

                SqlParameter detailParameter = command.Parameters.Add(
                    "@Detail",
                    SqlDbType.NVarChar,
                    1000);
                detailParameter.Value = (object)detail ?? DBNull.Value;

                SqlParameter clientIpParameter = command.Parameters.Add(
                    "@ClientIpAddress",
                    SqlDbType.NVarChar,
                    45);
                clientIpParameter.Value = (object)clientIpAddress ?? DBNull.Value;

                SqlParameter occurredParameter = command.Parameters.Add(
                    "@OccurredUtc",
                    SqlDbType.DateTime2);
                occurredParameter.Scale = 0;
                occurredParameter.Value = occurredUtc;

                command.ExecuteNonQuery();
            }
        }
    }
}
