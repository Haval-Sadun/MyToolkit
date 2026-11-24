using Microsoft.Maui.Controls.Shapes;

namespace MyToolkit.Controls.Sheets;

/// <summary>The round tick at the end of a sheet row: an empty ring while off, filled with the accent and a check while on.</summary>
/// <remarks>Generic, no app types. Colors come from the popup palette (<c>PopupBorderColor</c>, <c>PrimaryColor</c>); the check is the text color on that accent.</remarks>
public sealed class SheetCheck : ContentView
{
    public static readonly BindableProperty IsCheckedProperty = BindableProperty.Create(nameof(IsChecked), typeof(bool), typeof(SheetCheck), false, propertyChanged: (b, _, _) => ((SheetCheck)b).Refresh());

    private readonly Border _ring;
    private readonly Label _check;

    public SheetCheck()
    {
        _check = new Label { Text = SheetGlyphs.Check, FontFamily = "MaterialIcons", FontSize = 16, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, TextColor = SheetColors.OnPrimary() };

        _ring = new Border
        {
            WidthRequest = 24,
            HeightRequest = 24,
            StrokeThickness = 1.5,
            VerticalOptions = LayoutOptions.Center,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = _check,
        };

        Content = _ring;
        Refresh();
    }

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    private void Refresh()
    {
        _check.IsVisible = IsChecked;
        if (IsChecked)
        {
            _ring.SetDynamicResource(BackgroundColorProperty, "PrimaryColor");
            _ring.RemoveDynamicResource(Border.StrokeProperty);
            _ring.Stroke = Colors.Transparent;
            return;
        }

        _ring.RemoveDynamicResource(BackgroundColorProperty);
        _ring.BackgroundColor = Colors.Transparent;
        _ring.SetDynamicResource(Border.StrokeProperty, "PopupBorderColor");
    }
}
