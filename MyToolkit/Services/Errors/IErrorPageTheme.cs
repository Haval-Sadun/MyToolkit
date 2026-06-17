namespace MyToolkit.Services.Errors;

/// <summary>
/// Dynamic color contract for <c>ErrorReportPage</c>. Register a concrete implementation
/// as a DI singleton in the consuming app to inject brand colors.
/// When not registered, <see cref="DefaultErrorPageTheme"/> is used.
/// Brush properties are default implementations that wrap the matching Color property —
/// override them only if you need a non-solid brush.
/// </summary>
public interface IErrorPageTheme
{
    // ── Page / text ──────────────────────────────────────────────────────────
    Color PageBackground    { get; }
    Color WarningIconColor  { get; }
    Color TitleColor        { get; }
    Color SummaryColor      { get; }
    Color TraceColor        { get; }
    Color SectionLabelColor { get; }
    Color CloseIconColor    { get; }

    // ── Detail box ───────────────────────────────────────────────────────────
    Color DetailBoxBackground { get; }
    Color DetailBoxBorder     { get; }
    Color DetailTextColor     { get; }

    // ── User-description entry ───────────────────────────────────────────────
    Color InputBackground  { get; }
    Color InputBorderColor { get; }
    Color InputTextColor   { get; }

    // ── Buttons ──────────────────────────────────────────────────────────────
    Color CopyButtonBackground      { get; }
    Color CopyButtonTextColor       { get; }
    Color PrimaryButtonBackground   { get; }
    Color PrimaryButtonTextColor    { get; }
    Color SecondaryButtonTextColor  { get; }
    Color SecondaryButtonBorderColor { get; }

    // ── Branding badge ───────────────────────────────────────────────────────
    Color BrandingBackground { get; }
    Color BrandingTextColor  { get; }

    // ── Brush wrappers (for Border.Stroke bindings) ──────────────────────────
    SolidColorBrush DetailBoxBorderBrush => new(DetailBoxBorder);
    SolidColorBrush InputBorderBrush     => new(InputBorderColor);
}

/// <summary>Dark defaults — used when no <see cref="IErrorPageTheme"/> is registered.</summary>
public sealed class DefaultErrorPageTheme : IErrorPageTheme
{
    public Color PageBackground           => Color.FromArgb("#0D0D0F");
    public Color WarningIconColor         => Color.FromArgb("#F59E0B");
    public Color TitleColor               => Colors.White;
    public Color SummaryColor             => Color.FromArgb("#E2E8F0");
    public Color TraceColor               => Color.FromArgb("#94A3B8");
    public Color SectionLabelColor        => Color.FromArgb("#94A3B8");
    public Color CloseIconColor           => Color.FromArgb("#94A3B8");
    public Color DetailBoxBackground      => Color.FromArgb("#0B0B0D");
    public Color DetailBoxBorder          => Color.FromArgb("#1E293B");
    public Color DetailTextColor          => Color.FromArgb("#CBD5E1");
    public Color InputBackground          => Color.FromArgb("#1A1A1E");
    public Color InputBorderColor         => Color.FromArgb("#2D2D35");
    public Color InputTextColor           => Color.FromArgb("#E2E8F0");
    public Color CopyButtonBackground     => Color.FromArgb("#1E293B");
    public Color CopyButtonTextColor      => Color.FromArgb("#BBFF00");
    public Color PrimaryButtonBackground  => Color.FromArgb("#BBFF00");
    public Color PrimaryButtonTextColor   => Color.FromArgb("#131315");
    public Color SecondaryButtonTextColor  => Color.FromArgb("#BBFF00");
    public Color SecondaryButtonBorderColor => Color.FromArgb("#BBFF00");
    public Color BrandingBackground       => Color.FromArgb("#1A1A1E");
    public Color BrandingTextColor        => Color.FromArgb("#475569");
}
