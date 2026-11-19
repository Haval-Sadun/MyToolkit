using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MyToolkit.Controls;

/// <summary>Turns values into bar heights. Pure: a zero still draws a short stub so the chart keeps its shape, and any other value at least a few pixels so it is visible.</summary>
public static class BarChartScale
{
    public static double Height(long value, long max, double maxHeight, double minHeight = 5, double zeroHeight = 3) =>
        value <= 0 || max <= 0 ? zeroHeight : Math.Max(minHeight, value / (double)max * maxHeight);
}

/// <summary>One bar of a <see cref="BarChartView"/>. Tapping it runs the callback the owner gave, which usually selects it and shows its <see cref="Caption"/>.</summary>
public sealed class BarChartItem : ObservableObject
{
    private bool _isSelected;

    /// <param name="label">Written under the bar; empty for a bar that needs none.</param>
    /// <param name="caption">What the bar stands for, for the owner to show when it is selected.</param>
    /// <param name="height">The bar's height in dip (see <see cref="BarChartScale"/>).</param>
    /// <param name="width">The bar's width in dip.</param>
    /// <param name="choose">Called when the bar is tapped; null for a chart that cannot be read bar by bar.</param>
    public BarChartItem(string label, string caption, double height, double width, bool isSelected, Action<BarChartItem>? choose)
    {
        Label = label;
        Caption = caption;
        Height = height;
        Width = width;
        _isSelected = isSelected;
        SelectCommand = new RelayCommand(() => choose?.Invoke(this));
    }

    public string Label { get; }

    public string Caption { get; }

    public double Height { get; }

    public double Width { get; }

    /// <summary>Wide enough to write the label under a thin bar; the neighbours' labels are empty.</summary>
    public double LabelWidth => Math.Max(Width, 30);

    /// <summary>The selected bar is drawn solid, the rest faded.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
                OnPropertyChanged(nameof(Opacity));
        }
    }

    public double Opacity => IsSelected ? 1 : 0.4;

    public ICommand SelectCommand { get; }
}
