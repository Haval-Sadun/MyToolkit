using Microsoft.Maui.Controls.Shapes;
using UXDivers.Popups.Maui.Controls;

namespace MyToolkit.Services.Popups;

/// <summary>Shared building blocks for the Dialog and Sheet popups. Every colour and font is a dynamic resource, so the app palette and OS theme just work.</summary>
internal static class PopupKit
{
    public const string DangerKey = "PopupDangerColor";
    public const double CardRadius = 28;
    public const double ButtonHeight = 48;
    private const string DefaultInfoGlyph = "";
    private const string MaterialCheckGlyph = "";

    public static bool IsPhone => DeviceInfo.Idiom == DeviceIdiom.Phone;

    public enum ButtonKind
    {
        Primary,
        Danger,
        Ghost,
    }

    public static Label Text(string text, double size, bool semibold, string colorKey, TextAlignment align)
    {
        var label = new Label
        {
            Text = text,
            FontSize = size,
            HorizontalTextAlignment = align,
            LineBreakMode = LineBreakMode.WordWrap,
        };
        label.SetDynamicResource(Label.TextColorProperty, colorKey);
        label.SetDynamicResource(Label.FontFamilyProperty, semibold ? "AppSemiBoldFamily" : "AppFontFamily");
        return label;
    }

    public static Label Glyph(string glyph, double size, string colorKey, Color? color = null)
    {
        var label = new Label
        {
            Text = glyph,
            FontSize = size,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
        };
        if (color is null)
            label.SetDynamicResource(Label.TextColorProperty, colorKey);
        else
            label.TextColor = color;

        label.SetDynamicResource(Label.FontFamilyProperty, "IconsFontFamily");
        return label;
    }

    public static Button PillButton(string text, ButtonKind kind, Action onTap)
    {
        var button = new Button
        {
            Text = text,
            HeightRequest = ButtonHeight,
            CornerRadius = (int)(ButtonHeight / 2),
            FontSize = 16,
            Padding = new Thickness(16, 0),
            Command = new Command(onTap),
        };
        button.SetDynamicResource(Button.FontFamilyProperty, "AppSemiBoldFamily");
        if (kind == ButtonKind.Ghost)
        {
            button.BackgroundColor = Colors.Transparent;
            button.SetDynamicResource(Button.TextColorProperty, "TextColor");
            return button;
        }

        var fill = kind == ButtonKind.Danger ? DangerKey : "PrimaryColor";
        button.SetDynamicResource(VisualElement.BackgroundColorProperty, fill);
        button.TextColor = ContrastOn(fill);
        return button;
    }

