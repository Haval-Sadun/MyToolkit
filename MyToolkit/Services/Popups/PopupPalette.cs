namespace MyToolkit.Services.Popups;

/// <summary>
/// Declares, once, which of the app's OWN existing resources play each semantic popup role — see
/// <see cref="PopupTheme"/>'s doc comment for the full picture. Every property is optional; leave
/// a role unset to keep whichever default already applies to it (UXDivers' own, or a
/// <c>TK_Popup_*</c> XAML override — see <see cref="PopupTheme"/>).
/// </summary>
/// <remarks>
/// For every <b>colour</b> property, pass the base name of a resource key the app already has —
/// not a colour value, and not a full key name. <see cref="PopupTheme"/> resolves
/// <c>"{BaseName}Dark"</c> / <c>"{BaseName}Light"</c> against the app's resources for the current
/// OS theme (falling back to the bare <c>"{BaseName}"</c> for a single-theme app or a brand colour
/// that never swaps). This is the whole point: the popup palette is never a second copy of a
/// colour value anywhere — it always mirrors whatever Colors.xaml currently says, so it cannot
/// drift out of sync the way a hand-copied hex value can.
///
/// <code>
///     PopupTheme.Initialize(this, new PopupPalette
///     {
///         Background       = "SurfaceContainer",      // -&gt; SurfaceContainerDark / SurfaceContainerLight
///         SurfaceSecondary = "SurfaceContainerHigh",
///         SurfaceTertiary  = "SurfaceVariant",
///         Border           = "OutlineVariant",
///         Text             = "OnSurface",
///         TextSecondary    = "OnSurfaceVariant",
///         TextTertiary     = "Outline",
///         Placeholder      = "Outline",
///         Accent           = "Seed",                  // bare key, no Light/Dark suffix — resolved as-is
///         AccentVariant    = "PrimaryLight",
///         Font             = "OpenSansRegular",
///         FontSemibold     = "OpenSansSemibold",
///         IconFont         = "MaterialIcons",
///         CloseIcon        = "",
///         CheckIcon        = "",
///     });
/// </code>
///
/// For the <b>font/icon</b> properties there is no existing-resource lookup — pass the literal
/// value directly (a registered font alias, or a glyph character).
/// </remarks>
public sealed class PopupPalette
{
    // ── Colours — base name of an existing app resource key ─────────────────────────────
    public string? Background { get; init; }
    public string? SurfaceSecondary { get; init; }
    public string? SurfaceTertiary { get; init; }
    public string? Border { get; init; }
    public string? Backdrop { get; init; }
    public string? Text { get; init; }
    public string? TextSecondary { get; init; }
    public string? TextTertiary { get; init; }
    public string? Placeholder { get; init; }
    public string? Accent { get; init; }
    public string? AccentVariant { get; init; }

    // ── Fonts / icon glyphs — the literal value itself ───────────────────────────────────
    public string? Font { get; init; }
    public string? FontSemibold { get; init; }
    public string? IconFont { get; init; }
    public string? CloseIcon { get; init; }
    public string? CheckIcon { get; init; }
}
