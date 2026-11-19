using System.Globalization;

namespace MyToolkit.Localization;

/// <summary>A language an app's own text comes in.</summary>
/// <param name="Code">"" for the system's language, else a culture name ("en", "ckb").</param>
/// <param name="NativeName">The language written in itself, shown in the picker; null for the system choice, which has no name of its own.</param>
/// <param name="IsRightToLeft">Whether its script runs right to left. Stated here rather than read from the culture, which not every platform answers the same way for some languages (Kurdish, for one).</param>
public sealed record LanguageChoice(string Code, string? NativeName, bool IsRightToLeft = false);

/// <summary>An app's interface languages and the rules for turning a saved choice into a culture. Platform-free; the saved choice lives in <see cref="LanguageSettings"/>.</summary>
public sealed class LanguageCatalog(IReadOnlyList<LanguageChoice> choices)
{
    /// <summary>The system's language: the app text follows the device, or the neutral text when the device language has none.</summary>
    public const string System = "";

    public IReadOnlyList<LanguageChoice> Choices => choices;

    /// <summary>The culture for a saved choice. Null means "leave the device's culture alone": the system choice, an unknown code, or a culture this platform does not know.</summary>
    public CultureInfo? Resolve(string? code)
    {
        if (string.IsNullOrEmpty(code) || !choices.Any(choice => choice.Code == code))
            return null;

        try
        {
            return CultureInfo.GetCultureInfo(code);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Whether the chosen language's script runs right to left. False for the system choice and for a code nobody knows.</summary>
    public bool IsRightToLeft(string? code) => choices.FirstOrDefault(choice => choice.Code == code)?.IsRightToLeft ?? false;
}
