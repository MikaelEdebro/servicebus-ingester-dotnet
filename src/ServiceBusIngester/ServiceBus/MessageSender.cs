using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ServiceBusIngester.Models;

namespace ServiceBusIngester.ServiceBus;

public sealed class MessageSender(ServiceBusSender sender) : IAsyncDisposable
{
    private static readonly ActivitySource Tracer = new("servicebus-ingester/sender");

    public async Task SendCloudEventAsync(string eventType, string source, object? data, CancellationToken ct)
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

    public async ValueTask DisposeAsync() => await sender.DisposeAsync();
}
