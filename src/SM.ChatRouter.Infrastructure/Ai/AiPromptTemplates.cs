using System.Text;
using System.Text.Json;
using SM.ChatRouter.Application.Ai;

namespace SM.ChatRouter.Infrastructure.Ai;

/// <summary>
/// Prompt de sistema por defecto y contexto dinámico del routing. El prompt que se usa en producción se lee de
/// <see cref="AiOptions.SystemPromptPath"/> (<see cref="IAiPromptProvider"/>); este solo aplica si no se configura.
/// </summary>
public static class AiPromptTemplates
{
    public const string DefaultSystemPrompt =
        """
        Eres un clasificador de solicitudes del chat de atención al cliente.
        Las categorías forman un árbol de tres niveles (nivel 1 > nivel 2 > nivel 3) y solo se pueden elegir
        categorías de nivel 3, identificadas por su id. Usa la ruta completa para entender el contexto: el nivel 1
        indica el tipo de usuario o ámbito (p. ej. librerías, profesores, familias) y hay categorías de nivel 3 con el
        mismo nombre en ramas distintas.

        Reglas:
        - Usa únicamente los ids de categoría de la lista. No inventes categorías ni parámetros.
        - Si ninguna categoría encaja, responde con decision = "NoMatch".
        - Si la categoría está clara pero faltan parámetros obligatorios, responde con decision = "NeedsMoreInformation",
          indica el categoryId y redacta en followUpQuestion una pregunta breve y amable pidiendo SOLO lo que falta.
        - Si dudas entre varias categorías (por ejemplo, el mismo problema en ramas distintas), responde
          "NeedsMoreInformation" sin categoryId y pregunta para desambiguar.
        - Si tienes todos los parámetros obligatorios, responde decision = "CategorySelected".
        - Extrae en extractedParameters todos los parámetros que encuentres en la conversación (no solo en el último
          mensaje), usando como name la clave del parámetro ("name"), no su etiqueta.
        - Si un parámetro tiene allowedValues, el valor debe ser exactamente uno de ellos.
        - confidence es un número entre 0 y 1.

        Responde EXCLUSIVAMENTE con un JSON con este esquema:
        {
          "decision": "CategorySelected" | "NeedsMoreInformation" | "NoMatch",
          "categoryId": "string o null",
          "confidence": 0.0,
          "extractedParameters": [ { "name": "clave-del-parametro", "value": "valor" } ],
          "followUpQuestion": "string o null"
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Compacto y sin escapar tildes: son 50+ categorías y se paga por token.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Serializa las categorías (N3 con su ruta) y el estado de la conversación como contexto para el modelo.</summary>
    public static string BuildContext(AiRoutingRequest request)
    {
        var categories = request.Categories.Select(c => new
        {
            id = c.Id,
            path = c.FullName,
            parameters = c.Parameters.Select(p => new
            {
                name = p.Name,
                label = p.Label,
                required = p.IsRequired,
                allowedValues = p.AllowedValues.Count > 0 ? p.AllowedValues : null
            })
        });

        var builder = new StringBuilder();
        builder.AppendLine("Categorías disponibles (una por línea):");
        foreach (var category in categories)
        {
            builder.AppendLine(JsonSerializer.Serialize(category, JsonOptions));
        }

        if (request.CandidateCategoryId is not null)
        {
            builder.AppendLine($"Categoría candidata de turnos anteriores: {request.CandidateCategoryId}");
        }

        if (request.CollectedParameters.Count > 0)
        {
            builder.AppendLine("Parámetros ya recogidos:");
            builder.AppendLine(JsonSerializer.Serialize(request.CollectedParameters, JsonOptions));
        }

        return builder.ToString();
    }
}
