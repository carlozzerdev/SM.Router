using System.Globalization;
using System.Text;

namespace SM.ChatRouter.Infrastructure.Repositories;

/// <summary>Genera slugs en minúsculas, sin tildes y con guiones: "¿Cómo realizaste tu pedido?" → "como-realizaste-tu-pedido".</summary>
public static class SlugGenerator
{
    public static string Generate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var pendingSeparator = false;
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(c))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }
                builder.Append(char.ToLowerInvariant(c));
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        return builder.ToString();
    }
}
