using System.Diagnostics;
using Npgsql;

namespace ServiceBusIngester.Repositories;

public sealed class UserAuditLogRepository(NpgsqlDataSource dataSource)
{
    private static readonly ActivitySource Tracer = new("servicebus-ingester/db");

    private const string InsertSql =
        "INSERT INTO user_audit_logs (user_id, event_type, source, payload) VALUES ($1, $2, $3, $4::jsonb)";

    public async Task InsertAsync(string userId, string eventType, string source, string payload, CancellationToken ct)
    {
        using var activity = Tracer.StartActivity("db.InsertUserAuditLog");

        await using var cmd = dataSource.CreateCommand(InsertSql);
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(eventType);
        cmd.Parameters.AddWithValue(source);
        cmd.Parameters.AddWithValue(payload);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
