using Azure.Messaging.ServiceBus;
using ServiceBusIngester.Config;
using ServiceBusIngester.Handlers;

namespace ServiceBusIngester.ServiceBus;

public sealed class MessageConsumer(
    ServiceBusClient client,
    IngesterOptions options,
    EventHandlerDispatcher dispatcher,
    ILogger<MessageConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var groups = dispatcher.Handlers
            .GroupBy(h => (h.Topic, h.Subscription))
            .ToList();

        var tasks = new List<Task>();

        foreach (var group in groups)
        {
            var (topic, subscription) = group.Key;
            var strategy = group.First().Strategy;

            logger.LogInformation(
                "Starting {ConsumerCount} receivers (batch size {BatchSize}, prefetch {PrefetchCount}, strategy {Strategy}) on {Topic}/{Subscription}",
                options.ConsumerCount, options.BatchSize, options.PrefetchCount, strategy, topic, subscription);

            for (var i = 0; i < options.ConsumerCount; i++)
            {
                var receiverIndex = i;
                tasks.Add(Task.Run(
                    () => ReceiveLoop(receiverIndex, topic, subscription, strategy, stoppingToken),
                    stoppingToken));
            }
        }

        await Task.WhenAll(tasks);
    }

    private async Task ReceiveLoop(
        int index, string topic, string subscription, ProcessingStrategy strategy, CancellationToken ct)
    {
        var receiverOptions = new ServiceBusReceiverOptions { PrefetchCount = options.PrefetchCount };
        var receiver = client.CreateReceiver(topic, subscription, receiverOptions);

        logger.LogInformation("Receiver {Index} started on {Topic}/{Subscription}", index, topic, subscription);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                IReadOnlyList<ServiceBusReceivedMessage> messages;
                try
                {
                    messages = await receiver.ReceiveMessagesAsync(options.BatchSize, cancellationToken: ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Receiver {Index} on {Topic}/{Subscription}: error receiving messages",
                        index, topic, subscription);
                    continue;
                }

                try
                {
                    await dispatcher.DispatchAsync(receiver, messages, strategy, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Receiver {Index} on {Topic}/{Subscription}: error dispatching {Count} messages, letting locks expire",
                        index, topic, subscription, messages.Count);
                }
            }
        }
        finally
        {
            await receiver.DisposeAsync();
            logger.LogInformation("Receiver {Index} on {Topic}/{Subscription} stopped", index, topic, subscription);
        }
    }
}
