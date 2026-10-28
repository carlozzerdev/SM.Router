namespace SM.ChatRouter.Application.Ai;

public enum AiDecision
{
    /// <summary>El modelo ha elegido una categoría y cree tener toda la información.</summary>
    CategorySelected,

    /// <summary>El modelo necesita más información (de la categoría o para decidir entre varias).</summary>
    NeedsMoreInformation,

    /// <summary>Ninguna categoría encaja con el prompt.</summary>
    NoMatch
}

public sealed record AiRoutingResponse
{
    public required AiDecision Decision { get; init; }

    public string? CategoryId { get; init; }

    /// <summary>Confianza del modelo entre 0 y 1.</summary>
    public double Confidence { get; init; }

    public IReadOnlyDictionary<string, string?> ExtractedParameters { get; init; } = new Dictionary<string, string?>();

    /// <summary>Pregunta propuesta por el modelo para pedir la información que falta.</summary>
    public string? FollowUpQuestion { get; init; }
}
