using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ServiceBusIngester.Config;
using ServiceBusIngester.Repositories;
using ServiceBusIngester.Handlers;
using ServiceBusIngester.Handlers.MachineLocationEvent;
using ServiceBusIngester.Handlers.UserUpdatedEvent;
using ServiceBusIngester.ServiceBus;

var options = IngesterOptions.FromEnvironment();

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls($"http://+:{options.Port}");

builder.Logging.AddJsonConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

builder.Services.AddSingleton(options);

var dataSource = NpgsqlDataSource.Create(options.DbConnectionString);
builder.Services.AddSingleton(dataSource);
builder.Services.AddSingleton<MessageRepository>();
builder.Services.AddSingleton<UserAuditLogRepository>();

var sbClient = ServiceBusClientFactory.Create(options);
builder.Services.AddSingleton(sbClient);

if (options.SbSendTopic is not null)
{
    var sender = sbClient.CreateSender(options.SbSendTopic);
    builder.Services.AddSingleton(new MessageSender(sender));
}
else
{
    builder.Services.AddSingleton<MessageSender>(sp => null!);
}

builder.Services.AddSingleton<IEventHandler, MachineLocationEventHandler>();
builder.Services.AddSingleton<IEventHandler, UserUpdatedEventHandler>();
builder.Services.AddSingleton<EventHandlerDispatcher>();
builder.Services.AddHostedService<MessageConsumer>();

builder.Services.AddHealthChecks()
    .AddNpgSql(options.DbConnectionString, name: "postgres");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("servicebus-ingester-dotnet"))
    .WithTracing(t => t
        .AddSource("servicebus-ingester/handler")
        .AddSource("servicebus-ingester/db")
        .AddSource("servicebus-ingester/sender")
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());

var app = builder.Build();

app.MapHealthChecks("/healthz", new() { Predicate = _ => false });
app.MapHealthChecks("/readyz");

app.Run();
