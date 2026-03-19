using Azure.Identity;
using Azure.Messaging.ServiceBus;
using ServiceBusIngester.Config;

namespace ServiceBusIngester.ServiceBus;

public static class ServiceBusClientFactory
{
    public static ServiceBusClient Create(IngesterOptions options)
    {
        if (!string.IsNullOrEmpty(options.ServiceBusConnectionString))
            return new ServiceBusClient(options.ServiceBusConnectionString);

        if (!string.IsNullOrEmpty(options.ServiceBusNamespace))
            return new ServiceBusClient(options.ServiceBusNamespace, new DefaultAzureCredential());

        throw new InvalidOperationException(
            "Either SERVICEBUS_CONNECTION_STRING or SERVICEBUS_NAMESPACE must be set");
    }
}
