using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SM.ChatRouter.Infrastructure.Ai.OpenAI.Models;

/// <summary>
/// Cuerpo de <c>POST /v1/responses</c>. Los campos nulos no se envían; los no modelados
/// se pueden añadir en <see cref="AdditionalProperties"/> y se serializan en la raíz del JSON.
/// </summary>
public sealed record ResponseRequest
{
    /// <summary>Si es nulo se usa <c>OpenAI:Model</c>.</summary>
    public string? Model { get; init; }

    public IList<ResponseInputItem> Input { get; init; } = [];

    public string? Instructions { get; init; }

    public ReasoningOptions? Reasoning { get; init; }

    public ResponseTextOptions? Text { get; init; }

    public IList<ResponseTool>? Tools { get; init; }

    /// <summary><c>"auto"</c>, <c>"none"</c>, <c>"required"</c> o un objeto, p. ej. <see cref="ToolChoice.Function"/>.</summary>
    public JsonNode? ToolChoice { get; init; }

    public bool? ParallelToolCalls { get; init; }

    public int? MaxOutputTokens { get; init; }

    public int? MaxToolCalls { get; init; }

    public double? Temperature { get; init; }

    public double? TopP { get; init; }

    /// <summary>Lo gestiona el cliente: <c>CreateAsync</c> envía false y <c>StreamAsync</c> true.</summary>
    public bool? Stream { get; init; }

    public bool? Store { get; init; }

    public bool? Background { get; init; }

    public string? PreviousResponseId { get; init; }

    /// <summary>p. ej. <c>reasoning.encrypted_content</c>, <c>web_search_call.action.sources</c>…</summary>
    public IList<string>? Include { get; init; }

    public IDictionary<string, string>? Metadata { get; init; }

    /// <summary><c>auto</c> o <c>disabled</c>.</summary>
    public string? Truncation { get; init; }

    public string? ServiceTier { get; init; }

    public string? SafetyIdentifier { get; init; }

    public string? PromptCacheKey { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed record ReasoningOptions
{
    public string? Effort { get; init; }

    public string? Summary { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed record ResponseTextOptions
{
    public ResponseTextFormat? Format { get; init; }

    public string? Verbosity { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public static class ReasoningEffort
{
    public const string None = "none";
    public const string Minimal = "minimal";
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
}

public static class ToolChoice
{
    public static JsonNode Auto => JsonValue.Create("auto");

    public static JsonNode None => JsonValue.Create("none");

    public static JsonNode Required => JsonValue.Create("required");

    public static JsonNode Function(string name) => new JsonObject { ["type"] = "function", ["name"] = name };
}
