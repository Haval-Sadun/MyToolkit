using System.Windows.Input;
using MyToolkit.Theme;

namespace MyToolkit.Controls;

/// <summary>How a detail page opens. Pick per page with <see cref="DetailHeader.HeaderStyle"/> (and the same value on <see cref="DetailTopBar"/>).</summary>
public enum DetailHeaderStyle
{
    /// <summary>A: full-width cover fading into the page, title over its bottom edge; only the back button floats on top.</summary>
    Hero,

    /// <summary>B: like <see cref="Hero"/>, and a slim bar with the small title fades in once the hero has scrolled away.</summary>
    CollapsingHero,

    /// <summary>C: no picture, a big title under the back button that shrinks into the slim bar on scroll. For settings-like pages.</summary>
    LargeTitle,

    /// <summary>D: like <see cref="CollapsingHero"/> but the hero is a palette gradient with a big glyph. For things with no cover (playlists, folders).</summary>
    PaletteHero,
}

/// <summary>How the cover sits in the hero. Set per page with <see cref="DetailHeader.CoverFit"/>.</summary>
public enum DetailCoverFit
{
    /// <summary>Full width, cropped to fill the hero (the default). Soft on a very wide window, but the most immersive.</summary>
    Fill,

    /// <summary>A centred square the height of the hero, the rest stays the page colour. Sharp on tablets and desktops.</summary>
    Square,

    /// <summary>The whole cover, uncropped, centred in the hero.</summary>
    Fit,
}

