using SM.ChatRouter.Domain.Enums;
using SM.ChatRouter.Domain.ValueObjects;

namespace SM.ChatRouter.Api.Contracts;

public sealed record ChatRequest(string? ConversationId, string? Message);

/// <param name="Path">Ruta legible N1 &gt; N2 &gt; N3 (en el fallback virtual, solo su nombre).</param>
public sealed record CategorySummary(string Id, string Name, string Path);

public sealed record ChatResponse(
    string ConversationId,
    RoutingOutcome Outcome,
    string? Question,
    CategorySummary? Category,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<string> MissingParameters,
    FallbackReason? FallbackReason,
    string? Explanation)
{
    public static ChatResponse From(RoutingResult result) => new(
        result.ConversationId,
        result.Outcome,
        result.Question,
        result.Category is null ? null : new CategorySummary(result.Category.Id, result.Category.Name, result.Category.FullName),
        result.Parameters,
        result.MissingParameters,
        result.FallbackReason,
        result.Explanation);
}
