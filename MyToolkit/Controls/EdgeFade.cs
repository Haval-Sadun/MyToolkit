using MyToolkit.Theme;

namespace MyToolkit.Controls;

public enum FadeEdge
{
    Top,
    Bottom,
}

/// <summary>A strip that fades a scrolling view's top or bottom edge into a palette surface (instead of a hard crop). Place it over the view; it never takes touches.</summary>
/// <remarks>The transparent end is the same colour at alpha 0, so the fade never passes through grey. Follows light/dark; the theme subscription lives only while the control has a handler.</remarks>
public sealed class EdgeFade : BoxView
{
    public static readonly BindableProperty EdgeProperty = BindableProperty.Create(nameof(Edge), typeof(FadeEdge), typeof(EdgeFade), FadeEdge.Bottom, propertyChanged: (b, _, _) => ((EdgeFade)b).Apply());

    public static readonly BindableProperty SurfaceTokenProperty = BindableProperty.Create(nameof(SurfaceToken), typeof(string), typeof(EdgeFade), "Surface", propertyChanged: (b, _, _) => ((EdgeFade)b).Apply());

    public EdgeFade()
    {
        InputTransparent = true;
        Color = Colors.Transparent;
        HeightRequest = 40;
    }

    public FadeEdge Edge
    {
        get => (FadeEdge)GetValue(EdgeProperty);
        set => SetValue(EdgeProperty, value);
    }

    /// <summary>Palette token whose Light/Dark pair is the surface behind the view (default "Surface").</summary>
    public string SurfaceToken
    {
        get => (string)GetValue(SurfaceTokenProperty);
        set => SetValue(SurfaceTokenProperty, value);
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);
        if (args.OldHandler is not null && Application.Current is { } app)
            app.RequestedThemeChanged -= OnThemeChanged;

        if (args.NewHandler is not null && Application.Current is { } current)
        {
            current.RequestedThemeChanged += OnThemeChanged;
            Apply();
        }
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => Apply();

    private void Apply()
    {
        if (ThemeColors.Current(SurfaceToken) is not { } surface)
            return;

        var clear = surface.WithAlpha(0);
        Background = new LinearGradientBrush(
            Edge == FadeEdge.Top ? [new GradientStop(surface, 0), new GradientStop(clear, 1)] : [new GradientStop(clear, 0), new GradientStop(surface, 1)],
            new Point(0, 0),
            new Point(0, 1));
    }
}
