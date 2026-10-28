using SM.ChatRouter.Application.Ai;

namespace SM.ChatRouter.Application.Abstractions;

/// <summary>
/// Cliente del modelo de IA que clasifica el prompt del usuario en una de las categorías disponibles
/// y extrae los parámetros que encuentre en la conversación.
/// </summary>
public interface IAiRoutingClient
{
    Task<AiRoutingResponse> RouteAsync(AiRoutingRequest request, CancellationToken cancellationToken = default);
}
