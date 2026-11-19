using MyToolkit.Theme;

namespace MyToolkit.Controls;

/// <summary>Why a share did not happen: the picture could not be made (<see cref="Exception"/> null), or the share sheet failed.</summary>
public sealed class ShareFailedEventArgs(Exception? exception) : EventArgs
{
    public Exception? Exception { get; } = exception;
}

/// <summary>Shows a view (a poster, a card) full-screen on a dimmed background with Close and Share buttons; Share photographs the view as it is on screen and hands the picture to the system share sheet.
/// Put the view to share inside it: <c>&lt;tk:SharePreviewOverlay IsOpen="{Binding IsPosterShowing}"&gt;…&lt;/tk:SharePreviewOverlay&gt;</c>. Place it last in a page's root grid so it covers the page; it is invisible while closed.</summary>
[ContentProperty(nameof(Preview))]
public sealed class SharePreviewOverlay : ContentView
{
    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(nameof(IsOpen), typeof(bool), typeof(SharePreviewOverlay), false, BindingMode.TwoWay);
    public static readonly BindableProperty PreviewProperty = BindableProperty.Create(nameof(Preview), typeof(View), typeof(SharePreviewOverlay), null, propertyChanged: OnPreviewChanged);
    public static readonly BindableProperty CloseTextProperty = BindableProperty.Create(nameof(CloseText), typeof(string), typeof(SharePreviewOverlay), "Close", propertyChanged: (bindable, _, text) => ((SharePreviewOverlay)bindable)._close.Text = (string)text);
    public static readonly BindableProperty ShareTextProperty = BindableProperty.Create(nameof(ShareText), typeof(string), typeof(SharePreviewOverlay), "Share", propertyChanged: (bindable, _, text) => ((SharePreviewOverlay)bindable)._share.Text = (string)text);
    public static readonly BindableProperty ShareTitleProperty = BindableProperty.Create(nameof(ShareTitle), typeof(string), typeof(SharePreviewOverlay), string.Empty);
    public static readonly BindableProperty FileNameProperty = BindableProperty.Create(nameof(FileName), typeof(string), typeof(SharePreviewOverlay), "share.png");

    /// <summary>Raised when Share could not photograph the view or the share sheet threw; the app decides what to tell the user.</summary>
    public event EventHandler<ShareFailedEventArgs>? ShareFailed;

    private readonly ContentView _host = new() { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
    private readonly Button _close = new() { Text = "Close", CornerRadius = 24, HeightRequest = 48, Padding = new Thickness(28, 0) };
    private readonly Button _share = new() { Text = "Share", CornerRadius = 24, HeightRequest = 48, Padding = new Thickness(28, 0) };

    public SharePreviewOverlay()
    {
        IsVisible = false;
        ThemeColors.Set(_close, VisualElement.BackgroundColorProperty, "SurfaceContainerHigh");
        ThemeColors.Set(_close, Button.TextColorProperty, "OnSurface");
        ThemeColors.Set(_share, VisualElement.BackgroundColorProperty, "Primary");
        ThemeColors.Set(_share, Button.TextColorProperty, "OnPrimary");
        _close.Clicked += OnCloseClicked;
        _share.Clicked += OnShareClicked;

        var buttons = new HorizontalStackLayout { Spacing = 12, HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 16, 0, 0), Children = { _close, _share } };
        var root = new Grid
        {
            BackgroundColor = Color.FromArgb("#CC000000"),
            RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)],
            Padding = new Thickness(0, 24),
        };
        root.Add(_host, 0, 0);
        root.Add(buttons, 0, 1);

        // A tap beside the preview closes it.
        var tap = new TapGestureRecognizer();
        tap.Tapped += OnBackdropTapped;
        root.GestureRecognizers.Add(tap);
        Content = root;
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>The view that is shown and photographed.</summary>
    public View? Preview
    {
        get => (View?)GetValue(PreviewProperty);
        set => SetValue(PreviewProperty, value);
    }

    public string CloseText
    {
        get => (string)GetValue(CloseTextProperty);
        set => SetValue(CloseTextProperty, value);
    }

    public string ShareText
    {
        get => (string)GetValue(ShareTextProperty);
        set => SetValue(ShareTextProperty, value);
    }

    /// <summary>The title the system share sheet shows.</summary>
    public string ShareTitle
    {
        get => (string)GetValue(ShareTitleProperty);
        set => SetValue(ShareTitleProperty, value);
    }

    public string FileName
    {
        get => (string)GetValue(FileNameProperty);
        set => SetValue(FileNameProperty, value);
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == IsOpenProperty.PropertyName)
            IsVisible = IsOpen;
    }

    private static void OnPreviewChanged(BindableObject bindable, object? oldValue, object? newValue) => ((SharePreviewOverlay)bindable)._host.Content = newValue as View;

    private void OnCloseClicked(object? sender, EventArgs e) => IsOpen = false;

    private void OnBackdropTapped(object? sender, TappedEventArgs e) => IsOpen = false;

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        if (Preview is not { } preview)
            return;

        // Pressed twice, the share sheet would open twice.
        _share.IsEnabled = false;
        try
        {
            // The artwork in the preview loads a moment after it appears; give it a frame before the picture.
            await Task.Delay(150);
            if (!await ViewCapture.ShareAsync(preview, ShareTitle, FileName))
                ShareFailed?.Invoke(this, new ShareFailedEventArgs(null));
        }
        catch (Exception ex)
        {
            ShareFailed?.Invoke(this, new ShareFailedEventArgs(ex));
        }
        finally
        {
            _share.IsEnabled = true;
        }
    }
}
