using System.Text.Json;
using System.Text.Json.Nodes;

namespace SM.ChatRouter.Infrastructure.Ai.OpenAI.Functions;

/// <summary>
/// Función que el modelo puede invocar (function calling). Se registra con
/// <c>services.AddOpenAIFunction&lt;T&gt;()</c> y el <see cref="IOpenAIConversationRunner"/> la ejecuta automáticamente.
/// </summary>
public interface IOpenAIFunctionHandler
{
    string Name { get; }

    string? Description { get; }

    /// <summary>JSON Schema de los argumentos. Con <see cref="Strict"/> = true todos los campos deben ser required.</summary>
    JsonNode ParametersSchema { get; }

    bool Strict { get; }

    /// <summary>Ejecuta la función y devuelve el resultado (normalmente JSON) que se envía al modelo.</summary>
    Task<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken);
}
