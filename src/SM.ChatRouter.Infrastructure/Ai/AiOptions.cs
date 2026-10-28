namespace SM.ChatRouter.Infrastructure.Ai;

/// <summary>Configuración común a todos los proveedores de IA (sección <c>"Ai"</c>).</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Proveedor que implementa el routing ("OpenAI"...).</summary>
    public string Provider { get; set; } = "OpenAI";

    /// <summary>
    /// Fichero con el prompt de sistema del routing. Si es relativo, se resuelve contra el directorio de la
    /// aplicación. Si no se configura, se usa <see cref="AiPromptTemplates.DefaultSystemPrompt"/>.
    /// </summary>
    public string? SystemPromptPath { get; set; }

    internal string? ResolveSystemPromptPath() =>
        string.IsNullOrWhiteSpace(SystemPromptPath)
            ? null
            : Path.IsPathRooted(SystemPromptPath) ? SystemPromptPath : Path.Combine(AppContext.BaseDirectory, SystemPromptPath);

    internal bool IsValid() =>
        !string.IsNullOrWhiteSpace(Provider)
        && (ResolveSystemPromptPath() is not { } path || File.Exists(path));
}
