using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SM.ChatRouter.Infrastructure.Ai.OpenAI.Models;

/// <summary>Elemento del array <c>tools</c>.</summary>
public abstract record ResponseTool
{
    [JsonPropertyOrder(-1)]
    public abstract string Type { get; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed record FunctionTool : ResponseTool
{
    public override string Type => "function";

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>JSON Schema de los argumentos.</summary>
    public required JsonNode Parameters { get; init; }

    public bool? Strict { get; init; }
}

public sealed record WebSearchTool : ResponseTool
{
    public override string Type => "web_search";

    /// <summary><c>low</c>, <c>medium</c> o <c>high</c>.</summary>
    public string? SearchContextSize { get; init; }

    public JsonNode? UserLocation { get; init; }

    public JsonNode? Filters { get; init; }
}

public sealed record FileSearchTool : ResponseTool
{
    public override string Type => "file_search";

    public required IList<string> VectorStoreIds { get; init; }

    public int? MaxNumResults { get; init; }

    public JsonNode? Filters { get; init; }
}

public sealed record CodeInterpreterTool : ResponseTool
{
    public override string Type => "code_interpreter";

    /// <summary>p. ej. <c>{"type":"auto"}</c> o el id de un contenedor.</summary>
    public JsonNode Container { get; init; } = new JsonObject { ["type"] = "auto" };
}

public sealed record McpTool : ResponseTool
{
    public override string Type => "mcp";

    public required string ServerLabel { get; init; }

    public string? ServerUrl { get; init; }

    public string? ConnectorId { get; init; }

    public string? Authorization { get; init; }

    public IDictionary<string, string>? Headers { get; init; }

    public JsonNode? AllowedTools { get; init; }

    /// <summary><c>"always"</c>, <c>"never"</c> o un objeto con filtros.</summary>
    public JsonNode? RequireApproval { get; init; }
}

/// <summary>Cualquier otra tool (image_generation, computer_use, custom…) en JSON crudo.</summary>
public sealed record RawTool(JsonNode Json) : ResponseTool, IRawJson
{
    public override string Type => Json["type"]?.GetValue<string>() ?? string.Empty;
}

/// <summary>Valor de <c>text.format</c>.</summary>
public abstract record ResponseTextFormat
{
    [JsonPropertyOrder(-1)]
    public abstract string Type { get; }
}

public sealed record TextFormat : ResponseTextFormat
{
    public override string Type => "text";
}

public sealed record JsonObjectFormat : ResponseTextFormat
{
    public override string Type => "json_object";
}

public sealed record JsonSchemaFormat : ResponseTextFormat
{
    public override string Type => "json_schema";

    public required string Name { get; init; }

    public required JsonNode Schema { get; init; }

    public string? Description { get; init; }

    public bool? Strict { get; init; } = true;
}

public sealed record RawTextFormat(JsonNode Json) : ResponseTextFormat, IRawJson
{
    public override string Type => Json["type"]?.GetValue<string>() ?? string.Empty;
}
