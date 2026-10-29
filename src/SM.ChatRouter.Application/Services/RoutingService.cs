using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SM.ChatRouter.Application.Abstractions;
using SM.ChatRouter.Application.Ai;
using SM.ChatRouter.Application.Exceptions;
using SM.ChatRouter.Application.Options;
using SM.ChatRouter.Domain.Entities;
using SM.ChatRouter.Domain.Enums;
using SM.ChatRouter.Domain.ValueObjects;

namespace SM.ChatRouter.Application.Services;

/// <summary>
/// Orquesta el routing de un mensaje del web chat: consulta al modelo de IA, valida su respuesta
/// contra los metadatos de las categorías, pide los parámetros obligatorios que falten y aplica
/// el fallback cuando no es posible determinar una categoría.
/// </summary>
public sealed class RoutingService(
    ICategoryRepository categoryRepository,
    IConversationRepository conversationRepository,
    IAiRoutingClient aiClient,
    IFallbackCategoryProvider fallbackProvider,
    IOptions<RoutingOptions> options,
    TimeProvider timeProvider,
    ILogger<RoutingService> logger) : IRoutingService
{
    private readonly RoutingOptions _options = options.Value;

    public async Task<RoutingResult> ProcessMessageAsync(
        string? conversationId,
        string? message,
        CancellationToken cancellationToken = default)
    {
        var prompt = ValidateMessage(message);
        var conversation = await GetOrCreateConversationAsync(conversationId, cancellationToken);
        conversation.AddUserMessage(prompt);

        var result = await RouteAsync(conversation, prompt, cancellationToken);

        await conversationRepository.SaveAsync(conversation, cancellationToken);
        return result;
    }

    public Task<bool> ResetConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        return conversationRepository.DeleteAsync(conversationId, cancellationToken);
    }

    private async Task<RoutingResult> RouteAsync(Conversation conversation, string prompt, CancellationToken cancellationToken)
    {
        var categories = (await categoryRepository.GetAllAsync(cancellationToken))
            .Where(c => !IsConfiguredFallback(c))
            .ToList();

        if (categories.Count == 0)
        {
            return await FallbackAsync(conversation, null, FallbackReason.NoCategoriesAvailable, cancellationToken);
        }

        var response = await CallAiAsync(conversation, prompt, categories, cancellationToken);
        if (response is null)
        {
            return await FallbackAsync(conversation, null, FallbackReason.AiError, cancellationToken);
        }

        // La explicación del modelo acompaña a cualquier resultado que salga de su respuesta
        // (también al fallback por NoMatch, confianza baja, etc.).
        var result = await ApplyDecisionAsync(conversation, categories, response, cancellationToken);
        return result with { Explanation = string.IsNullOrWhiteSpace(response.Explanation) ? null : response.Explanation.Trim() };
    }

    private async Task<RoutingResult> ApplyDecisionAsync(
        Conversation conversation,
        IReadOnlyList<Category> categories,
        AiRoutingResponse response,
        CancellationToken cancellationToken)
    {
        var category = string.IsNullOrWhiteSpace(response.CategoryId)
            ? null
            : categories.FirstOrDefault(c => string.Equals(c.Id, response.CategoryId.Trim(), StringComparison.OrdinalIgnoreCase));

        switch (response.Decision)
        {
            case AiDecision.NoMatch:
                return await FallbackAsync(conversation, response.FollowUpQuestion, FallbackReason.NoMatch, cancellationToken);

            case AiDecision.CategorySelected when category is null:
                logger.LogWarning("El modelo devolvió una categoría desconocida: {CategoryId}", response.CategoryId);
                return await FallbackAsync(conversation, response.FollowUpQuestion, FallbackReason.UnknownCategory, cancellationToken);

            case AiDecision.CategorySelected when response.Confidence < _options.MinConfidence:
                logger.LogInformation("Confianza {Confidence} inferior al mínimo {Min} para {CategoryId}",
                    response.Confidence, _options.MinConfidence, category.Id);
                return await FallbackAsync(conversation, response.FollowUpQuestion,     FallbackReason.LowConfidence, cancellationToken);

            case AiDecision.CategorySelected:
                return await ResolveCategoryAsync(conversation, category, response, modelWantsMoreInfo: false, cancellationToken);

            case AiDecision.NeedsMoreInformation when category is not null:
                return await ResolveCategoryAsync(conversation, category, response, modelWantsMoreInfo: true, cancellationToken);

            case AiDecision.NeedsMoreInformation:
                // El modelo aún no sabe qué categoría es: se pregunta sin candidata.
                return await AskAsync(conversation, null, [], [], response.FollowUpQuestion, cancellationToken);

            default:
                logger.LogWarning("Decisión del modelo no soportada: {Decision}", response.Decision);
                return await FallbackAsync(conversation, response.FollowUpQuestion , FallbackReason.AiError, cancellationToken);
        }
    }

    private async Task<RoutingResult> ResolveCategoryAsync(
        Conversation conversation,
        Category category,
        AiRoutingResponse response,
        bool modelWantsMoreInfo,
        CancellationToken cancellationToken)
    {
        conversation.SetCandidate(category);
        var rejectedNames = conversation.MergeParameters(category, response.ExtractedParameters);
        var rejected = rejectedNames.Select(n => category.FindParameter(n)!).ToList();
        var missing = category.GetMissingRequired(conversation.CollectedParameters);

        // No se confía en el modelo: aunque diga que ha terminado, si faltan obligatorios se preguntan.
        var mustAsk = missing.Count > 0
            || (modelWantsMoreInfo && !string.IsNullOrWhiteSpace(response.FollowUpQuestion));

        if (mustAsk)
        {
            return await AskAsync(conversation, category, missing, rejected, response.FollowUpQuestion, cancellationToken);
        }

        conversation.MarkRouted(category);
        conversation.AddAssistantMessage($"Solicitud clasificada como '{category.Name}'.");
        logger.LogInformation("Conversación {ConversationId} enrutada a {CategoryId}", conversation.Id, category.Id);

        return new RoutingResult
        {
            ConversationId = conversation.Id,
            Outcome = RoutingOutcome.Routed,
            Category = category,
            Parameters = new Dictionary<string, string>(conversation.CollectedParameters, StringComparer.OrdinalIgnoreCase)
        };
    }

    private async Task<RoutingResult> AskAsync(
        Conversation conversation,
        Category? category,
        IReadOnlyList<CategoryParameter> missing,
        IReadOnlyList<CategoryParameter> rejected,
        string? modelQuestion,
        CancellationToken cancellationToken)
    {
        if (conversation.ClarificationTurns >= _options.MaxClarificationTurns)
        {
            return await FallbackAsync(conversation, modelQuestion, FallbackReason.MaxClarificationsReached, cancellationToken);
        }

        var question = ClarificationQuestionBuilder.Build(missing, rejected, modelQuestion, _options.DefaultFollowUpQuestion);
        conversation.MarkAwaitingInformation();
        conversation.AddAssistantMessage(question);

        return new RoutingResult
        {
            ConversationId = conversation.Id,
            Outcome = RoutingOutcome.NeedsMoreInformation,
            Category = category,
            Parameters = new Dictionary<string, string>(conversation.CollectedParameters, StringComparer.OrdinalIgnoreCase),
            MissingParameters = missing.Select(p => p.Name).ToList(),
            Question = question
        };
    }

    private async Task<RoutingResult> FallbackAsync(
        Conversation conversation,
        string? question,
        FallbackReason reason,
        CancellationToken cancellationToken)
    {
        var fallback = await fallbackProvider.GetFallbackAsync(cancellationToken);
        conversation.MarkFallback(fallback);
        conversation.AddAssistantMessage($"Solicitud derivada a '{fallback.Name}'.");
        logger.LogInformation("Conversación {ConversationId} derivada a fallback {CategoryId}. Motivo: {Reason}",
            conversation.Id, fallback.Id, reason);

        return new RoutingResult
        {
            ConversationId = conversation.Id,
            Outcome = RoutingOutcome.Fallback,
            Category = fallback,
            FallbackReason = reason,
            Question = question
        };
    }

    private async Task<AiRoutingResponse?> CallAiAsync(
        Conversation conversation,
        string prompt,
        IReadOnlyList<Category> categories,
        CancellationToken cancellationToken)
    {
        var request = new AiRoutingRequest
        {
            UserPrompt = prompt,
            History = conversation.Messages.ToList(),
            Categories = categories,
            CandidateCategoryId = conversation.CandidateCategoryId,
            CollectedParameters = new Dictionary<string, string>(conversation.CollectedParameters, StringComparer.OrdinalIgnoreCase)
        };

        using var timeoutCts = new CancellationTokenSource(_options.AiTimeout, timeProvider);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            return await aiClient.RouteAsync(request, linkedCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex, "El modelo de IA superó el timeout de {Timeout}", _options.AiTimeout);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error llamando al modelo de IA");
            return null;
        }
    }

    private async Task<Conversation> GetOrCreateConversationAsync(string? conversationId, CancellationToken cancellationToken)
    {
        var id = string.IsNullOrWhiteSpace(conversationId) ? Guid.NewGuid().ToString("N") : conversationId.Trim();
        var conversation = await conversationRepository.GetAsync(id, cancellationToken)
            ?? new Conversation(id, timeProvider);

        // Un mensaje nuevo tras una conversación ya enrutada inicia un routing nuevo.
        if (conversation.IsClosed)
        {
            conversation.Reset();
        }

        return conversation;
    }

    private string ValidateMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new InvalidChatMessageException("El mensaje no puede estar vacío.");
        }

        var trimmed = message.Trim();
        if (trimmed.Length > _options.MaxPromptLength)
        {
            throw new InvalidChatMessageException(
                $"El mensaje supera la longitud máxima permitida ({_options.MaxPromptLength} caracteres).");
        }

        return trimmed;
    }

    private bool IsConfiguredFallback(Category category) =>
        string.Equals(category.Id, _options.FallbackCategoryId, StringComparison.OrdinalIgnoreCase);
}
