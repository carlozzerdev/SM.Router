using SM.ChatRouter.Domain.Entities;

namespace SM.ChatRouter.Application.Ai;

public sealed record AiRoutingRequest
{
    /// <summary>Último mensaje escrito por el usuario.</summary>
    public required string UserPrompt { get; init; }

    /// <summary>Historial completo de la conversación (incluye el último mensaje).</summary>
    public IReadOnlyList<ChatMessage> History { get; init; } = [];

    /// <summary>Categorías entre las que el modelo puede elegir (sin la de fallback).</summary>
    public required IReadOnlyList<Category> Categories { get; init; }

    /// <summary>Categoría propuesta en turnos anteriores, si la hay.</summary>
    public string? CandidateCategoryId { get; init; }

    /// <summary>Parámetros ya recogidos en turnos anteriores.</summary>
    public IReadOnlyDictionary<string, string> CollectedParameters { get; init; } = new Dictionary<string, string>();
}
