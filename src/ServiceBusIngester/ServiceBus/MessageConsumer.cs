using Azure.Messaging.ServiceBus;
using ServiceBusIngester.Config;
using ServiceBusIngester.Handler;

namespace ServiceBusIngester.ServiceBus;

public sealed class MessageConsumer(
    ServiceBusClient client,
    IngesterOptions options,
    MessageHandler handler,
    ILogger<MessageConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Starting {ConsumerCount} receivers (batch size {BatchSize}, prefetch {PrefetchCount}, strategy {Strategy}) on {Topic}/{Subscription}",
            options.ConsumerCount, options.BatchSize, options.PrefetchCount, options.Strategy,
            options.ServiceBusTopic, options.ServiceBusSubscription);

        var tasks = new Task[options.ConsumerCount];
        for (var i = 0; i < options.ConsumerCount; i++)
        {
            var receiverIndex = i;
            tasks[i] = Task.Run(() => ReceiveLoop(receiverIndex, stoppingToken), stoppingToken);
        }

        await Task.WhenAll(tasks);
    }

    private async Task ReceiveLoop(int index, CancellationToken ct)
    {
        var receiverOptions = new ServiceBusReceiverOptions
        {
            PrefetchCount = options.PrefetchCount
        };
        var receiver = client.CreateReceiver(options.ServiceBusTopic, options.ServiceBusSubscription, receiverOptions);
        logger.LogInformation("Receiver {Index} started", index);

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
                    logger.LogError(ex, "Receiver {Index}: error receiving messages", index);
                    continue;
                }

                if (options.Strategy == ProcessingStrategy.Batch)
                {
                    await ProcessBatch(receiver, messages, index, ct);
                }
                else
                {
                    await ProcessSingle(receiver, messages, index, ct);
                }
            }
        }
        finally
        {
            await receiver.DisposeAsync();
            logger.LogInformation("Receiver {Index} stopped", index);
        }
    }

    private async Task ProcessBatch(ServiceBusReceiver receiver, IReadOnlyList<ServiceBusReceivedMessage> messages, int index, CancellationToken ct)
    {
        try
        {
            await handler.HandleBatchAsync(receiver, messages, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Receiver {Index}: error handling batch of {Count} messages, letting locks expire",
                index, messages.Count);
        }
    }

    private async Task ProcessSingle(ServiceBusReceiver receiver, IReadOnlyList<ServiceBusReceivedMessage> messages, int index, CancellationToken ct)
    {
        foreach (var msg in messages)
        {
            try
            {
                await handler.HandleSingleAsync(msg, ct);
                await receiver.CompleteMessageAsync(msg, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Receiver {Index}: error handling message {MessageId}, letting lock expire",
                    index, msg.MessageId);
            }
        }
    }
}
