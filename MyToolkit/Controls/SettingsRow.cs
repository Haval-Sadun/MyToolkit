using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

using MyToolkit.Theme;

namespace MyToolkit.Controls;

/// <summary>One settings list row: tinted icon tile, title + supporting text, optional trailing value, color swatch and chevron.
/// Highlights while pressed and runs <see cref="Command"/> on tap.</summary>
/// <remarks>Destructive rows (clear/delete) tint the icon and title with the Error color. Generic, no app types.</remarks>
public sealed class SettingsRow : ContentView
{
    public static readonly BindableProperty IconProperty = BindableProperty.Create(nameof(Icon), typeof(string), typeof(SettingsRow), string.Empty, propertyChanged: (b, _, v) => ((SettingsRow)b)._icon.Text = (string)v);

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(SettingsRow), string.Empty, propertyChanged: (b, _, v) => ((SettingsRow)b)._title.Text = (string)v);

    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(SettingsRow), null, propertyChanged: (b, _, v) => ((SettingsRow)b).UpdateSubtitle((string?)v));

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(string), typeof(SettingsRow), null, propertyChanged: (b, _, v) => ((SettingsRow)b).UpdateValue((string?)v));

    public static readonly BindableProperty SwatchColorProperty = BindableProperty.Create(nameof(SwatchColor), typeof(Color), typeof(SettingsRow), null, propertyChanged: (b, _, v) => ((SettingsRow)b).UpdateSwatch((Color?)v));

    public static readonly BindableProperty ShowChevronProperty = BindableProperty.Create(nameof(ShowChevron), typeof(bool), typeof(SettingsRow), false, propertyChanged: (b, _, v) => ((SettingsRow)b)._chevron.IsVisible = (bool)v);

    /// <summary>With <see cref="ShowChevron"/>: the row opens a bottom sheet, so its arrow points down instead of to the side.</summary>
    public static readonly BindableProperty OpensSheetProperty = BindableProperty.Create(nameof(OpensSheet), typeof(bool), typeof(SettingsRow), false, propertyChanged: (b, _, _) => ((SettingsRow)b).UpdateChevron());

    /// <summary>The row is the one open in a master-detail layout: it keeps a tinted background.</summary>
    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(nameof(IsSelected), typeof(bool), typeof(SettingsRow), false, propertyChanged: (b, _, _) => ((SettingsRow)b).ApplySelection());

    public static readonly BindableProperty IsDestructiveProperty = BindableProperty.Create(nameof(IsDestructive), typeof(bool), typeof(SettingsRow), false, propertyChanged: (b, _, _) => ((SettingsRow)b).ApplyTone());

    public static readonly BindableProperty ShowSwitchProperty = BindableProperty.Create(nameof(ShowSwitch), typeof(bool), typeof(SettingsRow), false, propertyChanged: (b, _, v) => ((SettingsRow)b)._switch.IsVisible = (bool)v);

    public static readonly BindableProperty IsToggledProperty = BindableProperty.Create(nameof(IsToggled), typeof(bool), typeof(SettingsRow), false, BindingMode.TwoWay, propertyChanged: (b, _, v) => ((SettingsRow)b).SyncSwitch((bool)v));

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SettingsRow));

    private const string ChevronRight = "";
    /// <summary>The arrow of a row that opens a bottom sheet: down, since the sheet rises over the page.</summary>
    private const string ChevronDown = "";

    private const string ChevronLeft = "";

    private readonly Grid _root;
    private readonly Border _iconTile;
    private readonly Label _icon;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _value;
    private readonly Border _swatch;
    private readonly Label _chevron;
    private readonly Switch _switch;

    // True while the switch is being set from the bound value, so that does not echo back as a user toggle.
    private bool _syncing;

    // Points the way the row opens: left under a right-to-left layout. Read from the effective direction, so no subscription is needed.
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        UpdateChevron();
    }

    private void UpdateChevron() => _chevron.Text = OpensSheet ? ChevronDown : ((IVisualElementController)this).EffectiveFlowDirection.HasFlag(EffectiveFlowDirection.RightToLeft) ? ChevronLeft : ChevronRight;

    public SettingsRow()
    {
        _icon = new Label { FontFamily = "MaterialIcons", FontSize = 22, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        _iconTile = new Border
        {
            WidthRequest = 40,
            HeightRequest = 40,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            VerticalOptions = LayoutOptions.Center,
            Content = _icon,
        };

        _title = new Label { Style = ThemeColors.Style("BodyLarge") };
        _subtitle = new Label { Style = ThemeColors.Style("BodyMedium"), MaxLines = 2, IsVisible = false };
        var texts = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { _title, _subtitle } };

        _value = new Label { Style = ThemeColors.Style("BodyMedium"), VerticalOptions = LayoutOptions.Center, IsVisible = false };
        _swatch = new Border
        {
            WidthRequest = 18,
            HeightRequest = 18,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 5 },
            VerticalOptions = LayoutOptions.Center,
            IsVisible = false,
        };
        _chevron = new Label { Text = ChevronRight, FontFamily = "MaterialIcons", FontSize = 22, VerticalOptions = LayoutOptions.Center, IsVisible = false };
        ThemeColors.Set(_chevron, Label.TextColorProperty, "OnSurfaceVariant");

        _switch = new Switch { VerticalOptions = LayoutOptions.Center, IsVisible = false };
        _switch.Style = ThemeColors.Style("AppSwitchStyle");
        _switch.Toggled += OnSwitchToggled;

        var trailing = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center, Children = { _value, _swatch, _chevron, _switch } };

        _root = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 16,
            Padding = new Thickness(16, 12),
            MinimumHeightRequest = 64,
            // Transparent, not null: a null background isn't hit-testable between the labels on Windows.
            BackgroundColor = Colors.Transparent,
        };
        _root.Add(_iconTile, 0);
        _root.Add(texts, 1);
        _root.Add(trailing, 2);

        // Owned by this row, same lifetime — nothing to unsubscribe.
        var tap = new TapGestureRecognizer();
        tap.Tapped += OnTapped;
        _root.GestureRecognizers.Add(tap);

        var pointer = new PointerGestureRecognizer();
        pointer.PointerPressed += OnPointerPressed;
        pointer.PointerReleased += OnPointerUp;
        pointer.PointerExited += OnPointerUp;
        _root.GestureRecognizers.Add(pointer);

        Content = _root;
        ApplyTone();
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => (string?)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string? Value
    {
        get => (string?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Color? SwatchColor
    {
        get => (Color?)GetValue(SwatchColorProperty);
        set => SetValue(SwatchColorProperty, value);
    }

    public bool ShowChevron
    {
        get => (bool)GetValue(ShowChevronProperty);
        set => SetValue(ShowChevronProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool OpensSheet
    {
        get => (bool)GetValue(OpensSheetProperty);
        set => SetValue(OpensSheetProperty, value);
    }

    public bool IsDestructive
    {
        get => (bool)GetValue(IsDestructiveProperty);
        set => SetValue(IsDestructiveProperty, value);
    }

    /// <summary>Shows a trailing switch instead of a value; tapping the row toggles it too.</summary>
    public bool ShowSwitch
    {
        get => (bool)GetValue(ShowSwitchProperty);
        set => SetValue(ShowSwitchProperty, value);
    }

    public bool IsToggled
    {
        get => (bool)GetValue(IsToggledProperty);
        set => SetValue(IsToggledProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    private void SyncSwitch(bool value)
    {
        _syncing = true;
        _switch.IsToggled = value;
        _syncing = false;
    }

    private void OnSwitchToggled(object? sender, ToggledEventArgs e)
    {
        if (_syncing)
            return;

        IsToggled = e.Value;
    }

    private void UpdateSubtitle(string? text)
    {
        _subtitle.Text = text;
        _subtitle.IsVisible = !string.IsNullOrEmpty(text);
    }

    private void UpdateValue(string? text)
    {
        _value.Text = text;
        _value.IsVisible = !string.IsNullOrEmpty(text);
    }

    private void UpdateSwatch(Color? color)
    {
        _swatch.BackgroundColor = color;
        _swatch.IsVisible = color is not null;
    }

    /// <summary>Normal rows: PrimaryContainer tile. Destructive rows: a faint Error tile, Error glyph and title.</summary>
    private void ApplyTone()
    {
        if (IsDestructive)
        {
            ThemeColors.Set(_icon, Label.TextColorProperty, "Error");
            ThemeColors.Set(_title, Label.TextColorProperty, "Error");
            if (ThemeColors.Get("ErrorLight") is { } light && ThemeColors.Get("ErrorDark") is { } dark)
                _iconTile.SetAppThemeColor(BackgroundColorProperty, light.WithAlpha(0.12f), dark.WithAlpha(0.16f));
            return;
        }

        ThemeColors.Set(_iconTile, BackgroundColorProperty, "PrimaryContainer");
        ThemeColors.Set(_icon, Label.TextColorProperty, "OnPrimaryContainer");
        ThemeColors.Set(_title, Label.TextColorProperty, "OnSurface");
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if (ThemeColors.Current("OnSurface") is { } onSurface)
            _root.BackgroundColor = onSurface.WithAlpha(0.08f);
    }

    private void OnPointerUp(object? sender, PointerEventArgs e) => ApplySelection();

    // The resting background: tinted while selected (following light and dark), otherwise clear. RemoveBinding first, as FilterChip does, or the tint would come back on the next theme switch.
    private void ApplySelection()
    {
        if (IsSelected && ThemeColors.Get("SecondaryContainerLight") is { } light && ThemeColors.Get("SecondaryContainerDark") is { } dark)
        {
            _root.SetAppThemeColor(BackgroundColorProperty, light, dark);
            return;
        }

        _root.RemoveBinding(BackgroundColorProperty);
        _root.BackgroundColor = Colors.Transparent;
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        ApplySelection();
        if (ShowSwitch)
        {
            _switch.IsToggled = !_switch.IsToggled;
            return;
        }

        if (Command?.CanExecute(null) == true)
            Command.Execute(null);
    }
}
