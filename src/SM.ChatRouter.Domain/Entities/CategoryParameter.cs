using System.Globalization;
using SM.ChatRouter.Domain.Enums;

namespace SM.ChatRouter.Domain.Entities;

public sealed record CategoryParameter
{
    /// <summary>Clave del parámetro (slug del nombre original). Es la que usa el modelo al extraerlo.</summary>
    public required string Name { get; init; }

    /// <summary>Texto original del campo, para mostrarlo al usuario y al modelo.</summary>
    public string Label { get; init; } = string.Empty;

    public ParameterType Type { get; init; } = ParameterType.String;

    public bool IsRequired { get; init; }

    /// <summary>Valores permitidos (solo aplica a <see cref="ParameterType.Enum"/>).</summary>
    public IReadOnlyList<string> AllowedValues { get; init; } = [];

    /// <summary>Pregunta predefinida para solicitar el parámetro al usuario.</summary>
    public string? Question { get; init; }

    /// <summary>
    /// Valida y normaliza un valor recibido para este parámetro.
    /// Devuelve <c>false</c> si el valor está vacío o no es compatible con el tipo.
    /// </summary>
    public bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        switch (Type)
        {
            case ParameterType.Number:
                if (!decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    return false;
                }
                normalized = number.ToString(CultureInfo.InvariantCulture);
                return true;

            case ParameterType.Date:
                if (!DateOnly.TryParse(trimmed, CultureInfo.InvariantCulture, out var date))
                {
                    return false;
                }
                normalized = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return true;

            case ParameterType.Boolean:
                if (!bool.TryParse(trimmed, out var flag))
                {
                    return false;
                }
                normalized = flag ? "true" : "false";
                return true;

            case ParameterType.Enum:
                var match = AllowedValues.FirstOrDefault(v => string.Equals(v, trimmed, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    return false;
                }
                normalized = match;
                return true;

            default:
                normalized = trimmed;
                return true;
        }
    }
}
