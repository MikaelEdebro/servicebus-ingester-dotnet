using Azure.Messaging.ServiceBus;
using ServiceBusIngester.Config;
using ServiceBusIngester.Models;

namespace ServiceBusIngester.Handlers.UserUpdatedEvent;

public sealed class UserUpdatedEventHandler(
    ILogger<UserUpdatedEventHandler> logger) : IEventHandler
{
    public string EventType => "UserUpdatedEvent";
    public string Topic { get; } = Environment.GetEnvironmentVariable("SB_USER_UPDATED_TOPIC") ?? "";
    public string Subscription { get; } = Environment.GetEnvironmentVariable("SB_USER_UPDATED_SUBSCRIPTION") ?? "";
    public ProcessingStrategy Strategy { get; } = Enum.TryParse<ProcessingStrategy>(
        Environment.GetEnvironmentVariable("SB_USER_UPDATED_STRATEGY"), ignoreCase: true, out var s) ? s : ProcessingStrategy.Single;

    public Task HandleSingleAsync(string messageId, CloudEvent cloudEvent, string rawBody, CancellationToken ct)
    {
        logger.LogInformation("User {UserId} updated", cloudEvent.Id);
        return Task.CompletedTask;
    }

    public async Task HandleBatchAsync(
        ServiceBusReceiver receiver,
        IReadOnlyList<(ServiceBusReceivedMessage Msg, CloudEvent Event)> items,
        CancellationToken ct)
    {
        foreach (var (msg, cloudEvent) in items)
        {
            logger.LogInformation("User {UserId} updated", cloudEvent.Id);
            await receiver.CompleteMessageAsync(msg, ct);
        }
    }
}
