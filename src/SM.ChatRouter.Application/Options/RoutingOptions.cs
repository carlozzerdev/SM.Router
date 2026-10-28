namespace SM.ChatRouter.Application.Options;

public sealed class RoutingOptions
{
    public const string SectionName = "Routing";

    /// <summary>Id de la categoría a usar cuando no se encuentra ninguna.</summary>
    public string FallbackCategoryId { get; set; } = "general";

    /// <summary>Nombre de la categoría por defecto si el id de fallback no existe en el repositorio.</summary>
    public string FallbackCategoryName { get; set; } = "Consulta general";

    /// <summary>Confianza mínima (0-1) para aceptar la categoría elegida por el modelo.</summary>
    public double MinConfidence { get; set; } = 0.6;

    /// <summary>Número máximo de preguntas de aclaración antes de aplicar el fallback.</summary>
    public int MaxClarificationTurns { get; set; } = 3;

    public int MaxPromptLength { get; set; } = 4000;

    public TimeSpan AiTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public string DefaultFollowUpQuestion { get; set; } = "¿Podrías darme más detalles sobre lo que necesitas?";

    internal bool IsValid() =>
        !string.IsNullOrWhiteSpace(FallbackCategoryId)
        && MinConfidence is >= 0 and <= 1
        && MaxClarificationTurns >= 0
        && MaxPromptLength > 0
        && AiTimeout > TimeSpan.Zero;
}
