using System.Windows.Input;
using MyToolkit.Theme;

namespace MyToolkit.Controls;

/// <summary>The floating top of a detail page: always a back button, plus (for the collapsing styles) a slim bar that fades in with the small title as the list scrolls. Put it over the page's list; it only takes touches on the button.</summary>
/// <remarks>Set <see cref="ScrollSource"/> to the page's CollectionView (or call <see cref="SetScrollOffset"/> from a ScrollView). Opacity and colour only, so scrolling stays cheap. The scroll subscription lives only while this control has a handler, so a popped page never stays referenced by its list.</remarks>
public partial class DetailTopBar : Grid
{
    private const double RevealRange = 60;

    public static readonly BindableProperty HeaderStyleProperty = BindableProperty.Create(nameof(HeaderStyle), typeof(DetailHeaderStyle), typeof(DetailTopBar), DetailHeaderStyle.CollapsingHero, propertyChanged: (b, _, _) => ((DetailTopBar)b).Apply());

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(DetailTopBar), string.Empty, propertyChanged: (b, _, _) => ((DetailTopBar)b).Apply());

    public static readonly BindableProperty BackCommandProperty = BindableProperty.Create(nameof(BackCommand), typeof(ICommand), typeof(DetailTopBar), null);

    public static readonly BindableProperty ScrollSourceProperty = BindableProperty.Create(nameof(ScrollSource), typeof(ItemsView), typeof(DetailTopBar), null, propertyChanged: OnScrollSourceChanged);

    /// <summary>Scroll offset (dip) where the bar starts to appear; the default follows <see cref="HeaderStyle"/>.</summary>
    public static readonly BindableProperty RevealStartProperty = BindableProperty.Create(nameof(RevealStart), typeof(double), typeof(DetailTopBar), -1d);

    private double _progress;

    public DetailTopBar()
    {
        InitializeComponent();
        // Own child, same lifetime: nothing to unsubscribe.
        BackButton.Clicked += OnBackClicked;
        Apply();
    }

    /// <summary>Raised on the back button (in addition to <see cref="BackCommand"/>).</summary>
    public event EventHandler? BackClicked;

    public DetailHeaderStyle HeaderStyle
    {
        get => (DetailHeaderStyle)GetValue(HeaderStyleProperty);
        set => SetValue(HeaderStyleProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public ICommand? BackCommand
    {
        get => (ICommand?)GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }

    public ItemsView? ScrollSource
    {
        get => (ItemsView?)GetValue(ScrollSourceProperty);
        set => SetValue(ScrollSourceProperty, value);
    }

    public double RevealStart
    {
        get => (double)GetValue(RevealStartProperty);
        set => SetValue(RevealStartProperty, value);
    }

    /// <summary>For pages that scroll a ScrollView instead of an ItemsView.</summary>
    public void SetScrollOffset(double offset)
    {
        var start = RevealStart >= 0 ? RevealStart : DefaultRevealStart(HeaderStyle);
        var progress = HeaderStyle == DetailHeaderStyle.Hero ? 0 : Math.Clamp((offset - start) / RevealRange, 0, 1);
        if (Math.Abs(progress - _progress) < 0.01)
            return;

        _progress = progress;
        Apply();
    }

    private static double DefaultRevealStart(DetailHeaderStyle style) => style == DetailHeaderStyle.LargeTitle ? 56 : 200;

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);
        if (args.OldHandler is not null)
        {
            if (ScrollSource is { } oldSource)
                oldSource.Scrolled -= OnScrolled;

            if (Application.Current is { } app)
                app.RequestedThemeChanged -= OnThemeChanged;
        }

        if (args.NewHandler is not null)
        {
            if (ScrollSource is { } source)
                source.Scrolled += OnScrolled;

            if (Application.Current is { } current)
                current.RequestedThemeChanged += OnThemeChanged;

            Apply();
        }
    }

    private static void OnScrollSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var bar = (DetailTopBar)bindable;
        if (bar.Handler is null)
            return;

        if (oldValue is ItemsView old)
            old.Scrolled -= bar.OnScrolled;

        if (newValue is ItemsView current)
            current.Scrolled += bar.OnScrolled;
    }

    private void OnScrolled(object? sender, ItemsViewScrolledEventArgs e) => SetScrollOffset(e.VerticalOffset);

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => Apply();

    private void OnBackClicked(object? sender, EventArgs e)
    {
        BackClicked?.Invoke(this, EventArgs.Empty);
        if (BackCommand?.CanExecute(null) == true)
            BackCommand.Execute(null);
    }

    private void Apply()
    {
        var collapses = HeaderStyle != DetailHeaderStyle.Hero;
        var overPicture = HeaderStyle is DetailHeaderStyle.Hero or DetailHeaderStyle.CollapsingHero or DetailHeaderStyle.PaletteHero;

        BarFill.IsVisible = collapses;
        BarTitle.IsVisible = collapses;
        BarFill.Opacity = _progress;
        BarTitle.Opacity = _progress;
        BarTitle.Text = Title;
        ThemeColors.Set(BarFill, BackgroundColorProperty, "SurfaceContainer");
        ThemeColors.Set(BarTitle, Label.TextColorProperty, "OnSurface");

        // A scrim under the arrow only while it sits on a picture; the arrow turns dark as the bar becomes solid.
        Disc.BackgroundColor = overPicture ? Color.FromRgba(0, 0, 0, 0.4 * (1 - _progress)) : Colors.Transparent;
        BackButton.GlyphColor = overPicture && _progress < 0.5 ? Colors.White : ThemeColors.Current("OnSurface") ?? Colors.Black;
    }
}
