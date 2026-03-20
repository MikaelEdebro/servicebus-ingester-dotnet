using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using ServiceBusIngester.Config;
using ServiceBusIngester.Models;
using ServiceBusIngester.Repositories;
using ServiceBusIngester.ServiceBus;

namespace ServiceBusIngester.Handlers.MachineLocationEvent;

public sealed class MachineLocationEventHandler(
    MessageRepository repository,
    MessageSender? sender) : IEventHandler
{
    private static readonly ActivitySource Tracer = new("servicebus-ingester/handler");

    public string EventType => "MachineLocationEvent";
    public string Topic { get; } = Environment.GetEnvironmentVariable("SB_MACHINE_LOCATION_TOPIC") ?? "";
    public string Subscription { get; } = Environment.GetEnvironmentVariable("SB_MACHINE_LOCATION_SUBSCRIPTION") ?? "";
    public ProcessingStrategy Strategy { get; } = Enum.TryParse<ProcessingStrategy>(
        Environment.GetEnvironmentVariable("SB_MACHINE_LOCATION_STRATEGY"), ignoreCase: true, out var s) ? s : ProcessingStrategy.Single;

    public async Task HandleSingleAsync(string messageId, CloudEvent cloudEvent, string rawBody, CancellationToken ct)
    {
        using var activity = Tracer.StartActivity("HandleMachineLocationEvent");
        activity?.SetTag("messaging.message_id", messageId);

        await repository.InsertAsync(messageId, cloudEvent.Type, cloudEvent.Source, rawBody, ct);

        if (sender is not null)
            await sender.SendAsync(cloudEvent.Type, cloudEvent.Source, cloudEvent.Data, ct);
    }

    public async Task HandleBatchAsync(
        ServiceBusReceiver receiver,
        IReadOnlyList<(ServiceBusReceivedMessage Msg, CloudEvent Event)> items,
        CancellationToken ct)
    {
        using var activity = Tracer.StartActivity("HandleMachineLocationEventBatch");
        activity?.SetTag("messaging.batch_size", items.Count);

        var rows = items.Select(i =>
            new MessageRow(i.Msg.MessageId, i.Event.Type, i.Event.Source, i.Msg.Body.ToString())).ToList();

        var (conn, tx) = await repository.BeginTransactionAsync(ct);
        await using (conn)
        await using (tx)
        {
            await repository.InsertBatchAsync(conn, tx, rows, ct);

            if (sender is not null)
            {
                var events = items.Select(i =>
                    new OutboundEvent(i.Event.Type, i.Event.Source, i.Event.Data)).ToList();
                await sender.SendBatchAsync(events, ct);
            }

            foreach (var (msg, _) in items)
                await receiver.CompleteMessageAsync(msg, ct);

            await tx.CommitAsync(ct);
        }
    }
}
