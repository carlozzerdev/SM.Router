using System.Collections.Concurrent;
using SM.ChatRouter.Application.Abstractions;
using SM.ChatRouter.Domain.Entities;

namespace SM.ChatRouter.Infrastructure.Repositories;

/// <summary>
/// Almacén de conversaciones en memoria. Sustituible por Redis o base de datos
/// implementando <see cref="IConversationRepository"/>.
/// </summary>
public sealed class InMemoryConversationRepository : IConversationRepository
{
    private readonly ConcurrentDictionary<string, Conversation> _conversations = new(StringComparer.Ordinal);

    public Task<Conversation?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return Task.FromResult(_conversations.GetValueOrDefault(id));
    }

    public Task SaveAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        _conversations[conversation.Id] = conversation;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return Task.FromResult(_conversations.TryRemove(id, out _));
    }
}
