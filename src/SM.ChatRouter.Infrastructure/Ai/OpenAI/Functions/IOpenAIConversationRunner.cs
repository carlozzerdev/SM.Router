using SM.ChatRouter.Infrastructure.Ai.OpenAI.Models;

namespace SM.ChatRouter.Infrastructure.Ai.OpenAI.Functions;

/// <summary>
/// Ejecuta una petición a la Responses API resolviendo automáticamente las llamadas a funciones
/// registradas hasta obtener la respuesta final.
/// </summary>
public interface IOpenAIConversationRunner
{
    Task<ResponseResult> RunAsync(ResponseRequest request, CancellationToken cancellationToken = default);

    /// <summary>Igual que <see cref="RunAsync"/> pero emite los eventos de stream de cada vuelta.</summary>
    IAsyncEnumerable<ResponseStreamEvent> RunStreamingAsync(ResponseRequest request, CancellationToken cancellationToken = default);
}
