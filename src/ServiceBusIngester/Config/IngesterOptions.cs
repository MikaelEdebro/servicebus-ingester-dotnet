namespace ServiceBusIngester.Config;

public enum ProcessingStrategy { Single, Batch }

public sealed class IngesterOptions
{
    // Health
    public int Port { get; set; } = 8080;

    // Database
    public string DbHost { get; set; } = "";
    public string DbUser { get; set; } = "";
    public string DbPassword { get; set; } = "";
    public int DbPort { get; set; } = 5432;
    public string DbDatabase { get; set; } = "";
    public string? DbSchema { get; set; }
    public string DbSslMode { get; set; } = "require";
    public bool DbSimpleProtocol { get; set; }
    public int DbMaxConnections { get; set; } = 50;
    public int DbConnectionIdleTimeMinutes { get; set; } = 5;
    public int DbConnectionLifeTimeMinutes { get; set; } = 30;

    public string DbConnectionString =>
        $"Host={DbHost};Port={DbPort};Database={DbDatabase};Username={DbUser};Password={DbPassword}" +
        $";SSL Mode={DbSslMode};Maximum Pool Size={DbMaxConnections}" +
        $";Connection Idle Lifetime={DbConnectionIdleTimeMinutes * 60}" +
        $";Connection Lifetime={DbConnectionLifeTimeMinutes * 60}" +
        (DbSchema is not null ? $";Search Path={DbSchema}" : "") +
        (DbSimpleProtocol ? ";No Reset On Close=true" : "");

    // Azure Identity
    public string Stage { get; set; } = "";
    public string? AzureClientId { get; set; }

    // Service Bus
    public string? SbConnectionString { get; set; }
    public string? SbNamespaceName { get; set; }
    
    // Consumer
    public int SbConsumerCount { get; set; } = 10;
    public int SbBatchSize { get; set; } = 20;
    public int SbPrefetchCount { get; set; }
    public string? SbUserUpdatedTopic { get; set; }
    public string? SbUserUpdatedSubcription { get; set; }
    public string? SbUserUpdatedStrategy { get; set; }
    public string? SbMachineLocationTopic { get; set; }
    public string? SbMachineLocationSubcription { get; set; }
    public string? SbMachineLocationStrategy { get; set; }

    public string? SbSendTopic { get; set; }


    public static IngesterOptions FromEnvironment()
    {
        static string Env(string name, string fallback = "") =>
            Environment.GetEnvironmentVariable(name) ?? fallback;

        static int EnvInt(string name, int fallback) =>
            int.TryParse(Environment.GetEnvironmentVariable(name), out var v) ? v : fallback;

        static bool EnvBool(string name, bool fallback = false) =>
            bool.TryParse(Environment.GetEnvironmentVariable(name), out var v) ? v : fallback;

        return new IngesterOptions
        {
            Port = EnvInt("PORT", 8080),
            DbHost = Env("DB_HOST"),
            DbUser = Env("DB_USER"),
            DbPassword = Env("DB_PASSWORD"),
            DbPort = EnvInt("DB_PORT", 5432),
            DbDatabase = Env("DB_DATABASE"),
            DbSchema = Env("DB_SCHEMA") is { Length: > 0 } schema ? schema : null,
            DbSslMode = Env("DB_SSL_MODE", "Require"),
            DbMaxConnections = EnvInt("DB_MAX_CONNECTIONS", 50),
            DbConnectionIdleTimeMinutes = EnvInt("DB_CONNECTION_IDLE_TIME_MINUTES", 5),
            DbConnectionLifeTimeMinutes = EnvInt("DB_CONNECTION_LIFE_TIME_MINUTES", 30),
            Stage = Env("STAGE"),
            AzureClientId = Env("AZURE_CLIENT_ID") is { Length: > 0 } cid ? cid : null,
            SbConnectionString = Env("SB_CONNECTION_STRING") is { Length: > 0 } s ? s : null,
            SbNamespaceName = Env("SB_NAMESPACE_NAME") is { Length: > 0 } ns ? ns : null,
            SbConsumerCount = EnvInt("SB_CONSUMER_COUNT", 10),
            SbBatchSize = EnvInt("SB_BATCH_SIZE", 20),
            SbPrefetchCount = EnvInt("SB_PREFETCH_COUNT", 0),
            SbUserUpdatedTopic = Env("SB_USER_UPDATED_TOPIC") is { Length: > 0 } s1 ? s1 : null,
            SbUserUpdatedSubcription = Env("SB_USER_UPDATED_SUBSCRIPTION") is { Length: > 0 } s2 ? s2 : null,
            SbUserUpdatedStrategy = Env("SB_USER_UPDATED_STRATEGY", "Single"),
            SbMachineLocationTopic = Env("SB_MACHINE_LOCATION_TOPIC") is { Length: > 0 } s3 ? s3 : null,
            SbMachineLocationSubcription = Env("SB_MACHINE_LOCATION_SUBSCRIPTION") is { Length: > 0 } s4 ? s4 : null,
            SbMachineLocationStrategy = Env("SB_MACHINE_LOCATION_STRATEGY", "Single")
        };
    }
}
