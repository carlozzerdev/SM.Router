using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SM.ChatRouter.Infrastructure.Ai.OpenAI.Models;

/// <summary>Objeto <c>response</c> devuelto por la API.</summary>
public sealed record ResponseResult
{
    public string Id { get; init; } = string.Empty;

    public string? Object { get; init; }

    public long? CreatedAt { get; init; }

    /// <summary><c>completed</c>, <c>failed</c>, <c>in_progress</c>, <c>cancelled</c>, <c>queued</c> o <c>incomplete</c>.</summary>
    public string? Status { get; init; }

    public string? Model { get; init; }

    public IReadOnlyList<ResponseOutputItem> Output { get; init; } = [];

    public ResponseUsage? Usage { get; init; }

    public ResponseError? Error { get; init; }

    public IncompleteDetails? IncompleteDetails { get; init; }

    public string? PreviousResponseId { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }

    /// <summary>Concatenación de todos los <c>output_text</c> de los mensajes de salida.</summary>
    [JsonIgnore]
    public string OutputText => string.Concat(
        Output.OfType<OutputMessage>()
            .SelectMany(m => m.Content)
            .Where(c => c.Type == "output_text")
            .Select(c => c.Text));

    /// <summary>Primer rechazo (<c>refusal</c>) del modelo, si lo hay.</summary>
    [JsonIgnore]
    public string? Refusal => Output.OfType<OutputMessage>()
        .SelectMany(m => m.Content)
        .FirstOrDefault(c => c.Type == "refusal")?.Refusal;

    [JsonIgnore]
    public IReadOnlyList<OutputFunctionCall> FunctionCalls => Output.OfType<OutputFunctionCall>().ToList();
}

public sealed record ResponseUsage
{
    public int InputTokens { get; init; }

    public int OutputTokens { get; init; }

    public int TotalTokens { get; init; }

    public InputTokensDetails? InputTokensDetails { get; init; }

    public OutputTokensDetails? OutputTokensDetails { get; init; }
}

public sealed record InputTokensDetails(int CachedTokens);

public sealed record OutputTokensDetails(int ReasoningTokens);

public sealed record ResponseError(string? Code, string? Message);

public sealed record IncompleteDetails(string? Reason);

/// <summary>Elemento del array <c>output</c>. <see cref="Raw"/> conserva el JSON original.</summary>
public abstract record ResponseOutputItem
{
    public string Type { get; init; } = string.Empty;

    public string? Id { get; init; }

    public string? Status { get; init; }

    [JsonIgnore]
    public JsonElement Raw { get; init; }

    /// <summary>Convierte el item en un item de entrada para reenviarlo en la siguiente petición.</summary>
    public InputRawItem ToInputItem() => new(JsonNode.Parse(Raw.GetRawText())!);
}

public sealed record OutputMessage : ResponseOutputItem
{
    public string? Role { get; init; }

    public IReadOnlyList<OutputContent> Content { get; init; } = [];
}

public sealed record OutputContent
{
    /// <summary><c>output_text</c> o <c>refusal</c>.</summary>
    public string Type { get; init; } = string.Empty;

    public string? Text { get; init; }

    public string? Refusal { get; init; }

    public JsonElement? Annotations { get; init; }
}

public sealed record OutputFunctionCall : ResponseOutputItem
{
    public string CallId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>Argumentos en JSON (string).</summary>
    public string Arguments { get; init; } = "{}";
}

public sealed record OutputReasoning : ResponseOutputItem
{
    public IReadOnlyList<ReasoningSummaryPart> Summary { get; init; } = [];

    public string? EncryptedContent { get; init; }
}

public sealed record ReasoningSummaryPart(string Type, string Text);

/// <summary>Cualquier otro item (web_search_call, file_search_call, mcp_call…): usar <see cref="ResponseOutputItem.Raw"/>.</summary>
public sealed record OutputUnknownItem : ResponseOutputItem;

internal sealed class ResponseOutputItemConverter : JsonConverter<ResponseOutputItem>
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(ResponseOutputItem);

    public override ResponseOutputItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var raw = document.RootElement.Clone();
        var type = raw.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;

        ResponseOutputItem item = type switch
        {
            "message" => raw.Deserialize<OutputMessage>(options)!,
            "function_call" => raw.Deserialize<OutputFunctionCall>(options)!,
            "reasoning" => raw.Deserialize<OutputReasoning>(options)!,
            _ => raw.Deserialize<OutputUnknownItem>(options)!
        };
        return item with { Raw = raw };
    }

    public override void Write(Utf8JsonWriter writer, ResponseOutputItem value, JsonSerializerOptions options) =>
        value.Raw.WriteTo(writer);
}
