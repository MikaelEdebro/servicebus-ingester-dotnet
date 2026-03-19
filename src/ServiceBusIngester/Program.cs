using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ServiceBusIngester.Config;
using ServiceBusIngester.Database;
using ServiceBusIngester.Handler;
using ServiceBusIngester.ServiceBus;

var options = IngesterOptions.FromEnvironment();

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls($"http://+:{options.HealthPort}");

builder.Services.AddSingleton(options);

var dataSource = NpgsqlDataSource.Create(options.ConnectionString);
builder.Services.AddSingleton(dataSource);
builder.Services.AddSingleton<MessageRepository>();

var sbClient = ServiceBusClientFactory.Create(options);
builder.Services.AddSingleton(sbClient);

if (options.HasSendDestination)
{
    var sender = sbClient.CreateSender(options.SendDestination);
    builder.Services.AddSingleton(new MessageSender(sender));
}
else
{
    builder.Services.AddSingleton<MessageSender>(sp => null!);
}

builder.Services.AddSingleton<MessageHandler>();
builder.Services.AddHostedService<MessageConsumer>();

builder.Services.AddHealthChecks()
    .AddNpgSql(options.ConnectionString, name: "postgres");

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
