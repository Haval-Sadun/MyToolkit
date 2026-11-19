using System.Collections;
using Microsoft.Maui.Controls.Shapes;
using MyToolkit.Theme;

namespace MyToolkit.Controls;

/// <summary>A row of bars drawn from plain shapes (no chart library): bind <see cref="Bars"/> to a list of <see cref="BarChartItem"/>. Scrolls sideways when there are more bars than fit; the bars follow the theme's Primary colour and the labels OnSurfaceVariant.</summary>
public sealed class BarChartView : ContentView
{
    public static readonly BindableProperty BarsProperty = BindableProperty.Create(nameof(Bars), typeof(IEnumerable<BarChartItem>), typeof(BarChartView), null,
        propertyChanged: (bindable, _, bars) => BindableLayout.SetItemsSource(((BarChartView)bindable)._row, (IEnumerable?)bars));

    public static readonly BindableProperty ChartHeightProperty = BindableProperty.Create(nameof(ChartHeight), typeof(double), typeof(BarChartView), 140d,
        propertyChanged: (bindable, _, height) => ((BarChartView)bindable)._row.HeightRequest = (double)height);

    private readonly HorizontalStackLayout _row = new() { Spacing = 3, HeightRequest = 140 };

    public BarChartView()
    {
        BindableLayout.SetItemTemplate(_row, new DataTemplate(BuildBar));
        Content = new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = _row };
    }

    public IEnumerable<BarChartItem>? Bars
    {
        get => (IEnumerable<BarChartItem>?)GetValue(BarsProperty);
        set => SetValue(BarsProperty, value);
    }

    /// <summary>The height of the whole chart, labels included; the tallest bar should be a little under it.</summary>
    public double ChartHeight
    {
        get => (double)GetValue(ChartHeightProperty);
        set => SetValue(ChartHeightProperty, value);
    }

    private static View BuildBar()
    {
        var bar = new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 4 }, VerticalOptions = LayoutOptions.End };
        ThemeColors.Set(bar, VisualElement.BackgroundColorProperty, "Primary");
        bar.SetBinding(VisualElement.HeightRequestProperty, static (BarChartItem item) => item.Height);
        bar.SetBinding(VisualElement.WidthRequestProperty, static (BarChartItem item) => item.Width);
        bar.SetBinding(VisualElement.OpacityProperty, static (BarChartItem item) => item.Opacity);

        var label = new Label { FontSize = 10, HeightRequest = 16, HorizontalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.NoWrap };
        ThemeColors.Set(label, Label.TextColorProperty, "OnSurfaceVariant");
        label.SetBinding(Label.TextProperty, static (BarChartItem item) => item.Label);
        label.SetBinding(VisualElement.WidthRequestProperty, static (BarChartItem item) => item.LabelWidth);

        var tap = new TapGestureRecognizer();
        tap.SetBinding(TapGestureRecognizer.CommandProperty, static (BarChartItem item) => item.SelectCommand);

        var column = new VerticalStackLayout { VerticalOptions = LayoutOptions.End, Spacing = 4, Children = { bar, label } };
        column.SetBinding(VisualElement.WidthRequestProperty, static (BarChartItem item) => item.Width);
        column.GestureRecognizers.Add(tap);
        return column;
    }
}
