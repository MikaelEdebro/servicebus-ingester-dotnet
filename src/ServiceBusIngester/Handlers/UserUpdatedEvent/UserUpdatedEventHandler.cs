using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ServiceBusIngester.Config;
using ServiceBusIngester.Models;
using ServiceBusIngester.Repositories;

namespace ServiceBusIngester.Handlers.UserUpdatedEvent;

public sealed class UserUpdatedEventHandler(
    ILogger<UserUpdatedEventHandler> logger,
    UserAuditLogRepository auditLogRepository) : IEventHandler
{
    public string EventType => "UserUpdatedEvent";
    public string Topic { get; } = Environment.GetEnvironmentVariable("SB_USER_UPDATED_TOPIC") ?? "";
    public string Subscription { get; } = Environment.GetEnvironmentVariable("SB_USER_UPDATED_SUBSCRIPTION") ?? "";
    public ProcessingStrategy Strategy { get; } = Enum.TryParse<ProcessingStrategy>(
        Environment.GetEnvironmentVariable("SB_USER_UPDATED_STRATEGY"), ignoreCase: true, out var s) ? s : ProcessingStrategy.Single;

    public async Task HandleSingleAsync(string messageId, CloudEvent cloudEvent, string rawBody, CancellationToken ct)
    {
        logger.LogInformation("User {UserId} updated", cloudEvent.Id);

        await auditLogRepository.InsertAsync(
            cloudEvent.Id,
            cloudEvent.Type,
            cloudEvent.Source,
            JsonSerializer.Serialize(cloudEvent.Data),
            ct);
    }

    public async Task HandleBatchAsync(
        ServiceBusReceiver receiver,
        IReadOnlyList<(ServiceBusReceivedMessage Msg, CloudEvent Event)> items,
        CancellationToken ct)
    {
        foreach (var (msg, cloudEvent) in items)
        {
            logger.LogInformation("User {UserId} updated", cloudEvent.Id);

            await auditLogRepository.InsertAsync(
                cloudEvent.Id,
                cloudEvent.Type,
                cloudEvent.Source,
                JsonSerializer.Serialize(cloudEvent.Data),
                ct);

            await receiver.CompleteMessageAsync(msg, ct);
        }
    }
}
