using System.Text.Json;

namespace SM.ChatRouter.Infrastructure.Ai.OpenAI.Models;

/// <summary>
/// Evento SSE de la Responses API en modo stream (<c>response.created</c>, <c>response.output_text.delta</c>,
/// <c>response.function_call_arguments.delta</c>, <c>response.completed</c>, <c>error</c>…).
/// </summary>
public sealed record ResponseStreamEvent
{
    public required string Type { get; init; }

    public long? SequenceNumber { get; init; }

    /// <summary>Fragmento de texto o de argumentos en los eventos <c>*.delta</c>.</summary>
    public string? Delta { get; init; }

    public string? ItemId { get; init; }

    public int? OutputIndex { get; init; }

    /// <summary>Objeto <c>response</c> en los eventos <c>response.*</c> (created, completed, failed…).</summary>
    public ResponseResult? Response { get; init; }

    /// <summary>JSON completo del evento.</summary>
    public JsonElement Raw { get; init; }

    public bool IsTerminal => Type is "response.completed" or "response.failed" or "response.incomplete" or "error";

    internal static ResponseStreamEvent Parse(string data)
    {
        using var document = JsonDocument.Parse(data);
        var raw = document.RootElement.Clone();

        return new ResponseStreamEvent
        {
            Type = GetString(raw, "type") ?? "unknown",
            SequenceNumber = raw.TryGetProperty("sequence_number", out var seq) && seq.ValueKind == JsonValueKind.Number ? seq.GetInt64() : null,
            Delta = GetString(raw, "delta"),
            ItemId = GetString(raw, "item_id"),
            OutputIndex = raw.TryGetProperty("output_index", out var idx) && idx.ValueKind == JsonValueKind.Number ? idx.GetInt32() : null,
            Response = raw.TryGetProperty("response", out var response) && response.ValueKind == JsonValueKind.Object
                ? response.Deserialize<ResponseResult>(OpenAIJson.Options)
                : null,
            Raw = raw
        };
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
