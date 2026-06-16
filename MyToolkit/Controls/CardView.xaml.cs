namespace MyToolkit.Controls;

public partial class CardView : ContentView
{
    public CardView()
    {
        InitializeComponent();
    }

    // --- Header slot ---

    public static readonly BindableProperty HeaderProperty =
        BindableProperty.Create(nameof(Header), typeof(View), typeof(CardView),
            propertyChanged: (b, _, _) => ((CardView)b).OnPropertyChanged(nameof(HasHeader)));

    public View? Header
    {
        get => (View?)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public bool HasHeader => Header is not null;

    // --- Body slot ---

    public static readonly BindableProperty BodyProperty =
        BindableProperty.Create(nameof(Body), typeof(View), typeof(CardView));

    public View? Body
    {
        get => (View?)GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    // --- Appearance ---

    public static readonly BindableProperty CardPaddingProperty =
        BindableProperty.Create(nameof(CardPadding), typeof(Thickness), typeof(CardView), new Thickness(16));

    public Thickness CardPadding
    {
        get => (Thickness)GetValue(CardPaddingProperty);
        set => SetValue(CardPaddingProperty, value);
    }

    public static readonly BindableProperty CardBackgroundProperty =
        BindableProperty.Create(nameof(CardBackground), typeof(Color), typeof(CardView), Colors.White);

    public Color CardBackground
    {
        get => (Color)GetValue(CardBackgroundProperty);
        set => SetValue(CardBackgroundProperty, value);
    }

    public static readonly BindableProperty SectionSpacingProperty =
        BindableProperty.Create(nameof(SectionSpacing), typeof(double), typeof(CardView), 8.0);

    public double SectionSpacing
    {
        get => (double)GetValue(SectionSpacingProperty);
        set => SetValue(SectionSpacingProperty, value);
    }

    // --- Shadow ---

    public static readonly BindableProperty ShadowBrushProperty =
        BindableProperty.Create(nameof(ShadowBrush), typeof(Brush), typeof(CardView), new SolidColorBrush(Colors.Black));

    public Brush ShadowBrush
    {
        get => (Brush)GetValue(ShadowBrushProperty);
        set => SetValue(ShadowBrushProperty, value);
    }

    public static readonly BindableProperty ShadowOffsetProperty =
        BindableProperty.Create(nameof(ShadowOffset), typeof(Point), typeof(CardView), new Point(0, 2));

    public Point ShadowOffset
    {
        get => (Point)GetValue(ShadowOffsetProperty);
        set => SetValue(ShadowOffsetProperty, value);
    }

    public static readonly BindableProperty ShadowRadiusProperty =
        BindableProperty.Create(nameof(ShadowRadius), typeof(float), typeof(CardView), 8f);

    public float ShadowRadius
    {
        get => (float)GetValue(ShadowRadiusProperty);
        set => SetValue(ShadowRadiusProperty, value);
    }

    public static readonly BindableProperty ShadowOpacityProperty =
        BindableProperty.Create(nameof(ShadowOpacity), typeof(float), typeof(CardView), 0.1f);

    public float ShadowOpacity
    {
        get => (float)GetValue(ShadowOpacityProperty);
        set => SetValue(ShadowOpacityProperty, value);
    }
}
