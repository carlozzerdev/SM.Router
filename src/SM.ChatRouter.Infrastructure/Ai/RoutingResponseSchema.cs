using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SM.ChatRouter.Application.Ai;

namespace SM.ChatRouter.Infrastructure.Ai;

/// <summary>
/// Esquema JSON (compatible con structured outputs en modo strict) de la decisión de routing y su
/// conversión a <see cref="AiRoutingResponse"/>. Es independiente del proveedor de IA.
/// </summary>
public static class RoutingResponseSchema
{
    public const string Name = "routing_decision";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Crea el esquema. Strict exige que todos los campos sean required y <c>additionalProperties: false</c>,
    /// por eso los parámetros son un array de pares nombre/valor en lugar de un diccionario.
    /// </summary>
    public static JsonObject Create(IEnumerable<string> categoryIds)
    {
        var ids = new JsonArray();
        foreach (var id in categoryIds)
        {
            ids.Add(id);
        }
        ids.Add(null);

        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new JsonArray("decision", "categoryId", "confidence", "extractedParameters", "followUpQuestion", "explanation"),
            ["properties"] = new JsonObject
            {
                ["decision"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray(Enum.GetNames<AiDecision>().Select(n => (JsonNode)n!).ToArray())
                },
                ["categoryId"] = new JsonObject
                {
                    ["type"] = new JsonArray("string", "null"),
                    ["enum"] = ids
                },
                ["confidence"] = new JsonObject
                {
                    ["type"] = "number",
                    ["description"] = "Confianza entre 0 y 1"
                },
                ["extractedParameters"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["additionalProperties"] = false,
                        ["required"] = new JsonArray("name", "value"),
                        ["properties"] = new JsonObject
                        {
                            ["name"] = new JsonObject { ["type"] = "string" },
                            ["value"] = new JsonObject { ["type"] = new JsonArray("string", "null") }
                        }
                    }
                },
                ["followUpQuestion"] = new JsonObject { ["type"] = new JsonArray("string", "null") },
                ["explanation"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Explicación breve de por qué se ha tomado la decisión"
                }
            }
        };
    }

    /// <summary>Convierte el JSON devuelto por el modelo. Lanza <see cref="JsonException"/> si no es válido.</summary>
    public static AiRoutingResponse Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonException("El modelo no devolvió contenido.");
        }

        var dto = JsonSerializer.Deserialize<RoutingDecisionDto>(json, JsonOptions)
            ?? throw new JsonException("El modelo devolvió un JSON nulo.");

        var parameters = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in dto.ExtractedParameters ?? [])
        {
            if (!string.IsNullOrWhiteSpace(parameter.Name))
            {
                parameters[parameter.Name] = parameter.Value;
            }
        }

        return new AiRoutingResponse
        {
            Decision = dto.Decision ?? throw new JsonException("Falta 'decision' en la respuesta del modelo."),
            CategoryId = dto.CategoryId,
            Confidence = Math.Clamp(dto.Confidence ?? 0, 0, 1),
            ExtractedParameters = parameters,
            FollowUpQuestion = dto.FollowUpQuestion,
            Explanation = dto.Explanation ?? string.Empty
        };
    }

    private sealed record RoutingDecisionDto(
        AiDecision? Decision,
        string? CategoryId,
        double? Confidence,
        List<ParameterDto>? ExtractedParameters,
        string? FollowUpQuestion,
        string? Explanation);

    private sealed record ParameterDto(string? Name, string? Value);
}
