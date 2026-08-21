using UXDivers.Popups.Maui.Controls;

namespace MyToolkit.Services.Popups;

/// <summary>
/// Wires <c>UXDivers.Popups.Maui</c>'s own popup styling into the app and keeps it in sync with
/// the app's own palette. Call <see cref="Initialize"/> once, from the consuming app's
/// <c>App()</c> constructor, right after <c>InitializeComponent()</c>:
///
/// <code>
///     public App()
///     {
///         InitializeComponent();
///         PopupTheme.Initialize(this, new PopupPalette
///         {
///             Background = "SurfaceContainer", Text = "OnSurface", Accent = "Seed", ...
///         });
///         ...
///     }
/// </code>
///
/// That is the entire per-app popup setup: no <c>xmlns:uxd</c>, no merging
/// <c>uxd:DarkTheme</c>/<c>uxd:PopupStyles</c> by hand, no hand-written theme-sync method, no raw
/// UXDivers colour/font keys anywhere in the app's own XAML — and, critically, no colour value is
/// ever copy-pasted: the <see cref="PopupPalette"/> passed to <see cref="Initialize"/> only names
/// which of the app's own existing resource keys to reuse for each popup role, so the popup
/// palette always mirrors Colors.xaml and can never silently drift out of sync with it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the dictionary merge exists.</b> UXDivers.Popups.Maui ships its popup control styles as
/// two resource dictionaries (<see cref="DarkTheme"/>, <see cref="PopupStyles"/>) that every
/// consuming app has to merge itself. Forget it — or merge them under the wrong precedence — and
/// every popup type (Toast, ConfirmAsync, OptionSheetAsync, FormAsync, ...) has no style applied
/// at all, so its <c>PopupPage</c> never gets a visual child. UXDivers' own
/// <c>NativePopupManager</c> then throws
/// <c>NullReferenceException: "Popup content could not be converted to a native view"</c> the
/// instant any popup is shown — found by disassembling <c>ShowPlatformNativeViewAsync</c> (no
/// source ships with the NuGet package): it reads <c>PopupPage.ActualContent</c>, which resolves
/// via the templated content those two dictionaries supply. <see cref="Initialize"/> merges both,
/// exactly once, so no consuming app can forget it — this was ZagrosTune's exact crash the first
/// time any popup was shown.
/// </para>
/// <para>
/// <b>Why the palette is a name-mapping, not a colour copy.</b> UXDivers ships only a dark
/// palette and reads it back through a fixed, generic set of literal keys (<c>BackgroundColor</c>,
/// <c>TextColor</c>, ...) that collide easily with an app's own unrelated keys of the same name.
/// An earlier version of this had each app duplicate its own colour values under new
/// <c>TK_Popup_*</c> keys to avoid that collision — but a duplicated value is a second source of
/// truth that silently goes stale the moment Colors.xaml changes without a matching edit here.
/// <see cref="PopupPalette"/> instead only points at an EXISTING key by name; resolution happens
/// live, every time the theme is (re-)applied, straight out of the app's real resources. Per
/// colour role, in priority order: an explicit <c>TK_Popup_{Role}Dark</c>/<c>Light</c> escape
/// hatch (for a one-off colour that has no existing named resource — rare), then the alias
/// resolved via <see cref="PopupPalette"/> at <c>"{BaseName}Dark"</c>/<c>"{BaseName}Light"</c>
/// (falling back to the bare <c>"{BaseName}"</c> for a single-theme app or non-swapping brand
/// colour), then — if nothing applies — UXDivers' own shipped default, untouched.
/// </para>
/// <para>The full role table — resolves to this literal UXDivers-read key:</para>
/// <code>
///   Background        -> BackgroundColor           popup card
///   SurfaceSecondary   -> BackgroundSecondaryColor  nested panels / list rows
///   SurfaceTertiary    -> BackgroundTertiaryColor   further-nested surfaces
///   Border             -> PopupBorderColor          popup outline
///   Backdrop           -> PopupBackdropColor        dimmed screen behind the popup
///   Text               -> TextColor                 primary text
///   TextSecondary      -> TextSecondaryColor        secondary text
///   TextTertiary       -> TextTertiaryColor         muted text / icons
///   Placeholder        -> EntryPlaceholderColor     FormAsync field placeholders
///   Accent             -> PrimaryColor              action button (brand colour — usually bare, not themed)
///   AccentVariant      -> PrimaryVariantColor        pressed/variant shade of the above
///   Font               -> AppFontFamily             body text font (literal value, not aliased)
///   FontSemibold       -> AppSemiBoldFamily          title / emphasis font (literal value)
///   IconFont           -> IconsFontFamily            icon glyph font (literal value)
///   CloseIcon          -> UXDPopupsCloseIconButton   close-button glyph (literal value)
///   CheckIcon          -> UXDPopupsCheckCircleIconButton  confirm-button glyph (literal value)
/// </code>
/// </remarks>
public static class PopupTheme
{
    private const string OverridePrefix = "TK_Popup_";

