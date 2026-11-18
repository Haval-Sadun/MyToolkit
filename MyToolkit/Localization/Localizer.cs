using System.Globalization;
using System.Resources;

namespace MyToolkit.Localization;

/// <summary>Looks up UI text by key in the app's resource files, for the current UI culture. Platform-free; XAML reaches it through <see cref="LocalizeExtension"/>.</summary>
public static class Localizer
{
    private static ResourceManager? _manager;
    private static ResourceManager? _variant;

    /// <summary>Call once at startup, after the UI culture is set, with the app's string resources.
    /// <paramref name="variant"/> is a complete translation kept in the app's own assembly instead of a satellite (a language build tools and devices do not always know as a culture); its text wins, and anything it lacks falls back to <paramref name="manager"/>.</summary>
    public static void Initialize(ResourceManager manager, ResourceManager? variant = null)
    {
        _manager = manager;
        _variant = variant;
    }

    /// <summary>The text for <paramref name="key"/> in <paramref name="culture"/> (default: the current UI culture), falling back through its parent cultures to the neutral file.
    /// A key with no text at all comes back as itself, so a missing string shows its name instead of a blank.</summary>
    public static string Get(string key, CultureInfo? culture = null)
    {
        if (_manager is null)
            return key;

        try
        {
            if (_variant?.GetString(key, CultureInfo.InvariantCulture) is { } text)
                return text;

            return _manager.GetString(key, culture ?? CultureInfo.CurrentUICulture) ?? key;
        }
        catch (MissingManifestResourceException)
        {
            return key;
        }
    }

    /// <summary>Same as <see cref="Get"/>, with the text used as a format string for <paramref name="args"/>.</summary>
    public static string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentUICulture, Get(key), args);
}
