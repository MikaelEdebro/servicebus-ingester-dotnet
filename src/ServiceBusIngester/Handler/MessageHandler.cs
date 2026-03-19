using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ServiceBusIngester.Database;
using ServiceBusIngester.ServiceBus;

namespace ServiceBusIngester.Handler;

public sealed class MessageHandler(
    MessageRepository repository,
    MessageSender? sender,
    ILogger<MessageHandler> logger)
{
    private static readonly ActivitySource Tracer = new("servicebus-ingester/handler");

    public async Task HandleSingleAsync(ServiceBusReceivedMessage message, CancellationToken ct)
    {
        using var activity = Tracer.StartActivity("HandleMessage");
        activity?.SetTag("messaging.message_id", message.MessageId);

        var cloudEvent = Deserialize(message);

        activity?.SetTag("cloudevents.type", cloudEvent.Type);
        activity?.SetTag("cloudevents.source", cloudEvent.Source);
        activity?.SetTag("cloudevents.id", cloudEvent.Id);

        await repository.InsertAsync(message.MessageId, cloudEvent.Type, cloudEvent.Source, message.Body.ToString(), ct);

        if (sender is not null)
        {
            await sender.SendAsync(cloudEvent.Type, cloudEvent.Source, cloudEvent.Data, ct);
        }
    }

    public async Task HandleBatchAsync(
        ServiceBusReceiver receiver,
        IReadOnlyList<ServiceBusReceivedMessage> messages,
        CancellationToken ct)
    {
        using var activity = Tracer.StartActivity("HandleBatch");
        activity?.SetTag("messaging.batch_size", messages.Count);

        var valid = new List<(ServiceBusReceivedMessage Msg, MessageRow Row, OutboundEvent? Event)>(messages.Count);

        foreach (var msg in messages)
        {
            try
            {
                var cloudEvent = Deserialize(msg);
                var row = new MessageRow(msg.MessageId, cloudEvent.Type, cloudEvent.Source, msg.Body.ToString());
                var evt = sender is not null
                    ? new OutboundEvent(cloudEvent.Type, cloudEvent.Source, cloudEvent.Data)
                    : (OutboundEvent?)null;

                valid.Add((msg, row, evt));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse message {MessageId}, abandoning", msg.MessageId);
                await TryAbandonAsync(receiver, msg, ct);
            }
        }

        if (valid.Count == 0)
            return;

        var (conn, tx) = await repository.BeginTransactionAsync(ct);
        await using (conn)
        await using (tx)
        {
            await repository.InsertBatchAsync(conn, tx, valid.Select(v => v.Row).ToList(), ct);

            if (sender is not null)
            {
                var events = valid.Where(v => v.Event.HasValue).Select(v => v.Event!.Value).ToList();
                if (events.Count > 0)
                    await sender.SendBatchAsync(events, ct);
            }

            foreach (var (msg, _, _) in valid)
            {
                await receiver.CompleteMessageAsync(msg, ct);
            }

            await tx.CommitAsync(ct);
        }
    }

    private Models.CloudEvent Deserialize(ServiceBusReceivedMessage message)
    {
        Models.CloudEvent? cloudEvent;
        try
        {
            cloudEvent = JsonSerializer.Deserialize<Models.CloudEvent>(message.Body);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize CloudEvent for message {MessageId}", message.MessageId);
            throw;
        }

        if (cloudEvent is null)
            throw new InvalidOperationException($"Deserialized null CloudEvent for message {message.MessageId}");

        return cloudEvent;
    }

    private async Task TryAbandonAsync(ServiceBusReceiver receiver, ServiceBusReceivedMessage msg, CancellationToken ct)
    {
        try
        {
            await receiver.AbandonMessageAsync(msg, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to abandon message {MessageId}, lock will expire", msg.MessageId);
        }
    }
}
