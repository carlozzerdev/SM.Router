using SM.ChatRouter.Domain.ValueObjects;

namespace SM.ChatRouter.Application.Services;

public interface IRoutingService
{
    /// <summary>
    /// Procesa un mensaje del usuario. Si <paramref name="conversationId"/> es nulo o no existe,
    /// se inicia una conversación nueva.
    /// </summary>
    Task<RoutingResult> ProcessMessageAsync(string? conversationId, string? message, CancellationToken cancellationToken = default);

    Task<bool> ResetConversationAsync(string conversationId, CancellationToken cancellationToken = default);
}
