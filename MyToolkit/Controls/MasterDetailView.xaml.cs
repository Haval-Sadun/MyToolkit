using MyToolkit.Services;
using MyToolkit.Theme;
using MyToolkit.ViewModels;

namespace MyToolkit.Controls;

/// <summary>One screen in the detail side of a <see cref="MasterDetailView"/>: a title, the view, and the view-model that goes with it (released when the screen leaves).</summary>
/// <param name="Scrolls">True (the default): the view is put in a scroll view with a gutter. False: it lays out and scrolls itself (a list) and fills the side.</param>
public sealed record DetailPane(string Title, View View, ToolkitViewModel? ViewModel = null, bool Scrolls = true);

/// <summary>A master list with its detail beside it from <see cref="WideFrom"/>, and the master alone below. The detail side is a small stack: <see cref="ShowRoot"/> replaces it, <see cref="Push"/> adds a screen with a back arrow to the one before, <see cref="Pop"/> goes back.
/// It owns what it shows: a pane's view-model gets <c>OnAppearing</c> when its screen is on top again and is disposed when the screen leaves, so nothing outlives its screen.</summary>
/// <remarks>Generic, no app types. Bind <see cref="IsWide"/> (two-way) to know which layout is showing; the master is whatever you put in <see cref="Master"/>.
/// Right-to-left safe: columns follow the flow direction (the master is on the start side), the divider is on the detail's start edge, paddings are symmetric and the back arrow follows <see cref="Direction"/> (a font glyph never mirrors on its own).</remarks>
[ContentProperty(nameof(Master))]
public partial class MasterDetailView : ContentView
{
    /// <summary>The list side: what the page shows alone when narrow.</summary>
    public static readonly BindableProperty MasterProperty = BindableProperty.Create(nameof(Master), typeof(View), typeof(MasterDetailView), null, propertyChanged: (b, _, v) => ((MasterDetailView)b).MasterHost.Content = (View?)v);

    /// <summary>The width, in dip, from which the detail sits beside the master.</summary>
    public static readonly BindableProperty WideFromProperty = BindableProperty.Create(nameof(WideFrom), typeof(double), typeof(MasterDetailView), Breakpoints.Wide, propertyChanged: (b, _, _) => ((MasterDetailView)b).Relayout());

    /// <summary>The master's share of the width when wide, kept between <see cref="MasterMinWidth"/> and <see cref="MasterMaxWidth"/>.</summary>
    public static readonly BindableProperty MasterShareProperty = BindableProperty.Create(nameof(MasterShare), typeof(double), typeof(MasterDetailView), 0.25, propertyChanged: (b, _, _) => ((MasterDetailView)b).Relayout());

    public static readonly BindableProperty MasterMinWidthProperty = BindableProperty.Create(nameof(MasterMinWidth), typeof(double), typeof(MasterDetailView), 300d, propertyChanged: (b, _, _) => ((MasterDetailView)b).Relayout());

    public static readonly BindableProperty MasterMaxWidthProperty = BindableProperty.Create(nameof(MasterMaxWidth), typeof(double), typeof(MasterDetailView), 380d, propertyChanged: (b, _, _) => ((MasterDetailView)b).Relayout());

    /// <summary>How wide a scrolling detail screen may grow: a form stretched across a monitor reads badly.</summary>
    public static readonly BindableProperty DetailMaxWidthProperty = BindableProperty.Create(nameof(DetailMaxWidth), typeof(double), typeof(MasterDetailView), 760d);

    /// <summary>True while the detail is beside the master. Set by this control from its width.</summary>
    public static readonly BindableProperty IsWideProperty = BindableProperty.Create(nameof(IsWide), typeof(bool), typeof(MasterDetailView), false, BindingMode.TwoWay, propertyChanged: (b, _, _) => ((MasterDetailView)b).OnWideChanged());

    private readonly List<DetailPane> _stack = [];
    private View? _wrapper;

    public MasterDetailView()
    {
        InitializeComponent();
        ThemeColors.Set(Divider, BoxView.ColorProperty, "OutlineVariant");
        ThemeColors.Set(BackButton, IconButton.GlyphColorProperty, "OnSurface");
        ThemeColors.Set(DetailTitle, Label.TextColorProperty, "OnSurface");
        Spinner.SetBinding(ActivityIndicator.IsRunningProperty, "IsBusy");
        Scrim.SetBinding(IsVisibleProperty, "IsBusy");
    }

    public View? Master
    {
        get => (View?)GetValue(MasterProperty);
        set => SetValue(MasterProperty, value);
    }

