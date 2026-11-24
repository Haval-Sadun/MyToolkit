using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

using MyToolkit.Theme;

namespace MyToolkit.Controls;

/// <summary>Rounded pill with a leading glyph and a label — a toggle-able secondary action (e.g. Lyrics / Up next).
/// Selected pills use PrimaryContainer; it highlights while pressed and runs <see cref="Command"/> on tap.</summary>
/// <remarks>Generic, no app types.</remarks>
public sealed class PillButton : ContentView
{
    public static readonly BindableProperty IconProperty = BindableProperty.Create(nameof(Icon), typeof(string), typeof(PillButton), string.Empty, propertyChanged: (b, _, v) => ((PillButton)b).SetIcon((string)v));

    public static readonly BindableProperty TrailingIconProperty = BindableProperty.Create(nameof(TrailingIcon), typeof(string), typeof(PillButton), string.Empty, propertyChanged: (b, _, v) => ((PillButton)b).SetTrailingIcon((string)v));

    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(PillButton), string.Empty, propertyChanged: (b, _, v) => ((PillButton)b)._label.Text = (string)v);

    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(nameof(IsSelected), typeof(bool), typeof(PillButton), false, propertyChanged: (b, _, _) => ((PillButton)b).ApplyTone());

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(PillButton));

    private readonly Border _pill;
    private readonly Label _icon;
    private readonly Label _trailing;
    private readonly Label _label;

    public PillButton()
    {
        _icon = new Label { FontFamily = "MaterialIcons", FontSize = 18, VerticalOptions = LayoutOptions.Center, IsVisible = false };
        _label = new Label { FontSize = 14, FontFamily = "OpenSansSemibold", VerticalOptions = LayoutOptions.Center };
        _trailing = new Label { FontFamily = "MaterialIcons", FontSize = 20, VerticalOptions = LayoutOptions.Center, IsVisible = false };

        _pill = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            Padding = new Thickness(16, 10),
            Content = new HorizontalStackLayout { Spacing = 8, Children = { _icon, _label, _trailing } },
        };

        // Owned by this control, same lifetime — nothing to unsubscribe.
        var tap = new TapGestureRecognizer();
        tap.Tapped += OnTapped;
        _pill.GestureRecognizers.Add(tap);

        var pointer = new PointerGestureRecognizer();
        pointer.PointerPressed += (_, _) => Press(true);
        pointer.PointerReleased += (_, _) => Press(false);
        pointer.PointerExited += (_, _) => Press(false);
        _pill.GestureRecognizers.Add(pointer);

        Content = _pill;
        ApplyTone();
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>A glyph after the label, e.g. a down arrow on a pill that opens a list.</summary>
    public string TrailingIcon
    {
        get => (string)GetValue(TrailingIconProperty);
        set => SetValue(TrailingIconProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    // Text-only pills drop the icon, so the stack's spacing doesn't leave a gap.
    private void SetIcon(string glyph)
    {
        _icon.Text = glyph;
        _icon.IsVisible = !string.IsNullOrEmpty(glyph);
    }

    private void SetTrailingIcon(string glyph)
    {
        _trailing.Text = glyph;
        _trailing.IsVisible = !string.IsNullOrEmpty(glyph);
    }

    private void ApplyTone()
    {
        var (background, foreground) = IsSelected ? ("PrimaryContainer", "OnPrimaryContainer") : ("SurfaceContainerHigh", "OnSurface");
        ThemeColors.Set(_pill, BackgroundColorProperty, background);
        ThemeColors.Set(_icon, Label.TextColorProperty, foreground);
        ThemeColors.Set(_label, Label.TextColorProperty, foreground);
        ThemeColors.Set(_trailing, Label.TextColorProperty, foreground);
    }

    // Dims and shrinks under the finger, springs back on release; skipped when system animations are off.
    private void Press(bool down)
    {
        _pill.Opacity = down ? 0.7 : 1;
        if (Motion.Reduced)
            return;

        _pill.CancelAnimations();
        _ = down ? _pill.ScaleToAsync(0.94, 70, Easing.CubicOut) : _pill.ScaleToAsync(1, 180, Easing.SpringOut);
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        Press(false);
        if (Command?.CanExecute(null) == true)
            Command.Execute(null);
    }
}
