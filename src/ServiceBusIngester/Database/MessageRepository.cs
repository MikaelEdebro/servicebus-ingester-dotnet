using System.Diagnostics;
using Npgsql;

namespace ServiceBusIngester.Database;

public sealed class MessageRepository(NpgsqlDataSource dataSource)
{
    private static readonly ActivitySource Tracer = new("servicebus-ingester/db");

    private const string InsertSql =
        "INSERT INTO messages (message_id, event_type, source, body) VALUES ($1, $2, $3, $4::jsonb)";

    public async Task InsertMessageAsync(string messageId, string eventType, string source, string body, CancellationToken ct)
    {
        using var activity = Tracer.StartActivity("db.InsertMessage");

        await using var cmd = dataSource.CreateCommand(InsertSql);
        cmd.Parameters.AddWithValue(messageId);
        cmd.Parameters.AddWithValue(eventType);
        cmd.Parameters.AddWithValue(source);
        cmd.Parameters.AddWithValue(body);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