    public double WideFrom
    {
        get => (double)GetValue(WideFromProperty);
        set => SetValue(WideFromProperty, value);
    }

    public double MasterShare
    {
        get => (double)GetValue(MasterShareProperty);
        set => SetValue(MasterShareProperty, value);
    }

    public double MasterMinWidth
    {
        get => (double)GetValue(MasterMinWidthProperty);
        set => SetValue(MasterMinWidthProperty, value);
    }

    public double MasterMaxWidth
    {
        get => (double)GetValue(MasterMaxWidthProperty);
        set => SetValue(MasterMaxWidthProperty, value);
    }

    public double DetailMaxWidth
    {
        get => (double)GetValue(DetailMaxWidthProperty);
        set => SetValue(DetailMaxWidthProperty, value);
    }

    public bool IsWide
    {
        get => (bool)GetValue(IsWideProperty);
        set => SetValue(IsWideProperty, value);
    }

    /// <summary>How many screens the detail holds; 1 is the root.</summary>
    public int Depth => _stack.Count;

    /// <summary>Replaces everything in the detail with this one screen.</summary>
    public void ShowRoot(DetailPane pane)
    {
        Clear();
        Push(pane);
    }

    /// <summary>Adds a screen on top; the one under it stays (its view-model is kept) and is reached again with <see cref="Pop"/>.</summary>
    public void Push(DetailPane pane)
    {
        _stack.Add(pane);
        Render();
        pane.ViewModel?.OnAppearing();
    }

    /// <summary>Back to the screen under the top one. False when only the root is left.</summary>
    public bool Pop()
    {
        if (_stack.Count <= 1)
            return false;

        Release(_stack[^1]);
        _stack.RemoveAt(_stack.Count - 1);
        Render();

        // Back on a screen that may be out of date: what was done on top of it can have changed it.
        _stack[^1].ViewModel?.OnAppearing();
        return true;
    }

    /// <summary>Empties the detail, releasing every screen and its view-model.</summary>
    public void Clear()
    {
        for (var i = _stack.Count - 1; i >= 0; i--)
            Release(_stack[i]);

        _stack.Clear();
        Render();
    }

    private static void Release(DetailPane pane)
    {
        if (pane.ViewModel is { } viewModel)
        {
            viewModel.OnDisappearing();
            viewModel.Dispose();
        }

        pane.View.BindingContext = null;
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);

        // Gone from the screen: nothing in the detail may be left holding view-models.
        if (args.NewHandler is null)
            Clear();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0)
            return;

        IsWide = width >= WideFrom;
        Relayout();
    }

    private void OnWideChanged()
    {
        DetailArea.IsVisible = IsWide;
        if (!IsWide)
            Clear();
    }

    private void Relayout()
    {
        if (Width <= 0)
            return;

        var wide = Width >= WideFrom;
        Layout.ColumnDefinitions[0].Width = wide ? new GridLength(Math.Clamp(Width * MasterShare, MasterMinWidth, MasterMaxWidth)) : GridLength.Star;
        Layout.ColumnDefinitions[1].Width = wide ? GridLength.Star : new GridLength(0);
    }

    // The top screen: its title, the back arrow when there is a screen under it, its view (scrolling or not), and its busy state.
    private void Render()
    {
        // The view has one parent at a time: it leaves the old wrapper before it goes into a new one.
        if (_wrapper is ContentView { Content: not null } holder)
            holder.Content = null;
        else if (_wrapper is ScrollView { Content: ContentView inner })
            inner.Content = null;

        DetailHost.Content = null;
        _wrapper = null;

        if (_stack.Count == 0)
        {
            DetailTitle.Text = string.Empty;
            BackButton.IsVisible = false;
            Scrim.BindingContext = null;
            return;
        }

        var top = _stack[^1];
        DetailTitle.Text = top.Title;
        BackButton.IsVisible = _stack.Count > 1;
        BackButton.Glyph = Direction.BackArrow;
        Scrim.BindingContext = top.ViewModel;

        if (top.Scrolls)
        {
            var inner = new ContentView { Content = top.View, Padding = new Thickness(16, 0), MaximumWidthRequest = DetailMaxWidth, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Start };
            _wrapper = new ScrollView { Content = inner };
        }
        else
        {
            _wrapper = new ContentView { Content = top.View };
        }

        DetailHost.Content = _wrapper;
    }

    private void OnBackClicked(object? sender, EventArgs e) => Pop();
}
