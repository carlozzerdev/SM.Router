using SM.ChatRouter.Application.Ai;

namespace SM.ChatRouter.Tests.TestData;

/// <summary>Atajos para construir respuestas del modelo de IA en los tests.</summary>
internal static class Ai
{
    public static AiRoutingResponse Selected(string categoryId, double confidence = 0.9, params (string Name, string? Value)[] parameters) => new()
    {
        Decision = AiDecision.CategorySelected,
        CategoryId = categoryId,
        Confidence = confidence,
        ExtractedParameters = ToDictionary(parameters)
    };

    public static AiRoutingResponse NeedsInfo(string? categoryId, string? question = null, params (string Name, string? Value)[] parameters) => new()
    {
        Decision = AiDecision.NeedsMoreInformation,
        CategoryId = categoryId,
        Confidence = 0.8,
        FollowUpQuestion = question,
        ExtractedParameters = ToDictionary(parameters)
    };

    public static AiRoutingResponse NoMatch() => new() { Decision = AiDecision.NoMatch };

    private static Dictionary<string, string?> ToDictionary((string Name, string? Value)[] parameters) =>
        parameters.ToDictionary(p => p.Name, p => p.Value);
}