    /// <summary>(role, literal UXDivers key, palette accessor) for every themeable colour.</summary>
    private static readonly (string Role, string UxDiversKey, Func<PopupPalette, string?> Alias)[] ColorRoles =
    [
        ("Background", "BackgroundColor", p => p.Background),
        ("SurfaceSecondary", "BackgroundSecondaryColor", p => p.SurfaceSecondary),
        ("SurfaceTertiary", "BackgroundTertiaryColor", p => p.SurfaceTertiary),
        ("Border", "PopupBorderColor", p => p.Border),
        ("Backdrop", "PopupBackdropColor", p => p.Backdrop),
        ("Text", "TextColor", p => p.Text),
        ("TextSecondary", "TextSecondaryColor", p => p.TextSecondary),
        ("TextTertiary", "TextTertiaryColor", p => p.TextTertiary),
        ("Placeholder", "EntryPlaceholderColor", p => p.Placeholder),
        ("Accent", "PrimaryColor", p => p.Accent),
        ("AccentVariant", "PrimaryVariantColor", p => p.AccentVariant),
    ];

    /// <summary>(role, literal UXDivers key, palette accessor) for the non-themed font/glyph keys
    /// — the palette value here is used as-is, not resolved as an alias.</summary>
    private static readonly (string Role, string UxDiversKey, Func<PopupPalette, string?> Value)[] StringRoles =
    [
        ("Font", "AppFontFamily", p => p.Font),
        ("FontSemibold", "AppSemiBoldFamily", p => p.FontSemibold),
        ("IconFont", "IconsFontFamily", p => p.IconFont),
        ("CloseIcon", "UXDPopupsCloseIconButton", p => p.CloseIcon),
        ("CheckIcon", "UXDPopupsCheckCircleIconButton", p => p.CheckIcon),
    ];

    /// <summary>
    /// Merges UXDivers' popup style dictionaries into <paramref name="app"/>, applies
    /// <paramref name="palette"/> for the current OS theme, and keeps it in sync as the theme
    /// changes. Call once, from <c>App()</c>, after <c>InitializeComponent()</c> (so the app's own
    /// resources — including any <c>TK_Popup_*</c> escape-hatch overrides — are already merged and
    /// resolvable). <paramref name="palette"/> may be omitted entirely to accept UXDivers' own
    /// dark-only defaults untouched.
    /// </summary>
    public static void Initialize(Application app, PopupPalette? palette = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        palette ??= new PopupPalette();

        app.Resources.MergedDictionaries.Add(new DarkTheme());
        app.Resources.MergedDictionaries.Add(new PopupStyles());

        Apply(app, palette, app.RequestedTheme);
        app.RequestedThemeChanged += (_, e) =>
            MainThread.BeginInvokeOnMainThread(() => Apply(app, palette, e.RequestedTheme));
    }

    private static void Apply(Application app, PopupPalette palette, AppTheme theme)
    {
        var themeSuffix = theme == AppTheme.Dark ? "Dark" : "Light";

        foreach (var (role, uxDiversKey, alias) in ColorRoles)
        {
            if (TryResolveColor(app, role, alias(palette), themeSuffix, out var color))
                app.Resources[uxDiversKey] = color;
        }

        foreach (var (role, uxDiversKey, value) in StringRoles)
        {
            // Escape hatch first, then the literal palette value itself.
            if (app.Resources.TryGetValue($"{OverridePrefix}{role}", out var over) && over is string overrideValue)
                app.Resources[uxDiversKey] = overrideValue;
            else if (value(palette) is { } literal)
                app.Resources[uxDiversKey] = literal;
        }
    }

    /// <summary>Resolution order: the <c>TK_Popup_{role}{themeSuffix}</c> escape hatch, then the
    /// theme-suffixed alias (<c>"{aliasBaseName}{themeSuffix}"</c>), then the bare alias
    /// (<c>"{aliasBaseName}"</c> — covers a single-theme app or a non-swapping brand colour), then
    /// "not defined" (caller keeps whatever default already applies).</summary>
    private static bool TryResolveColor(
        Application app, string role, string? aliasBaseName, string themeSuffix, out Color value)
    {
        if (app.Resources.TryGetValue($"{OverridePrefix}{role}{themeSuffix}", out var overThemed)
            && overThemed is Color overThemedColor)
        {
            value = overThemedColor;
            return true;
        }

        if (app.Resources.TryGetValue($"{OverridePrefix}{role}", out var over) && over is Color overColor)
        {
            value = overColor;
            return true;
        }

        if (aliasBaseName is not null)
        {
            if (app.Resources.TryGetValue($"{aliasBaseName}{themeSuffix}", out var themed) && themed is Color themedColor)
            {
                value = themedColor;
                return true;
            }

            if (app.Resources.TryGetValue(aliasBaseName, out var bare) && bare is Color bareColor)
            {
                value = bareColor;
                return true;
            }
        }

        value = null!;
        return false;
    }
}
