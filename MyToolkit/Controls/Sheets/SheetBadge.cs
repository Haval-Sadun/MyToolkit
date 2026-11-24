using Microsoft.Maui.Controls.Shapes;

namespace MyToolkit.Controls.Sheets;

/// <summary>The tile at the start of a sheet row: a glyph or a few letters on the nested-panel color. Hidden when there is nothing to show.</summary>
/// <remarks>Generic, no app types, like <see cref="PillButton"/>. Colors and fonts come from the popup palette (<c>BackgroundSecondaryColor</c>, <c>TextColor</c>, <c>AppSemiBoldFamily</c>).</remarks>
public sealed class SheetBadge : ContentView
{
    public static readonly BindableProperty BadgeProperty = BindableProperty.Create(nameof(Badge), typeof(string), typeof(SheetBadge), string.Empty, propertyChanged: (b, _, _) => ((SheetBadge)b).Refresh());

    public static readonly BindableProperty IsIconProperty = BindableProperty.Create(nameof(IsIcon), typeof(bool), typeof(SheetBadge), false, propertyChanged: (b, _, _) => ((SheetBadge)b).Refresh());

    /// <summary>The tile's side in dp.</summary>
    public static readonly BindableProperty SizeProperty = BindableProperty.Create(nameof(Size), typeof(double), typeof(SheetBadge), 40d, propertyChanged: (b, _, v) => ((SheetBadge)b).Resize((double)v));

    private readonly Border _tile;
    private readonly Label _glyph;
    private readonly Label _letters;

    public SheetBadge()
    {
        _glyph = new Label { FontFamily = "MaterialIcons", FontSize = 22, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        _glyph.SetDynamicResource(Label.TextColorProperty, "TextSecondaryColor");

        _letters = new Label { FontSize = 13, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        _letters.SetDynamicResource(Label.FontFamilyProperty, "AppSemiBoldFamily");
        _letters.SetDynamicResource(Label.TextColorProperty, "TextColor");

        _tile = new Border
        {
            StrokeThickness = 0,
            VerticalOptions = LayoutOptions.Center,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Content = new Grid { Children = { _glyph, _letters } },
        };
        _tile.SetDynamicResource(BackgroundColorProperty, "BackgroundSecondaryColor");

        Content = _tile;
        Resize(Size);
        Refresh();
    }

    public string Badge
    {
        get => (string)GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    public bool IsIcon
    {
        get => (bool)GetValue(IsIconProperty);
        set => SetValue(IsIconProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    private void Refresh()
    {
        _glyph.Text = Badge;
        _letters.Text = Badge;
        _glyph.IsVisible = IsIcon;
        _letters.IsVisible = !IsIcon;
        IsVisible = !string.IsNullOrEmpty(Badge);
    }

    private void Resize(double size)
    {
        _tile.WidthRequest = size;
        _tile.HeightRequest = size;
        _glyph.FontSize = size * 0.55;
    }
}
