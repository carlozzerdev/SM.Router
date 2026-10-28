using System.Text;
using SM.ChatRouter.Domain.Entities;

namespace SM.ChatRouter.Application.Services;

/// <summary>Construye la pregunta que se devuelve al usuario cuando falta información.</summary>
internal static class ClarificationQuestionBuilder
{
    public static string Build(
        IReadOnlyList<CategoryParameter> missing,
        IReadOnlyList<CategoryParameter> rejected,
        string? modelQuestion,
        string defaultQuestion)
    {
        var builder = new StringBuilder();

        foreach (var parameter in rejected)
        {
            builder.Append($"El valor indicado para '{parameter.Name}' no es válido");
            if (parameter.AllowedValues.Count > 0)
            {
                builder.Append($" (valores permitidos: {string.Join(", ", parameter.AllowedValues)})");
            }
            builder.AppendLine(".");
        }

        if (!string.IsNullOrWhiteSpace(modelQuestion))
        {
            builder.Append(modelQuestion.Trim());
            return builder.ToString();
        }

        switch (missing.Count)
        {
            case 0:
                builder.Append(defaultQuestion);
                break;
            case 1:
                builder.Append(QuestionFor(missing[0]));
                break;
            default:
                builder.AppendLine("Necesito algo más de información:");
                builder.AppendJoin(Environment.NewLine, missing.Select(p => $"- {QuestionFor(p)}"));
                break;
        }

        return builder.ToString().TrimEnd();
    }

    private static string QuestionFor(CategoryParameter parameter)
    {
        if (!string.IsNullOrWhiteSpace(parameter.Question))
        {
            return parameter.Question.Trim();
        }

        var subject = string.IsNullOrWhiteSpace(parameter.Label) ? parameter.Name : parameter.Label.Trim();

        // Si el campo ya es una pregunta ("¿Cómo realizaste tu pedido?") se usa tal cual.
        var question = subject.EndsWith('?')
            ? subject
            : $"¿Podrías indicarme {LowerFirst(subject)}?";
        return parameter.AllowedValues.Count > 0
            ? $"{question} ({string.Join(", ", parameter.AllowedValues)})"
            : question;
    }

    // Respeta las siglas: "DNI o NIE" se queda igual, "Fecha del pedido" pasa a "fecha del pedido".
    private static string LowerFirst(string text) =>
        text.Length > 1 && char.IsUpper(text[1]) ? text : $"{char.ToLowerInvariant(text[0])}{text[1..]}";
}
