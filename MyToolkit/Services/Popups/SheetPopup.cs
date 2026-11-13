using Microsoft.Maui.Controls.Shapes;
using UXDivers.Popups.Maui;

namespace MyToolkit.Services.Popups;

/// <summary>Bottom sheet: fixed header (handle + title), scrolling body and a pinned footer that stays above the keyboard.</summary>
internal sealed class SheetPopup : PopupPage
{
    private const double MaxWidth = 560;
    private const double MaxHeightFraction = 0.85;
    private const double ContentPadding = 20;

    private readonly View? _focusTarget;
    private readonly Border _card;
    private readonly double _bottomInset;
    private readonly bool _manualKeyboard;
    private readonly ScrollView? _scroll;
    private readonly double _chrome;
    private readonly double _screenHeight;
#if ANDROID
    private Android.Views.View? _decorView;
#endif

    public SheetPopup(string? title, View body, View? footer = null, bool scrollBody = false, bool avoidKeyboard = false, View? focusTarget = null, bool manualKeyboard = false)
    {
        _focusTarget = focusTarget;
        _manualKeyboard = manualKeyboard;
        var insets = PopupKit.SystemBarsInsetsDp();
        if (insets != default)
        {
            Padding = new Thickness(insets.Left, insets.Top, insets.Right, 0);
            _bottomInset = insets.Bottom;
        }
        else
        {
            SafeAreaAsPadding = UXDivers.Popups.SafeAreaAsPadding.All;
        }

        var grid = new Grid { RowSpacing = 0, RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) } };
        grid.Add(PopupKit.DragHandle(), 0, 0);
        if (!string.IsNullOrEmpty(title))
        {
            var label = PopupKit.Text(title, 18, true, "TextColor", TextAlignment.Start);
            label.Margin = new Thickness(0, 16, 0, 0);
            grid.Add(label, 0, 1);
        }

        var maxHeight = DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density * MaxHeightFraction;
        body.Margin = new Thickness(0, 12, 0, 0);
        // Auto rows don't shrink the body, so the scroll area is capped to what the fixed header and footer leave free.
        _screenHeight = DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density - insets.Top;
        _chrome = 112 + (footer is null ? 0 : 16 + Math.Max(footer.HeightRequest, PopupKit.ButtonHeight));
        if (scrollBody)
            _scroll = new ScrollView { Content = body };

        grid.Add(_scroll ?? body, 0, 2);
        UpdateScrollCap(0);
        if (footer is not null)
        {
            footer.Margin = new Thickness(0, 16, 0, 0);
            grid.Add(footer, 0, 3);
        }

        _card = new Border
        {
            Padding = CardPadding(0),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(PopupKit.CardRadius, PopupKit.CardRadius, 0, 0) },
            VerticalOptions = LayoutOptions.End,
            MaximumHeightRequest = maxHeight,
            Content = grid,
        };
        _card.SetDynamicResource(VisualElement.BackgroundColorProperty, "BackgroundColor");
        if (PopupKit.IsPhone)
        {
            _card.HorizontalOptions = LayoutOptions.Fill;
        }
        else
        {
            _card.HorizontalOptions = LayoutOptions.Center;
            _card.WidthRequest = MaxWidth;
        }

        this.SetDynamicResource(BackgroundColorProperty, "PopupBackdropColor");
        CloseWhenBackgroundIsClicked = true;
        AvoidKeyboard = avoidKeyboard;
        AppearingAnimation = new MoveInPopupAnimation { MoveDirection = MoveDirection.Bottom, Duration = 250 };
        DisappearingAnimation = new MoveOutPopupAnimation { MoveDirection = MoveDirection.Bottom, Duration = 200 };
        PopupContent = _card;
    }

    private void UpdateScrollCap(double imeDip)
    {
        if (_scroll is null)
            return;

        var available = Math.Min(_screenHeight * MaxHeightFraction, _screenHeight - imeDip);
        _scroll.MaximumHeightRequest = Math.Max(120, available - _chrome - (imeDip > 0 ? 0 : _bottomInset));
    }

    private Thickness CardPadding(double imeDip) => new(ContentPadding, 12, ContentPadding, ContentPadding + (imeDip > 0 ? 0 : _bottomInset));

    // The nav-bar height is already inside the IME inset, so the bottom padding drops while the keyboard is up.
    private void OnKeyboardChanged(double imeDip)
    {
        _card.Margin = new Thickness(0, 0, 0, imeDip);
        _card.Padding = CardPadding(imeDip);
        UpdateScrollCap(imeDip);
    }

    public override void OnAppearing()
    {
        base.OnAppearing();
#if ANDROID
        if (_manualKeyboard)
            AttachKeyboardListener();
#endif
        if (_focusTarget is { } target)
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () => target.Focus());
    }

    public override void OnDisappearing()
    {
        // A dismissed form (tap outside, back) must not leave the soft keyboard up with nothing to type into.
        _focusTarget?.Unfocus();
        HideSoftKeyboard();
#if ANDROID
        DetachKeyboardListener();
#endif
        base.OnDisappearing();
    }

    private static void HideSoftKeyboard()
    {
#if ANDROID
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not { } activity)
            return;

        var token = (activity.CurrentFocus ?? activity.Window?.DecorView)?.WindowToken;
        if (token is null)
            return;

        (activity.GetSystemService(Android.Content.Context.InputMethodService) as Android.Views.InputMethods.InputMethodManager)?.HideSoftInputFromWindow(token, Android.Views.InputMethods.HideSoftInputFlags.None);
#endif
    }

#if ANDROID
    // Window stays AdjustNothing while open and the IME inset is applied by hand (Android 15+ edge-to-edge adds blank space otherwise).
    private void AttachKeyboardListener()
    {
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not { } activity || activity.Window?.DecorView is not { } decor)
            return;

        activity.Window.SetSoftInputMode(Android.Views.SoftInput.AdjustNothing);
        _decorView = decor;
        AndroidX.Core.View.ViewCompat.SetOnApplyWindowInsetsListener(decor, new InsetsListener(this));
        AndroidX.Core.View.ViewCompat.RequestApplyInsets(decor);
    }

    private void DetachKeyboardListener()
    {
        if (_decorView is null)
            return;

        AndroidX.Core.View.ViewCompat.SetOnApplyWindowInsetsListener(_decorView, null);
        _decorView = null;
        Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window?.SetSoftInputMode(Android.Views.SoftInput.AdjustResize);
    }

    private sealed class InsetsListener(SheetPopup popup) : Java.Lang.Object, AndroidX.Core.View.IOnApplyWindowInsetsListener
    {
        private readonly WeakReference<SheetPopup> _popup = new(popup);

        public AndroidX.Core.View.WindowInsetsCompat OnApplyWindowInsets(Android.Views.View? v, AndroidX.Core.View.WindowInsetsCompat? insets)
        {
            if (insets is null)
                return AndroidX.Core.View.WindowInsetsCompat.Consumed!;

            var density = v?.Resources?.DisplayMetrics?.Density ?? 1;
            if (density <= 0)
                density = 1;

            var ime = insets.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.Ime());
            if (_popup.TryGetTarget(out var target))
                target.OnKeyboardChanged((ime?.Bottom ?? 0) / density);

            return AndroidX.Core.View.WindowInsetsCompat.Consumed!;
        }
    }
#endif
}
