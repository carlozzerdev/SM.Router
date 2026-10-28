using Microsoft.Extensions.Options;

namespace SM.ChatRouter.Infrastructure.Ai;

/// <summary>Prompt de sistema del routing, común a todos los proveedores de IA.</summary>
public interface IAiPromptProvider
{
    string SystemPrompt { get; }
}

/// <summary>
/// Lee el prompt de <see cref="AiOptions.SystemPromptPath"/> una sola vez (se registra como singleton).
/// Sin ruta configurada usa <see cref="AiPromptTemplates.DefaultSystemPrompt"/>.
/// </summary>
public sealed class FileAiPromptProvider(IOptions<AiOptions> options) : IAiPromptProvider
{
    private readonly Lazy<string> _systemPrompt = new(() => Load(options.Value));

    public string SystemPrompt => _systemPrompt.Value;

    private static string Load(AiOptions options)
    {
        if (options.ResolveSystemPromptPath() is not { } path)
        {
            return AiPromptTemplates.DefaultSystemPrompt;
        }

        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"No se encuentra el fichero del prompt de sistema: '{path}'.");
        }

        var prompt = File.ReadAllText(path).Trim();
        return prompt.Length > 0
            ? prompt
            : throw new InvalidOperationException($"El fichero del prompt de sistema está vacío: '{path}'.");
    }
}
