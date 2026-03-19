using System.Text.Json.Serialization;

namespace ServiceBusIngester.Models;

public sealed record CloudEvent
{
    [JsonPropertyName("specversion")]
    public string SpecVersion { get; init; } = "1.0";

    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("source")]
    public string Source { get; init; } = "";

    [JsonPropertyName("data")]
    public object? Data { get; init; }
}
