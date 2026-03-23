using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using ServiceBusIngester.Config;

namespace ServiceBusIngester.ServiceBus;

public static class ServiceBusClientFactory
{
    public static ServiceBusClient Create(IngesterOptions options)
    {
        if (!string.IsNullOrEmpty(options.SbConnectionString))
            return new ServiceBusClient(options.SbConnectionString);

        if (!string.IsNullOrEmpty(options.SbNamespaceName))
            return new ServiceBusClient($"{options.SbNamespaceName}.servicebus.windows.net", CreateCredential(options));

        throw new InvalidOperationException(
            "Either SB_CONNECTION_STRING or SB_NAMESPACE_NAME must be set");
    }

    private static TokenCredential CreateCredential(IngesterOptions options)
    {
        if (string.Equals(options.Stage, "localhost", StringComparison.OrdinalIgnoreCase))
            return new AzureCliCredential();

        return new ChainedTokenCredential(
            new ManagedIdentityCredential(options.AzureClientId),
            new ManagedIdentityCredential(),
            new AzureCliCredential());
    }
}
