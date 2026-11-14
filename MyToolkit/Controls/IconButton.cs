using MyToolkit.Theme;

namespace MyToolkit.Controls;

/// <summary>Font-glyph button with visible press feedback (dim + shrink) — replaces Label + TapGestureRecognizer, which gives none.</summary>
/// <remarks>An ImageButton, so it also gets a real button role, keyboard focus and Command-driven IsEnabled. The press dim is relative and restored
/// afterwards, so callers can still dim via triggers. Generic, no app types.</remarks>
public class IconButton : ImageButton
{
    public static readonly BindableProperty GlyphProperty = BindableProperty.Create(nameof(Glyph), typeof(string), typeof(IconButton), string.Empty, propertyChanged: OnGlyphChanged);

    public static readonly BindableProperty GlyphSizeProperty = BindableProperty.Create(nameof(GlyphSize), typeof(double), typeof(IconButton), 24d, propertyChanged: OnGlyphChanged);

    public static readonly BindableProperty GlyphColorProperty = BindableProperty.Create(nameof(GlyphColor), typeof(Color), typeof(IconButton), null, propertyChanged: OnGlyphChanged);

    public static readonly BindableProperty GlyphFontFamilyProperty = BindableProperty.Create(nameof(GlyphFontFamily), typeof(string), typeof(IconButton), "MaterialIcons", propertyChanged: OnGlyphChanged);

    private const double PressedOpacityFactor = 0.55;
    private const double PressedScale = 0.85;

    // Not fully transparent: WinUI hit-tests a null/clear background only where the glyph has pixels, so taps between strokes fell through.
    // Never changed while pressed — on Windows a background swap mid-press rebuilds the button's brushes and the click is lost.
    private static readonly Color RestingBackground = Color.FromRgba(0, 0, 0, 1);

    private double _restingOpacity = 1;
    private double _pressedOpacity = -1;

    public IconButton()
    {
        BackgroundColor = RestingBackground;
        BorderWidth = 0;
        Padding = 0;
        // Center: draws the glyph at GlyphSize instead of stretching it to fill the button.
        Aspect = Aspect.Center;
        WidthRequest = 48;
        HeightRequest = 48;

        // Own events, same lifetime as this control — nothing to unsubscribe.
        Pressed += OnPressed;
        Released += OnReleased;
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public double GlyphSize
    {
        get => (double)GetValue(GlyphSizeProperty);
        set => SetValue(GlyphSizeProperty, value);
    }

    public Color? GlyphColor
    {
        get => (Color?)GetValue(GlyphColorProperty);
        set => SetValue(GlyphColorProperty, value);
    }

    public string GlyphFontFamily
    {
        get => (string)GetValue(GlyphFontFamilyProperty);
        set => SetValue(GlyphFontFamilyProperty, value);
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);
#if WINDOWS
        // WinUI bubbles a button's Tapped to ancestors, so a parent's TapGestureRecognizer (row/card/bar tap) would also fire.
        if (args.OldHandler?.PlatformView is Microsoft.UI.Xaml.UIElement oldView)
        {
            oldView.Tapped -= OnPlatformTapped;
            oldView.DoubleTapped -= OnPlatformDoubleTapped;
        }

        if (args.NewHandler?.PlatformView is Microsoft.UI.Xaml.UIElement newView)
        {
            newView.Tapped += OnPlatformTapped;
            newView.DoubleTapped += OnPlatformDoubleTapped;
        }
#endif
    }

#if WINDOWS
    private static void OnPlatformTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e) => e.Handled = true;

    private static void OnPlatformDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e) => e.Handled = true;
#endif

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        // Keeps the native hover highlight a circle at the requested size. Derived from the request, never from layout:
        // setting CornerRadius in OnSizeAllocated re-triggered layout and crashed WinUI with a LayoutCycleException.
        if (propertyName is nameof(WidthRequest) or nameof(HeightRequest) && WidthRequest > 0 && HeightRequest > 0)
            CornerRadius = (int)(Math.Min(WidthRequest, HeightRequest) / 2);
    }

    private static void OnGlyphChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var button = (IconButton)bindable;
        button.Source = string.IsNullOrEmpty(button.Glyph)
            ? null
            : new FontImageSource { Glyph = button.Glyph, FontFamily = button.GlyphFontFamily, Size = button.GlyphSize, Color = button.GlyphColor };
    }

    private void OnPressed(object? sender, EventArgs e)
    {
        this.AbortAnimation("FadeTo");
        _restingOpacity = Opacity;
        _pressedOpacity = _restingOpacity * PressedOpacityFactor;
        Opacity = _pressedOpacity;
        _ = this.ScaleToAsync(PressedScale, 70, Easing.CubicOut);
    }

    private void OnReleased(object? sender, EventArgs e)
    {
        _ = this.ScaleToAsync(1, 140, Easing.CubicOut);

        // Only undo our own dim — if a trigger changed Opacity meanwhile, that value wins.
        if (Math.Abs(Opacity - _pressedOpacity) < 0.001)
            _ = this.FadeToAsync(_restingOpacity, 140, Easing.CubicOut);
    }
}