    /// <summary>Tinted circle (accent at 15%) with the glyph on top; null when there is no glyph to show.</summary>
    public static View? IconBadge(string? glyph)
    {
        if (string.IsNullOrEmpty(glyph))
            return null;

        var circle = new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 28 }, Opacity = 0.15 };
        circle.SetDynamicResource(VisualElement.BackgroundColorProperty, "PrimaryColor");
        return new Grid
        {
            WidthRequest = 56,
            HeightRequest = 56,
            HorizontalOptions = LayoutOptions.Center,
            Children = { circle, Glyph(glyph, 28, "PrimaryColor") },
        };
    }

    /// <summary>The palette's info glyph for icon-less alerts; empty when the app configured no icon font.</summary>
    public static string? DefaultInfoIcon() => HasResource("IconsFontFamily") ? DefaultInfoGlyph : null;

    /// <summary>True when the option icon is the palette's check glyph (or Material's plain check).</summary>
    public static bool IsCheckGlyph(string? icon)
    {
        if (string.IsNullOrEmpty(icon))
            return false;

        return icon == MaterialCheckGlyph || (TryGet("UXDPopupsCheckCircleIconButton") is string check && icon == check);
    }

    public static View DragHandle()
    {
        var handle = new Border { WidthRequest = 34, HeightRequest = 4, StrokeThickness = 0, HorizontalOptions = LayoutOptions.Center, StrokeShape = new RoundRectangle { CornerRadius = 2 }, InputTransparent = true };
        handle.SetDynamicResource(VisualElement.BackgroundColorProperty, "TextTertiaryColor");
        return handle;
    }

    /// <summary>True when the option's icon is a real leading icon (a check glyph renders trailing, so it doesn't count).</summary>
    public static bool HasLeadingIcon(string? icon) => !string.IsNullOrEmpty(icon) && !IsCheckGlyph(icon);

    /// <summary>56 dp option row. A check icon becomes a trailing check in the accent colour on a tinted row.
    /// <paramref name="reserveIconSlot"/> keeps the leading icon column; without it the text left-aligns with the sheet title.</summary>
    public static View OptionRow(string text, string? icon, Color? iconColor, bool destructive, bool reserveIconSlot, Action onTap)
    {
        var isCheck = IsCheckGlyph(icon);
        var row = new Grid { HeightRequest = 56, BackgroundColor = Colors.Transparent };
        if (reserveIconSlot)
        {
            row.ColumnSpacing = 16;
            row.Padding = new Thickness(12, 0);
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }
        else
        {
            // The tint (selected row) reaches 12 dp past the text into the sheet's side padding so the text stays aligned with the title.
            row.Padding = isCheck ? new Thickness(12, 0) : default;
            row.Margin = isCheck ? new Thickness(-12, 0) : default;
        }

        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var first = reserveIconSlot ? 1 : 0;
        if (isCheck)
        {
            var tint = new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 16 }, Opacity = 0.12, InputTransparent = true };
            tint.SetDynamicResource(VisualElement.BackgroundColorProperty, "PrimaryColor");
            row.Add(tint);
            Grid.SetColumnSpan(tint, row.ColumnDefinitions.Count);
        }

        if (HasLeadingIcon(icon))
            row.Add(Glyph(icon!, 24, destructive ? DangerKey : "TextSecondaryColor", iconColor), 0);

        var label = Text(text, 16, isCheck, destructive ? DangerKey : "TextColor", TextAlignment.Start);
        label.VerticalTextAlignment = TextAlignment.Center;
        label.LineBreakMode = LineBreakMode.TailTruncation;
        row.Add(label, first);
        if (isCheck)
            row.Add(Glyph(icon!, 24, "PrimaryColor"), first + 1);

        row.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(onTap) });
        return row;
    }

    /// <summary>Rounded text field bound two-way to <paramref name="field"/>; the last one submits on the keyboard action.</summary>
    public static View FormEntry(FormField field, bool isLast, Action submit, out Entry input)
    {
        input = new Entry
        {
            Placeholder = field.Placeholder,
            IsPassword = field.IsPassword,
            BackgroundColor = Colors.Transparent,
            FontSize = 16,
            VerticalOptions = LayoutOptions.Center,
        };
        input.SetBinding(Entry.TextProperty, new Binding(nameof(FormField.Value), BindingMode.TwoWay, source: field));
        input.SetDynamicResource(Entry.TextColorProperty, "TextColor");
        input.SetDynamicResource(Entry.PlaceholderColorProperty, "EntryPlaceholderColor");
        input.SetDynamicResource(Entry.FontFamilyProperty, "AppFontFamily");
        if (isLast)
        {
            input.ReturnType = ReturnType.Done;
            input.ReturnCommand = new Command(submit);
        }

        var hasIcon = !string.IsNullOrEmpty(field.Icon);
        var row = new Grid { ColumnSpacing = hasIcon ? 12 : 0, Padding = new Thickness(16, 0), HeightRequest = 52, ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) } };
        if (hasIcon)
            row.Add(Glyph(field.Icon!, 22, "TextSecondaryColor", field.IconColor), 0);

        row.Add(input, 1);
        var box = new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 16 }, Content = row };
        box.SetDynamicResource(VisualElement.BackgroundColorProperty, "BackgroundSecondaryColor");
        return box;
    }

    public static Label GroupHeader(string name)
    {
        var label = Text(name, 13, true, "TextSecondaryColor", TextAlignment.Start);
        label.Margin = new Thickness(12, 8, 0, 0);
        return label;
    }

    /// <summary>System bar insets in dp on Android; default elsewhere or when unavailable.</summary>
    public static Thickness SystemBarsInsetsDp()
    {
#if ANDROID
        try
        {
            if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window?.DecorView is not { } decorView || decorView.RootWindowInsets is not { } rootInsets)
                return default;

            var compat = AndroidX.Core.View.WindowInsetsCompat.ToWindowInsetsCompat(rootInsets);
            if (compat?.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars()) is not { } sys)
                return default;

            var density = decorView.Resources?.DisplayMetrics?.Density ?? 1f;
            if (density <= 0)
                density = 1f;

            return new Thickness(sys.Left / density, sys.Top / density, sys.Right / density, sys.Bottom / density);
        }
        catch
        {
            return default;
        }
#else
        return default;
#endif
    }

    /// <summary>Black or white, whichever reads better on the resolved colour of <paramref name="key"/>.</summary>
    private static Color ContrastOn(string key)
    {
        if (TryGet(key) is not Color color)
            return Colors.White;

        var luminance = 0.2126 * color.Red + 0.7152 * color.Green + 0.0722 * color.Blue;
        return luminance > 0.6 ? Colors.Black : Colors.White;
    }

    private static bool HasResource(string key) => TryGet(key) is not null;

    private static object? TryGet(string key) => Application.Current is { } app && app.Resources.TryGetValue(key, out var value) ? value : null;
}
