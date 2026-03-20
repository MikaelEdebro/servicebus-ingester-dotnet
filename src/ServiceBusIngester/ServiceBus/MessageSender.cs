using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ServiceBusIngester.Models;

namespace ServiceBusIngester.ServiceBus;

public sealed class MessageSender(ServiceBusSender sender) : IAsyncDisposable
{
    private static readonly ActivitySource Tracer = new("servicebus-ingester/sender");

    public async Task SendAsync(string eventType, string source, object? data, CancellationToken ct)
    {
        using var activity = Tracer.StartActivity("servicebus.SendCloudEvent");

        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Id = Guid.NewGuid().ToString(),
            Type = eventType,
            Source = source,
            Data = data
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(cloudEvent);
        var message = new ServiceBusMessage(body) { ContentType = "application/cloudevents+json" };

        await sender.SendMessageAsync(message, ct);
    }

    public async Task SendBatchAsync(IReadOnlyList<OutboundEvent> events, CancellationToken ct)
    {
        using var activity = Tracer.StartActivity("servicebus.SendBatch");
        activity?.SetTag("messaging.SB_BATCH_SIZE", events.Count);

        using var batch = await sender.CreateMessageBatchAsync(ct);

        foreach (var evt in events)
        {
            var cloudEvent = new CloudEvent
            {
                SpecVersion = "1.0",
                Id = Guid.NewGuid().ToString(),
                Type = evt.EventType,
                Source = evt.Source,
                Data = evt.Data
            };

            var body = JsonSerializer.SerializeToUtf8Bytes(cloudEvent);
            var message = new ServiceBusMessage(body) { ContentType = "application/cloudevents+json" };

            if (!batch.TryAddMessage(message))
            {
                await sender.SendMessagesAsync(batch, ct);
                using var overflowBatch = await sender.CreateMessageBatchAsync(ct);
                if (!overflowBatch.TryAddMessage(message))
                    throw new InvalidOperationException("Single message too large for Service Bus batch");
                await sender.SendMessagesAsync(overflowBatch, ct);
            }
        }

        if (batch.Count > 0)
            await sender.SendMessagesAsync(batch, ct);
    }

    public async ValueTask DisposeAsync() => await sender.DisposeAsync();
}

public readonly record struct OutboundEvent(string EventType, string Source, object? Data);
