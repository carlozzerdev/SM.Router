using System.Text.Json.Nodes;
using SM.ChatRouter.Infrastructure.Ai.OpenAI.Models;

namespace SM.ChatRouter.Infrastructure.Ai.OpenAI;

/// <summary>Cliente de bajo nivel de la OpenAI Responses API (<c>/v1/responses</c>).</summary>
public interface IOpenAIResponsesClient
{
    /// <summary>Crea una respuesta sin stream. Los campos nulos se completan con <c>OpenAI:Defaults</c>.</summary>
    Task<ResponseResult> CreateAsync(ResponseRequest request, CancellationToken cancellationToken = default);

    /// <summary>Crea una respuesta en modo stream y devuelve los eventos SSE según llegan.</summary>
    IAsyncEnumerable<ResponseStreamEvent> StreamAsync(ResponseRequest request, CancellationToken cancellationToken = default);

    /// <summary>Envía el payload exactamente como se recibe, sin aplicar defaults.</summary>
    Task<ResponseResult> CreateRawAsync(JsonObject payload, CancellationToken cancellationToken = default);

    Task<ResponseResult> GetAsync(string responseId, CancellationToken cancellationToken = default);

    Task<ResponseResult> CancelAsync(string responseId, CancellationToken cancellationToken = default);

    Task DeleteAsync(string responseId, CancellationToken cancellationToken = default);
}
