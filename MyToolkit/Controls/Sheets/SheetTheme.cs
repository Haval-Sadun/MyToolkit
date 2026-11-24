using System.Diagnostics;

namespace MyToolkit.Controls.Sheets;

/// <summary>The palette keys the sheets read. A missing one does not fail to compile; the sheet just looks wrong, so a debug build says so once.</summary>
public static class SheetTheme
{
    /// <summary>Set by <c>PopupTheme.Apply</c> from the app's colors; fonts are named <c>AppFontFamily</c> and <c>AppSemiBoldFamily</c>.</summary>
    public static readonly string[] RequiredKeys =
    [
        "PrimaryColor",
        "TextColor",
        "TextSecondaryColor",
        "BackgroundSecondaryColor",
        "PopupBorderColor",
        "AppFontFamily",
        "AppSemiBoldFamily",
    ];

    private static bool _checked;

    /// <summary>The keys the app has not defined.</summary>
    public static IReadOnlyList<string> Missing() => RequiredKeys.Where(key => Application.Current?.Resources.ContainsKey(key) != true).ToList();

    /// <summary>Called when a sheet is built; writes once, in debug builds only.</summary>
    [Conditional("DEBUG")]
    internal static void CheckOnce()
    {
        if (_checked)
            return;

        _checked = true;
        if (Missing() is { Count: > 0 } missing)
            Debug.WriteLine($"[MyToolkit] A sheet needs these palette resources (see PopupTheme.Apply): {string.Join(", ", missing)}");
    }
}
