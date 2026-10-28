using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SM.ChatRouter.Infrastructure.Ai.OpenAI.Models;

/// <summary>Opciones de serialización compartidas para la Responses API (snake_case, sin nulos).</summary>
public static class OpenAIJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new PolymorphicWriteConverter<ResponseInputItem>(),
                new PolymorphicWriteConverter<InputContentPart>(),
                new PolymorphicWriteConverter<ResponseTool>(),
                new PolymorphicWriteConverter<ResponseTextFormat>(),
                new ResponseOutputItemConverter()
            }
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}

/// <summary>Elemento que se serializa tal cual a partir de un JSON arbitrario.</summary>
public interface IRawJson
{
    JsonNode Json { get; }
}

/// <summary>
/// Serializa una jerarquía usando el tipo real del objeto (o el JSON crudo si implementa <see cref="IRawJson"/>).
/// Solo escritura: estas jerarquías se construyen en código, no se leen.
/// </summary>
internal sealed class PolymorphicWriteConverter<TBase> : JsonConverter<TBase> where TBase : class
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(TBase);

    public override TBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException($"La deserialización de {typeof(TBase).Name} no está soportada.");

    public override void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options)
    {
        if (value is IRawJson raw)
        {
            raw.Json.WriteTo(writer, options);
            return;
        }

        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
