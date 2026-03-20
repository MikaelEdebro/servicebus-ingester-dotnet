using Azure.Messaging.ServiceBus;
using ServiceBusIngester.Config;
using ServiceBusIngester.Models;

namespace ServiceBusIngester.Handlers;

public interface IEventHandler
{
    string EventType { get; }
    string Topic { get; }
    string Subscription { get; }
    ProcessingStrategy Strategy { get; }

    Task HandleSingleAsync(string messageId, CloudEvent cloudEvent, string rawBody, CancellationToken ct);

    Task HandleBatchAsync(
        ServiceBusReceiver receiver,
        IReadOnlyList<(ServiceBusReceivedMessage Msg, CloudEvent Event)> items,
        CancellationToken ct);
}
