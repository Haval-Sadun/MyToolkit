namespace MyToolkit.Services.Errors;

/// <summary>
/// <see cref="IErrorPageTheme"/> implementation that reads colors from the app's
/// <see cref="Microsoft.Maui.Controls.ResourceDictionary"/> at runtime, so every consuming
/// app drives the error-page palette purely from its own Colors.xaml (or App.xaml).
///
/// Register this as the <see cref="IErrorPageTheme"/> singleton in MauiProgram.cs and add
/// the six semantic alias keys below to the app's resource dictionary — point each one at an
/// existing color already in the app's palette:
///
/// <code>
///   &lt;Color x:Key="TK_ErrorPage_Background" … /&gt;   page / modal background
///   &lt;Color x:Key="TK_ErrorPage_Accent"     … /&gt;   primary brand accent
///   &lt;Color x:Key="TK_ErrorPage_OnAccent"   … /&gt;   text drawn ON the accent button
///   &lt;Color x:Key="TK_ErrorPage_Surface"    … /&gt;   detail-box / input background
///   &lt;Color x:Key="TK_ErrorPage_OnSurface"  … /&gt;   text inside the detail box
///   &lt;Color x:Key="TK_ErrorPage_Muted"      … /&gt;   secondary labels, trace, icons
/// </code>
///
/// All other colors are derived from these six — no per-app subclass needed.
/// </summary>
public sealed class ResourceDictionaryErrorPageTheme : IErrorPageTheme
{
    // ── Key names (contract between toolkit and consuming app) ───────────────
    public const string KeyBackground = "TK_ErrorPage_Background";
    public const string KeyAccent     = "TK_ErrorPage_Accent";
    public const string KeyOnAccent   = "TK_ErrorPage_OnAccent";
    public const string KeySurface    = "TK_ErrorPage_Surface";
    public const string KeyOnSurface  = "TK_ErrorPage_OnSurface";
    public const string KeyMuted      = "TK_ErrorPage_Muted";

    // ── Semantic palette (lazy — read at the moment the page is shown) ───────
    private Color Background => Resolve(KeyBackground, "#0D0D0F");
    private Color Accent     => Resolve(KeyAccent,     "#BBFF00");
    private Color OnAccent   => Resolve(KeyOnAccent,   "#131315");
    private Color Surface    => Resolve(KeySurface,    "#0B0B0D");
    private Color OnSurface  => Resolve(KeyOnSurface,  "#FFFFFF");
    private Color Muted      => Resolve(KeyMuted,      "#94A3B8");

    // ── IErrorPageTheme ──────────────────────────────────────────────────────
    public Color PageBackground           => Background;
    public Color WarningIconColor         => Color.FromArgb("#F59E0B"); // amber — universal
    public Color TitleColor               => Colors.White;
    public Color SummaryColor             => OnSurface;
    public Color TraceColor               => Muted;
    public Color SectionLabelColor        => Muted;
    public Color CloseIconColor           => Muted;
    public Color DetailBoxBackground      => Surface;
    public Color DetailBoxBorder          => Muted.WithAlpha(0.35f);
    public Color DetailTextColor          => OnSurface.WithAlpha(0.85f);
    public Color InputBackground          => Surface;
    public Color InputBorderColor         => Accent;
    public Color InputTextColor           => Colors.White;
    public Color CopyButtonBackground     => Surface;
    public Color CopyButtonTextColor      => Accent;
    public Color PrimaryButtonBackground  => Accent;
    public Color PrimaryButtonTextColor   => OnAccent;
    public Color SecondaryButtonTextColor  => Accent;
    public Color SecondaryButtonBorderColor => Accent;
    public Color BrandingBackground       => Surface;
    public Color BrandingTextColor        => Muted.WithAlpha(0.55f);

    // ── Helper ───────────────────────────────────────────────────────────────
    private static Color Resolve(string key, string hexFallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is Color color)
            return color;

        return Color.FromArgb(hexFallback);
    }
}