/// <summary>The top block of a detail page, meant as a list header: hero (picture or palette), title, subtitle, caption, and a footer slot for an action row.</summary>
/// <remarks>Everything is a transform or a colour, so scrolling costs no layout work. Colours come from <see cref="ThemeColors"/> tokens (Surface, SurfaceContainer, OnSurface, OnSurfaceVariant, Primary, PrimaryContainer); the theme subscription lives only while the control has a handler.</remarks>
public partial class DetailHeader : ContentView
{
    public static readonly BindableProperty HeaderStyleProperty = BindableProperty.Create(nameof(HeaderStyle), typeof(DetailHeaderStyle), typeof(DetailHeader), DetailHeaderStyle.CollapsingHero, propertyChanged: Refresh);

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(DetailHeader), string.Empty, propertyChanged: Refresh);

    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(DetailHeader), string.Empty, propertyChanged: Refresh);

    public static readonly BindableProperty CaptionProperty = BindableProperty.Create(nameof(Caption), typeof(string), typeof(DetailHeader), string.Empty, propertyChanged: Refresh);

    public static readonly BindableProperty CoverProperty = BindableProperty.Create(nameof(Cover), typeof(ImageSource), typeof(DetailHeader), null, propertyChanged: Refresh);

    public static readonly BindableProperty CoverFitProperty = BindableProperty.Create(nameof(CoverFit), typeof(DetailCoverFit), typeof(DetailHeader), DetailCoverFit.Fill, propertyChanged: Refresh);

    public static readonly BindableProperty PlaceholderGlyphProperty = BindableProperty.Create(nameof(PlaceholderGlyph), typeof(string), typeof(DetailHeader), string.Empty, propertyChanged: Refresh);

    public static readonly BindableProperty SubtitleCommandProperty = BindableProperty.Create(nameof(SubtitleCommand), typeof(ICommand), typeof(DetailHeader), null, propertyChanged: Refresh);

    public static readonly BindableProperty FooterProperty = BindableProperty.Create(nameof(Footer), typeof(View), typeof(DetailHeader), null, propertyChanged: (b, _, v) => ((DetailHeader)b).FooterHost.Content = (View?)v);

    public static readonly BindableProperty HeroHeightProperty = BindableProperty.Create(nameof(HeroHeight), typeof(double), typeof(DetailHeader), 300d, propertyChanged: Refresh);

    private TapGestureRecognizer? _subtitleTap;

    public DetailHeader()
    {
        InitializeComponent();
        Apply();
    }

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

    /// <summary>Secondary line, drawn as a link in the primary colour when <see cref="SubtitleCommand"/> is set (the tap area is the text only).</summary>
    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <summary>The picture for the hero (a URL string also works from XAML); none shows the placeholder glyph.</summary>
    public ImageSource? Cover
    {
        get => (ImageSource?)GetValue(CoverProperty);
        set => SetValue(CoverProperty, value);
    }

    public DetailCoverFit CoverFit
    {
        get => (DetailCoverFit)GetValue(CoverFitProperty);
        set => SetValue(CoverFitProperty, value);
    }

    /// <summary>Material glyph shown big while there is no picture (and always for <see cref="DetailHeaderStyle.PaletteHero"/>).</summary>
    public string PlaceholderGlyph
    {
        get => (string)GetValue(PlaceholderGlyphProperty);
        set => SetValue(PlaceholderGlyphProperty, value);
    }

    public ICommand? SubtitleCommand
    {
        get => (ICommand?)GetValue(SubtitleCommandProperty);
        set => SetValue(SubtitleCommandProperty, value);
    }

    /// <summary>Content under the title, usually the page's action row.</summary>
    public View? Footer
    {
        get => (View?)GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public double HeroHeight
    {
        get => (double)GetValue(HeroHeightProperty);
        set => SetValue(HeroHeightProperty, value);
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);
        if (args.OldHandler is not null && Application.Current is { } app)
            app.RequestedThemeChanged -= OnThemeChanged;

        if (args.NewHandler is not null && Application.Current is { } current)
        {
            current.RequestedThemeChanged += OnThemeChanged;
            Apply();
        }
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => Apply();

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        ApplyCoverFit(width);
    }

    private void ApplyCoverFit(double width)
    {
        // Square never exceeds the available width, so it is full-bleed on a narrow phone anyway.
        var square = CoverFit == DetailCoverFit.Square && width > 0 ? Math.Min(HeroHeight, width) : -1;
        var aspect = CoverFit == DetailCoverFit.Fit ? Aspect.AspectFit : Aspect.AspectFill;
        var centered = CoverFit != DetailCoverFit.Fill;
        if (Math.Abs(HeroImage.WidthRequest - square) < 0.5 && HeroImage.Aspect == aspect)
            return;

        HeroImage.WidthRequest = square;
        HeroImage.Aspect = aspect;
        HeroImage.HorizontalOptions = centered ? LayoutOptions.Center : LayoutOptions.Fill;
    }

    private static void Refresh(BindableObject bindable, object oldValue, object newValue) => ((DetailHeader)bindable).Apply();

    private void Apply()
    {
        var style = HeaderStyle;
        var hero = style != DetailHeaderStyle.LargeTitle;
        var palette = style == DetailHeaderStyle.PaletteHero;
        var hasImage = !palette && Cover is not null;

        HeroGrid.IsVisible = hero;
        HeroGrid.HeightRequest = HeroHeight;
        HeroImage.IsVisible = hasImage;
        HeroImage.Source = hasImage ? Cover : null;
        ApplyCoverFit(Width);
        GlyphLabel.Text = PlaceholderGlyph;
        GlyphLabel.IsVisible = hero && !hasImage && !string.IsNullOrEmpty(PlaceholderGlyph);
        HeroFade.IsVisible = hero && !palette;

        if (palette)
            HeroFill.Background = PaletteGradient();
        else
            ThemeColors.Set(HeroFill, BackgroundColorProperty, "SurfaceContainerHigh");

        ThemeColors.Set(GlyphLabel, Label.TextColorProperty, palette ? "OnPrimaryContainer" : "OnSurfaceVariant");
        GlyphLabel.Opacity = palette ? 0.55 : 1;

        TextBlock.VerticalOptions = hero ? LayoutOptions.End : LayoutOptions.Start;
        TextBlock.Padding = hero ? new Thickness(20, 0, 20, 12) : new Thickness(20, 72, 20, 12);
        TitleLabel.FontSize = style == DetailHeaderStyle.LargeTitle ? 34 : 30;
        TitleLabel.Text = Title;
        ThemeColors.Set(TitleLabel, Label.TextColorProperty, "OnSurface");

        SubtitleLabel.Text = Subtitle;
        SubtitleLabel.IsVisible = !string.IsNullOrEmpty(Subtitle);
        ThemeColors.Set(SubtitleLabel, Label.TextColorProperty, SubtitleCommand is null ? "OnSurfaceVariant" : "Primary");
        SetSubtitleTap();

        CaptionLabel.Text = Caption;
        CaptionLabel.IsVisible = !string.IsNullOrEmpty(Caption);
        ThemeColors.Set(CaptionLabel, Label.TextColorProperty, "OnSurfaceVariant");
    }

    private void SetSubtitleTap()
    {
        if (_subtitleTap is not null)
        {
            SubtitleLabel.GestureRecognizers.Remove(_subtitleTap);
            _subtitleTap = null;
        }

        if (SubtitleCommand is null)
            return;

        _subtitleTap = new TapGestureRecognizer { Command = SubtitleCommand };
        SubtitleLabel.GestureRecognizers.Add(_subtitleTap);
    }

    // Primary container at the top easing into the page colour, so each palette gives its own mood.
    private static LinearGradientBrush? PaletteGradient()
    {
        if (ThemeColors.Current("PrimaryContainer") is not { } top || ThemeColors.Current("SurfaceContainer") is not { } bottom)
            return null;

        return new LinearGradientBrush([new GradientStop(top, 0), new GradientStop(bottom, 1)], new Point(0, 0), new Point(0, 1));
    }
}
