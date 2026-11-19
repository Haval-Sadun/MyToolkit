using System.Globalization;

namespace MyToolkit.Theme;

/// <summary>Which way the interface reads, and the Material glyphs that point with it: a font glyph never mirrors on its own the way a real drawable does, so "back" and "opens a page" must be picked by direction. Platform-free.</summary>
public static class Direction
{
    public const string ArrowBack = "";
    public const string ArrowForward = "";
    public const string ChevronLeft = "";
    public const string ChevronRight = "";

    /// <summary>Set at startup from the language the user chose (see <see cref="Localization.LanguageSettings"/>); .NET calls Kurmanji ("ku") right to left, yet an app may lay it out left to right. Null follows the device culture.</summary>
    public static bool? ForcedRightToLeft { get; set; }

    public static bool IsRightToLeft => ForcedRightToLeft ?? CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

    /// <summary>The arrow for "back": pointing the way a reader goes back.</summary>
    public static string BackArrow => IsRightToLeft ? ArrowForward : ArrowBack;

    /// <summary>The "opens a page" marker: pointing the way a reader goes forward.</summary>
    public static string Chevron => IsRightToLeft ? ChevronLeft : ChevronRight;
}
