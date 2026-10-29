using SM.ChatRouter.Domain.Entities;
using SM.ChatRouter.Domain.Enums;

namespace SM.ChatRouter.Domain.ValueObjects;

public sealed record RoutingResult
{
    public required string ConversationId { get; init; }

    public required RoutingOutcome Outcome { get; init; }

    /// <summary>Categoría final (Routed/Fallback) o candidata (NeedsMoreInformation, si la hay).</summary>
    public Category? Category { get; init; }

    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<string> MissingParameters { get; init; } = [];

    /// <summary>Pregunta a mostrar al usuario cuando falta información.</summary>
    public string? Question { get; init; }

    public FallbackReason? FallbackReason { get; init; }

    /// <summary>Explicación del modelo sobre su decisión. Null si la IA no ha respondido (error, sin categorías).</summary>
    public string? Explanation { get; init; }
}
