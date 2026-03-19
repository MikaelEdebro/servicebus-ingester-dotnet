namespace ServiceBusIngester.Config;

public sealed class IngesterOptions
{
    // Service Bus
    public string? ServiceBusConnectionString { get; set; }
    public string? ServiceBusNamespace { get; set; }
    public string ServiceBusTopic { get; set; } = "";
    public string ServiceBusSubscription { get; set; } = "";
    public string? ServiceBusSendTopic { get; set; }
    public string? ServiceBusSendQueue { get; set; }

    // Consumer
    public int ConsumerCount { get; set; } = 10;
    public int BatchSize { get; set; } = 20;
    public int PrefetchCount { get; set; }

    // Database
    public string DbHost { get; set; } = "";
    public string DbUser { get; set; } = "";
    public string DbPassword { get; set; } = "";
    public int DbPort { get; set; } = 5432;
    public string DbDatabase { get; set; } = "";
    public string? DbSchema { get; set; }
    public string DbSslMode { get; set; } = "require";
    public bool DbSimpleProtocol { get; set; }
    public int DbMaxConns { get; set; } = 50;
    public int DbConnectionIdleTimeMinutes { get; set; } = 5;
    public int DbConnectionLifeTimeMinutes { get; set; } = 30;

    // Health
    public int HealthPort { get; set; } = 8080;

    public string SendDestination => ServiceBusSendTopic ?? ServiceBusSendQueue ?? "";
    public bool HasSendDestination => !string.IsNullOrEmpty(SendDestination);

    public string ConnectionString =>
        $"Host={DbHost};Port={DbPort};Database={DbDatabase};Username={DbUser};Password={DbPassword}" +
        $";SSL Mode={MapSslMode(DbSslMode)};Maximum Pool Size={DbMaxConns}" +
        $";Connection Idle Lifetime={DbConnectionIdleTimeMinutes * 60}" +
        $";Connection Lifetime={DbConnectionLifeTimeMinutes * 60}" +
        (DbSchema is not null ? $";Search Path={DbSchema}" : "") +
        (DbSimpleProtocol ? ";No Reset On Close=true" : "");

    private static string MapSslMode(string mode) => mode switch
    {
        "require" => "Require",
        "disable" => "Disable",
        "prefer" => "Prefer",
        "verify-ca" => "VerifyCA",
        "verify-full" => "VerifyFull",
        _ => "Require"
    };

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
            ServiceBusConnectionString = Env("SERVICEBUS_CONNECTION_STRING") is { Length: > 0 } s ? s : null,
            ServiceBusNamespace = Env("SERVICEBUS_NAMESPACE") is { Length: > 0 } ns ? ns : null,
            ServiceBusTopic = Env("SERVICEBUS_TOPIC"),
            ServiceBusSubscription = Env("SERVICEBUS_SUBSCRIPTION"),
            ServiceBusSendTopic = Env("SERVICEBUS_SEND_TOPIC") is { Length: > 0 } st ? st : null,
            ServiceBusSendQueue = Env("SERVICEBUS_SEND_QUEUE") is { Length: > 0 } sq ? sq : null,
            ConsumerCount = EnvInt("CONSUMER_COUNT", 10),
            BatchSize = EnvInt("BATCH_SIZE", 20),
            PrefetchCount = EnvInt("SB_PREFETCH_COUNT", 0),
            DbHost = Env("DB_HOST"),
            DbUser = Env("DB_USER"),
            DbPassword = Env("DB_PASSWORD"),
            DbPort = EnvInt("DB_PORT", 5432),
            DbDatabase = Env("DB_DATABASE"),
            DbSchema = Env("DB_SCHEMA") is { Length: > 0 } schema ? schema : null,
            DbSslMode = Env("DB_SSL_MODE", "require"),
            DbSimpleProtocol = EnvBool("DB_SIMPLE_PROTOCOL"),
            DbMaxConns = EnvInt("DB_MAX_CONNS", 50),
            DbConnectionIdleTimeMinutes = EnvInt("DB_CONNECTION_IDLE_TIME_MINUTES", 5),
            DbConnectionLifeTimeMinutes = EnvInt("DB_CONNECTION_LIFE_TIME_MINUTES", 30),
            HealthPort = EnvInt("HEALTH_PORT", 8080),
        };
    }
}
