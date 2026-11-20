using Microsoft.Maui.Controls.Shapes;
using MyToolkit.Behaviors;
using UXDivers.Popups.Maui;
using UXDivers.Popups.Services;

namespace MyToolkit.Services.Popups;

/// <summary>Bottom sheet: fixed header (handle + title), scrolling body and a pinned footer that stays above the keyboard.</summary>
internal sealed class SheetPopup : PopupPage
{
    private const double MaxWidth = 560;
    private const double MaxHeightFraction = 0.85;
    private const double ContentPadding = 20;
    private const double HandleRowHeight = 28;

    private readonly DragToDismissBehavior _dragToClose = new();
    private Color? _backdrop;
    private readonly View? _focusTarget;
    private readonly Border _card;
    private readonly double _bottomInset;
    private readonly bool _manualKeyboard;
    private readonly ScrollView? _scroll;
    private readonly double _chrome;
    private double _screenHeight;
    private double _imeDip;
#if WINDOWS
    private readonly MyToolkit.Platforms.Windows.ModalFocusTrapGuard _focusTrapGuard = new();
#endif
#if ANDROID
    private Android.Views.View? _decorView;
#endif

    public SheetPopup(string? title, View body, View? footer = null, bool scrollBody = false, bool avoidKeyboard = false, View? focusTarget = null, bool manualKeyboard = false)
    {
#if WINDOWS
        // Own event, so nothing outlives the sheet; covers a sheet torn down without OnDisappearing, which would leave the modal focus trap switched off.
        HandlerChanged += OnHandlerChangedResumeFocusTrap;
#endif
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
        // The header (handle and title) is what you hold to drag the sheet down; a transparent background keeps its whole area hit-testable.
        var header = new Grid { BackgroundColor = Colors.Transparent, RowSpacing = 0, RowDefinitions = { new RowDefinition(HandleRowHeight), new RowDefinition(GridLength.Auto) } };
        header.Add(PopupKit.DragHandle(), 0, 0);
        var hasTitle = !string.IsNullOrEmpty(title);
        if (hasTitle)
        {
            var label = PopupKit.Text(title!, 18, true, "TextColor", TextAlignment.Start);
            label.Margin = new Thickness(0, 4, 0, 0);
            header.Add(label, 0, 1);
        }

        header.Behaviors.Add(_dragToClose);
        grid.Add(header, 0, 0);

        var maxHeight = DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density * MaxHeightFraction;
        body.Margin = new Thickness(0, hasTitle ? 12 : 0, 0, 0);
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

        // Hold the header and drag down, as in Flutter: the sheet follows, the backdrop fades, and past half (or flung) it closes.
        _dragToClose.Target = _card;
        _dragToClose.DragChanged += OnDragChanged;
        _dragToClose.Settled += OnDragSettled;
        _dragToClose.Dismissed += OnDragDismissed;

        this.SetDynamicResource(BackgroundColorProperty, "PopupBackdropColor");
        CloseWhenBackgroundIsClicked = true;
        AvoidKeyboard = avoidKeyboard;
        AppearingAnimation = new MoveInPopupAnimation { MoveDirection = MoveDirection.Bottom, Duration = 250 };
        DisappearingAnimation = new MoveOutPopupAnimation { MoveDirection = MoveDirection.Bottom, Duration = 200 };
        PopupContent = _card;
    }

