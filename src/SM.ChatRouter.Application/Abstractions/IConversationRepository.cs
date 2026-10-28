using SM.ChatRouter.Domain.Entities;

namespace SM.ChatRouter.Application.Abstractions;

public interface IConversationRepository
{
    Task<Conversation?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task SaveAsync(Conversation conversation, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
