using System.Globalization;
using System.Resources;

namespace MyToolkit.Localization;

/// <summary>Looks up UI text by key in the app's resource files, for the current UI culture. Platform-free; XAML reaches it through <see cref="LocalizeExtension"/>.</summary>
public static class Localizer
{
    private static ResourceManager? _manager;

    /// <summary>Call once at startup, after the UI culture is set, with the app's string resources.</summary>
    public static void Initialize(ResourceManager manager) => _manager = manager;

    /// <summary>The text for <paramref name="key"/> in <paramref name="culture"/> (default: the current UI culture), falling back through its parent cultures to the neutral file.
    /// A key with no text at all comes back as itself, so a missing string shows its name instead of a blank.</summary>
    public static string Get(string key, CultureInfo? culture = null)
    {
        if (_manager is null)
            return key;

        try
        {
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
