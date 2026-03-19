using System.Diagnostics;
using Npgsql;
using NpgsqlTypes;

namespace ServiceBusIngester.Database;

public sealed class MessageRepository(NpgsqlDataSource dataSource)
{
    private static readonly ActivitySource Tracer = new("servicebus-ingester/db");

    private const string InsertSql =
        "INSERT INTO messages (message_id, event_type, source, body) VALUES ($1, $2, $3, $4::jsonb)";

    public async Task InsertAsync(string messageId, string eventType, string source, string body, CancellationToken ct)
    {
        using var activity = Tracer.StartActivity("db.InsertMessage");

        await using var cmd = dataSource.CreateCommand(InsertSql);
        cmd.Parameters.AddWithValue(messageId);
        cmd.Parameters.AddWithValue(eventType);
        cmd.Parameters.AddWithValue(source);
        cmd.Parameters.AddWithValue(body);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task InsertBatchAsync(NpgsqlConnection conn, NpgsqlTransaction tx, IReadOnlyList<MessageRow> rows, CancellationToken ct)
    {
        using var activity = Tracer.StartActivity("db.InsertBatch");
        activity?.SetTag("db.batch_size", rows.Count);

        await using var writer = await conn.BeginBinaryImportAsync(
            "COPY messages (message_id, event_type, source, body) FROM STDIN (FORMAT BINARY)", ct);

        foreach (var row in rows)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(row.MessageId, NpgsqlDbType.Text, ct);
            await writer.WriteAsync(row.EventType, NpgsqlDbType.Text, ct);
            await writer.WriteAsync(row.Source, NpgsqlDbType.Text, ct);
            await writer.WriteAsync(row.Body, NpgsqlDbType.Jsonb, ct);
        }

        await writer.CompleteAsync(ct);
    }

    public async Task<(NpgsqlConnection conn, NpgsqlTransaction tx)> BeginTransactionAsync(CancellationToken ct)
    {
        var conn = await dataSource.OpenConnectionAsync(ct);
        var tx = await conn.BeginTransactionAsync(ct);
        return (conn, tx);
    }
}

public readonly record struct MessageRow(string MessageId, string EventType, string Source, string Body);
