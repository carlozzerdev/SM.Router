using SM.ChatRouter.Domain.Enums;

namespace SM.ChatRouter.Domain.Entities;

public sealed class Conversation
{
    private readonly List<ChatMessage> _messages = [];
    private readonly Dictionary<string, string> _collectedParameters = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider _timeProvider;

    public Conversation(string id, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        _timeProvider = timeProvider ?? TimeProvider.System;
        CreatedAt = UpdatedAt = _timeProvider.GetUtcNow();
    }

    public string Id { get; }

    public IReadOnlyList<ChatMessage> Messages => _messages;

    public IReadOnlyDictionary<string, string> CollectedParameters => _collectedParameters;

    /// <summary>Categoría propuesta por el modelo para la que se están recogiendo parámetros.</summary>
    public string? CandidateCategoryId { get; private set; }

    public string? RoutedCategoryId { get; private set; }

    public int ClarificationTurns { get; private set; }

    public ConversationStatus Status { get; private set; } = ConversationStatus.Open;

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsClosed => Status is ConversationStatus.Routed or ConversationStatus.FallbackRouted;

    public void AddUserMessage(string content) => AddMessage(ChatRole.User, content);

    public void AddAssistantMessage(string content) => AddMessage(ChatRole.Assistant, content);

    /// <summary>
    /// Fija la categoría candidata. Si cambia respecto a la anterior, se descartan los
    /// parámetros recogidos que la nueva categoría no define.
    /// </summary>
    public void SetCandidate(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);
        if (string.Equals(CandidateCategoryId, category.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CandidateCategoryId = category.Id;
        foreach (var key in _collectedParameters.Keys.ToList())
        {
            if (category.FindParameter(key) is null)
            {
                _collectedParameters.Remove(key);
            }
        }
        Touch();
    }

    /// <summary>
    /// Fusiona parámetros extraídos, quedándose solo con los que la categoría define y tienen valor válido.
    /// Devuelve los nombres de los parámetros descartados por tener un valor inválido.
    /// </summary>
    public IReadOnlyList<string> MergeParameters(Category category, IReadOnlyDictionary<string, string?> extracted)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(extracted);

        var rejected = new List<string>();
        foreach (var (name, value) in extracted)
        {
            var parameter = category.FindParameter(name);
            if (parameter is null)
            {
                continue;
            }

            if (parameter.TryNormalize(value, out var normalized))
            {
                _collectedParameters[parameter.Name] = normalized;
            }
            else if (!string.IsNullOrWhiteSpace(value))
            {
                rejected.Add(parameter.Name);
            }
        }
        Touch();
        return rejected;
    }

    public void MarkAwaitingInformation()
    {
        Status = ConversationStatus.AwaitingInformation;
        ClarificationTurns++;
        Touch();
    }

    public void MarkRouted(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);
        RoutedCategoryId = category.Id;
        Status = ConversationStatus.Routed;
        Touch();
    }

    public void MarkFallback(Category fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        RoutedCategoryId = fallback.Id;
        Status = ConversationStatus.FallbackRouted;
        Touch();
    }

    /// <summary>Reinicia el estado de routing manteniendo el identificador.</summary>
    public void Reset()
    {
        _messages.Clear();
        _collectedParameters.Clear();
        CandidateCategoryId = null;
        RoutedCategoryId = null;
        ClarificationTurns = 0;
        Status = ConversationStatus.Open;
        Touch();
    }

    private void AddMessage(ChatRole role, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        _messages.Add(new ChatMessage(role, content, _timeProvider.GetUtcNow()));
        Touch();
    }

    private void Touch() => UpdatedAt = _timeProvider.GetUtcNow();
}
