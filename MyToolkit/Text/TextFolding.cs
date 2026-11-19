using System.Globalization;
using System.Text;

namespace MyToolkit.Text;

/// <summary>Folds text so two spellings of the same thing compare equal: case, diacritics, Turkish and other special Latin letters, punctuation, and the Arabic-script variants <see cref="ArabicScriptFold"/> knows. For searching and matching, never for display. Platform-free.</summary>
public static class TextFolding
{
    /// <summary>Lowercase, diacritics removed, dotless ı and ß-style letters mapped to ASCII, Arabic-script variants made one, punctuation turned into single spaces.</summary>
    public static string Fold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var lower = text.Replace("İ", "i").ToLowerInvariant()
            .Replace("ı", "i").Replace("ß", "ss").Replace("ø", "o").Replace("æ", "ae")
            .Replace("œ", "oe").Replace("đ", "d").Replace("ł", "l");

        var builder = new StringBuilder(lower.Length);
        var lastSpace = true;
        foreach (var raw in lower.Normalize(NormalizationForm.FormKD))
        {
            var c = ArabicScriptFold.Map(raw);
            if (c == '\0' || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                lastSpace = false;
                continue;
            }

            if (lastSpace)
                continue;

            builder.Append(' ');
            lastSpace = true;
        }

        return builder.ToString().TrimEnd();
    }
}
