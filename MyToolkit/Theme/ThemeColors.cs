namespace MyToolkit.Theme;

/// <summary>Code-side access to palette tokens: an app defines <c>{Token}Light</c>/<c>{Token}Dark</c> colour resources (Material 3 roles such as Primary, OnSurface) and toolkit controls follow them. A missing token leaves the control default.</summary>
public static class ThemeColors
{
    /// <summary>Binds <paramref name="property"/> to the light/dark pair of <paramref name="token"/> (e.g. "Primary") so it follows the app theme.</summary>
    public static void Set(BindableObject target, BindableProperty property, string token)
    {
        if (Get($"{token}Light") is { } light && Get($"{token}Dark") is { } dark)
            target.SetAppThemeColor(property, light, dark);
    }

    /// <summary>The light/dark value of <paramref name="token"/> for the theme currently showing.</summary>
    public static Color? Current(string token) =>
        Application.Current?.RequestedTheme == AppTheme.Dark ? Get($"{token}Dark") : Get($"{token}Light");

    public static Color? Get(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true ? value as Color : null;

    public static Style? Style(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true ? value as Style : null;
}
