using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

using MyToolkit.Theme;

namespace MyToolkit.Controls;

/// <summary>M3 filter chip — outlined when off, SecondaryContainer-filled when on. Same look as the Search/Home chip rows.</summary>
/// <remarks>Generic, no app types, like <see cref="PillButton"/>.</remarks>
public sealed class FilterChip : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(FilterChip), string.Empty, propertyChanged: (b, _, v) => ((FilterChip)b)._label.Text = (string)v);

    public static readonly BindableProperty TrailingIconProperty = BindableProperty.Create(nameof(TrailingIcon), typeof(string), typeof(FilterChip), string.Empty, propertyChanged: (b, _, v) => ((FilterChip)b).SetTrailingIcon((string)v));

    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(nameof(IsSelected), typeof(bool), typeof(FilterChip), false, propertyChanged: (b, _, _) => ((FilterChip)b).ApplyTone());

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(FilterChip));

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(FilterChip));

    private readonly Border _chip;
    private readonly Label _label;
    private readonly Label _trailing;

    public FilterChip()
    {
        // One line, centred: the taller line height of a script like Arabic must not make its chip taller than the others.
        _label = new Label { FontSize = 14, MaxLines = 1, LineBreakMode = LineBreakMode.NoWrap, VerticalOptions = LayoutOptions.Center, VerticalTextAlignment = TextAlignment.Center };

        _trailing = new Label { FontFamily = "MaterialIcons", FontSize = 20, VerticalOptions = LayoutOptions.Center, IsVisible = false };

        _chip = new Border
        {
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(16, 0),
            HeightRequest = 36,
            MinimumHeightRequest = 36,
            MaximumHeightRequest = 36,
            VerticalOptions = LayoutOptions.Center,
            Content = new HorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center, Children = { _label, _trailing } },
        };

        // Owned by this control, same lifetime — nothing to unsubscribe.
        var tap = new TapGestureRecognizer();
        tap.Tapped += OnTapped;
        _chip.GestureRecognizers.Add(tap);

        Content = _chip;
        ApplyTone();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>A glyph after the label, e.g. a down arrow on a chip that opens a list.</summary>
    public string TrailingIcon
    {
        get => (string)GetValue(TrailingIconProperty);
        set => SetValue(TrailingIconProperty, value);
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

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private void SetTrailingIcon(string glyph)
    {
        _trailing.Text = glyph;
        _trailing.IsVisible = !string.IsNullOrEmpty(glyph);
    }

    private void ApplyTone()
    {
        if (IsSelected)
        {
            ThemeColors.Set(_chip, BackgroundColorProperty, "SecondaryContainer");
            ThemeColors.Set(_label, Label.TextColorProperty, "OnSecondaryContainer");
            ThemeColors.Set(_trailing, Label.TextColorProperty, "OnSecondaryContainer");
            // Same reason as below: drop the unselected state's Outline binding before the plain assignment.
            _chip.RemoveBinding(Border.StrokeProperty);
            _chip.Stroke = Colors.Transparent;
        }
        else
        {
            // RemoveBinding first: a plain assignment leaves the selected state's theme binding live, and it would come back on the next light/dark switch.
            _chip.RemoveBinding(BackgroundColorProperty);
            _chip.BackgroundColor = Colors.Transparent;
            ThemeColors.Set(_label, Label.TextColorProperty, "OnSurface");
            ThemeColors.Set(_trailing, Label.TextColorProperty, "OnSurface");
            ThemeColors.Set(_chip, Border.StrokeProperty, "Outline");
        }
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
    }
}
