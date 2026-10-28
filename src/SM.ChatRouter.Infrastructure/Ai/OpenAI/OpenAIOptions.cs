namespace SM.ChatRouter.Infrastructure.Ai.OpenAI;

public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";

    /// <summary>No guardar en appsettings: usar user-secrets o la variable de entorno <c>OpenAI__ApiKey</c>.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Modelo por defecto si la petición no indica otro.</summary>
    public string Model { get; set; } = "gpt-5";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";

    public string? Organization { get; set; }

    public string? Project { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>Máximo de vueltas de function calling antes de abortar.</summary>
    public int MaxFunctionCallIterations { get; set; } = 5;

    /// <summary>Valores que se aplican a cada petición solo si esta no los fija.</summary>
    public OpenAIRequestDefaults Defaults { get; set; } = new();

    internal bool IsValid() =>
        !string.IsNullOrWhiteSpace(Model)
        && Uri.TryCreate(BaseUrl, UriKind.Absolute, out _)
        && Timeout > TimeSpan.Zero
        && MaxFunctionCallIterations > 0;
}

public sealed class OpenAIRequestDefaults
{
    /// <summary><c>none</c>, <c>minimal</c>, <c>low</c>, <c>medium</c>, <c>high</c>… (según el modelo).</summary>
    public string? ReasoningEffort { get; set; }

    /// <summary><c>auto</c>, <c>concise</c> o <c>detailed</c>.</summary>
    public string? ReasoningSummary { get; set; }

    /// <summary><c>low</c>, <c>medium</c> o <c>high</c>.</summary>
    public string? Verbosity { get; set; }

    public int? MaxOutputTokens { get; set; }

    public double? Temperature { get; set; }

    public double? TopP { get; set; }

    public bool? Store { get; set; }

    public string? ServiceTier { get; set; }

    public string? Truncation { get; set; }
}
