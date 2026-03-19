using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ServiceBusIngester.Database;
using ServiceBusIngester.Models;
using ServiceBusIngester.ServiceBus;

namespace ServiceBusIngester.Handler;

public sealed class MessageHandler(
    MessageRepository repository,
    MessageSender? sender,
    ILogger<MessageHandler> logger)
{
    private static readonly ActivitySource Tracer = new("servicebus-ingester/handler");

    public async Task HandleAsync(ServiceBusReceivedMessage message, CancellationToken ct)
    {
        using var activity = Tracer.StartActivity("HandleMessage");
        activity?.SetTag("messaging.message_id", message.MessageId);

        CloudEvent? cloudEvent;
        try
        {
            cloudEvent = JsonSerializer.Deserialize<CloudEvent>(message.Body);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize CloudEvent for message {MessageId}", message.MessageId);
            throw;
        }

        if (cloudEvent is null)
            throw new InvalidOperationException($"Deserialized null CloudEvent for message {message.MessageId}");

        activity?.SetTag("cloudevents.type", cloudEvent.Type);
        activity?.SetTag("cloudevents.source", cloudEvent.Source);
        activity?.SetTag("cloudevents.id", cloudEvent.Id);

        var rawBody = message.Body.ToString();

        await repository.InsertMessageAsync(message.MessageId, cloudEvent.Type, cloudEvent.Source, rawBody, ct);

        if (sender is not null)
        {
            await sender.SendCloudEventAsync(cloudEvent.Type, cloudEvent.Source, cloudEvent.Data, ct);
        }
    }
}