    // The window, not the screen: a small window on a big monitor (or a split screen) must still be able to show the sheet, so the sheet and its scroll area are capped to the height the popup really has.
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 0 && !PopupKit.IsPhone)
            _card.WidthRequest = Math.Min(MaxWidth, width - Padding.Left - Padding.Right);

        if (height <= 0)
            return;

        var available = height - Padding.Top;
        if (Math.Abs(available - _screenHeight) < 1)
            return;

        _screenHeight = available;
        _card.MaximumHeightRequest = available * MaxHeightFraction;
        UpdateScrollCap(_imeDip);
    }

    private void UpdateScrollCap(double imeDip)
    {
        if (_scroll is null)
            return;

        var available = Math.Min(_screenHeight * MaxHeightFraction, _screenHeight - imeDip);
        _scroll.MaximumHeightRequest = Math.Max(120, available - _chrome - (imeDip > 0 ? 0 : _bottomInset));
    }

    // No top padding: the handle row is tall enough to hold, and sits where the padding used to be.
    private Thickness CardPadding(double imeDip) => new(ContentPadding, 0, ContentPadding, ContentPadding + (imeDip > 0 ? 0 : _bottomInset));

    private void OnDragChanged(object? sender, DragToDismissEventArgs e) => SetBackdrop(1 - e.Fraction);

    private void OnDragSettled(object? sender, EventArgs e) => SetBackdrop(1);

    // Gone already: the pop's own slide-out must not draw the card again.
    private void OnDragDismissed(object? sender, EventArgs e)
    {
        _card.IsVisible = false;
        _ = IPopupService.Current.PopAsync(this);
    }

    // Fades the dimmed backdrop with the drag; 1 puts the themed colour back.
    private void SetBackdrop(double left)
    {
        if (left >= 1)
        {
            this.SetDynamicResource(BackgroundColorProperty, "PopupBackdropColor");
            return;
        }

        if (_backdrop is null && Application.Current?.Resources.TryGetValue("PopupBackdropColor", out var found) == true && found is Color color)
            _backdrop = color;

        if (_backdrop is { } backdrop)
            BackgroundColor = backdrop.WithAlpha((float)(backdrop.Alpha * left));
    }

    // The nav-bar height is already inside the IME inset, so the bottom padding drops while the keyboard is up.
    private void OnKeyboardChanged(double imeDip)
    {
        _imeDip = imeDip;
        _card.Margin = new Thickness(0, 0, 0, imeDip);
        _card.Padding = CardPadding(imeDip);
        UpdateScrollCap(imeDip);
#if ANDROID
        // Some devices (tablets) also resize the window, which would lift the card twice: measure the real overlap once layout settled.
        if (imeDip > 0)
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), CorrectLift);
#endif
    }

#if ANDROID
    private void CorrectLift()
    {
        if (_imeDip <= 0 || _decorView is null || _card.Handler?.PlatformView is not Android.Views.View view)
            return;

        var density = view.Resources?.DisplayMetrics?.Density ?? 1;
        var location = new int[2];
        view.GetLocationOnScreen(location);
        var cardBottom = location[1] + view.Height;
        var screenHeight = view.Resources?.DisplayMetrics?.HeightPixels ?? 0;
        var keyboardTop = screenHeight - _imeDip * density;
        var needed = Math.Max(0, (cardBottom + _card.Margin.Bottom * density - keyboardTop) / density);
        if (Math.Abs(needed - _card.Margin.Bottom) < 2)
            return;

        _card.Margin = new Thickness(0, 0, 0, needed);
        UpdateScrollCap(Math.Max(needed, 0));
    }
#endif

    public override void OnAppearing()
    {
        base.OnAppearing();
#if ANDROID
        if (_manualKeyboard)
            AttachKeyboardListener();
#endif
#if WINDOWS
        // Over a modal page (the Player) MAUI would veto focus into this sheet's text boxes.
        _focusTrapGuard.Suspend(Handler?.PlatformView as Microsoft.UI.Xaml.DependencyObject, (Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window)?.Content);
#endif
        if (_focusTarget is { } target)
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () => target.Focus());
    }

#if WINDOWS
    private void OnHandlerChangedResumeFocusTrap(object? sender, EventArgs e)
    {
        if (Handler is null)
            _focusTrapGuard.Resume();
    }
#endif

    public override void OnDisappearing()
    {
        _dragToClose.DragChanged -= OnDragChanged;
        _dragToClose.Settled -= OnDragSettled;
        _dragToClose.Dismissed -= OnDragDismissed;
        // A dismissed form (tap outside, back) must not leave the soft keyboard up with nothing to type into.
        _focusTarget?.Unfocus();
        HideSoftKeyboard();
#if WINDOWS
        _focusTrapGuard.Resume();
#endif
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
