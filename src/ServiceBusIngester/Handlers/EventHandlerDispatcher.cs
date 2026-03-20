using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ServiceBusIngester.Config;
using ServiceBusIngester.Models;

namespace ServiceBusIngester.Handlers;

public sealed class EventHandlerDispatcher
{
    private readonly Dictionary<string, IEventHandler> _handlers;
    private readonly ILogger<EventHandlerDispatcher> _logger;

    public EventHandlerDispatcher(IEnumerable<IEventHandler> handlers, ILogger<EventHandlerDispatcher> logger)
    {
        _logger = logger;
        _handlers = new(StringComparer.OrdinalIgnoreCase);

        foreach (var handler in handlers)
            _handlers[handler.EventType] = handler;
    }

    public IEnumerable<IEventHandler> Handlers => _handlers.Values;

    public async Task DispatchSingleAsync(ServiceBusReceivedMessage message, CancellationToken ct)
    {
        var cloudEvent = Deserialize(message);

        _logger.LogInformation("Received message {MessageId} type={Type} source={Source} strategy=single",
            message.MessageId, cloudEvent.Type, cloudEvent.Source);

        var handler = ResolveHandler(cloudEvent.Type);
        await handler.HandleSingleAsync(message.MessageId, cloudEvent, message.Body.ToString(), ct);
    }

    public async Task DispatchBatchAsync(
        ServiceBusReceiver receiver,
        IReadOnlyList<ServiceBusReceivedMessage> messages,
        CancellationToken ct)
    {
        var parsed = new List<(ServiceBusReceivedMessage Msg, CloudEvent Event)>(messages.Count);

        foreach (var msg in messages)
        {
            try
            {
                var cloudEvent = Deserialize(msg);
                _logger.LogInformation("Received message {MessageId} type={Type} source={Source} strategy=batch",
                    msg.MessageId, cloudEvent.Type, cloudEvent.Source);
                parsed.Add((msg, cloudEvent));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse message {MessageId}, abandoning", msg.MessageId);
                await TryAbandonAsync(receiver, msg, ct);
            }
        }

        if (parsed.Count == 0)
            return;

        var groups = parsed.GroupBy(p => p.Event.Type, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var handler = ResolveHandler(group.Key);
            await handler.HandleBatchAsync(receiver, group.ToList(), ct);
        }
    }

    public async Task DispatchAsync(
        ServiceBusReceiver receiver,
        IReadOnlyList<ServiceBusReceivedMessage> messages,
        ProcessingStrategy strategy,
        CancellationToken ct)
    {
        if (strategy == ProcessingStrategy.Batch)
            await DispatchBatchAsync(receiver, messages, ct);
        else
            await DispatchSingleAsync(receiver, messages, ct);
    }

    private async Task DispatchSingleAsync(
        ServiceBusReceiver receiver,
        IReadOnlyList<ServiceBusReceivedMessage> messages,
        CancellationToken ct)
    {
        foreach (var msg in messages)
        {
            try
            {
                await DispatchSingleAsync(msg, ct);
                await receiver.CompleteMessageAsync(msg, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message {MessageId}, letting lock expire", msg.MessageId);
            }
        }
    }

    private IEventHandler ResolveHandler(string eventType)
    {
        if (_handlers.TryGetValue(eventType, out var handler))
            return handler;

        throw new InvalidOperationException($"No handler registered for event type '{eventType}'");
    }

    private CloudEvent Deserialize(ServiceBusReceivedMessage message)
    {
        CloudEvent? cloudEvent;
        try
        {
            cloudEvent = JsonSerializer.Deserialize<CloudEvent>(message.Body);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize CloudEvent for message {MessageId}", message.MessageId);
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
            _logger.LogWarning(ex, "Failed to abandon message {MessageId}, lock will expire", msg.MessageId);
        }
    }
}
