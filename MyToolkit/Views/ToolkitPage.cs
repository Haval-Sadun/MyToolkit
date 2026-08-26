using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using MyToolkit.ViewModels;

namespace MyToolkit.Views;

/// <summary>
/// Base page: forwards navigation lifecycle to an <see cref="ILifecycleAware"/> bound
/// view-model, disposes transient VMs on pop, applies safe-area handling, and (on
/// Android) lifts an input row above the keyboard / pads for the nav-bar inset — the
/// technique that avoids the Android 15/16 edge-to-edge blank-space artifact.
/// </summary>
public abstract class ToolkitPage : ContentPage
{
    /// <summary>The bound view-model viewed through its lifecycle contract, if any.</summary>
    protected ILifecycleAware? Lifecycle => BindingContext as ILifecycleAware;

    /// <summary>
    /// When true, the bound ViewModel is disposed once this page is popped off the
    /// navigation stack. Pushed detail pages (transient VMs) override this to <c>true</c>;
    /// Shell tab-root pages keep the default so their singleton VMs are never disposed.
    /// </summary>
    protected virtual bool DisposeViewModelOnPop => false;

    /// <summary>
    /// Input row to keep above the soft keyboard. When non-null, on Android the
    /// window is switched to <c>AdjustNothing</c> while the page is visible (so the
    /// Android 15/16 edge-to-edge layout never injects blank space) and this view is
    /// lifted manually by the IME inset. Default null = no keyboard handling.
    /// </summary>
    protected virtual View? KeyboardInsetTarget => null;

    /// <summary>
    /// Master safe-area switch for the page. When <c>true</c> (the default), the page keeps
    /// clear of the system bars / notch on both platforms. A page that intentionally bleeds
    /// to every edge (e.g. a full-screen image viewer) overrides this to <c>false</c>.
    /// </summary>
    protected virtual bool ApplySafeArea => true;

    /// <summary>
    /// Whether a Shell tab bar sits below this page. When hosted inside Shell, this follows
    /// <see cref="Shell.GetTabBarIsVisible(BindableObject)"/> so pushed child pages that still
    /// show tabs are treated correctly. Outside Shell, fall back to the historical default
    /// (inverse of <see cref="DisposeViewModelOnPop"/>).
    /// </summary>
    protected virtual bool HasBottomTabBar => GetContainingShell() is not null
        ? Shell.GetTabBarIsVisible(this)
        : !DisposeViewModelOnPop;

    /// <summary>
    /// Whether the tab bar from <see cref="HasBottomTabBar"/> is trusted to already clear the
    /// Android system nav-bar inset on its own. Default true (the historical assumption). An app
    /// that pads its own native tab bar to clear the inset (e.g. ZagrosTune's
    /// ShellTabBarInsetFix) grows the tab bar's height without resizing the page content area
    /// above it — override to false on that app's tab-root pages so this page gets the matching
    /// bottom inset too, otherwise a gap opens up between the page content and the taller tab bar.
    /// </summary>
    protected virtual bool TabBarClearsOwnInset => true;

    private Shell? GetContainingShell()
    {
        Element? current = this;
        while (current is not null)
        {
            if (current is Shell shell)
                return shell;

            current = current.Parent;
        }

        return null;
    }

    // Android bottom nav-bar inset applies to safe-area pages with no tab bar below them, or
    // with a tab bar that doesn't clear its own inset — never when an input row is being lifted
    // manually (that path owns the bottom).
    private bool ApplyBottomInset => ApplySafeArea && (!HasBottomTabBar || !TabBarClearsOwnInset) && KeyboardInsetTarget is null;

#if ANDROID
    private bool _kbListenerAttached;
    private Android.Views.View? _insetRoot;
    private double _basePaddingBottom;
#endif

    private int _lastStackDepth = 0;
    protected ToolkitPage()
    {
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Lifecycle?.OnAppearing();
#if IOS
        // Single global iOS safe-area enable — insets content below the notch / home indicator.
        On<iOS>().SetUseSafeArea(ApplySafeArea);
#endif
#if ANDROID
        if (KeyboardInsetTarget is { } target)
        {
            SetWindowSoftInput(Android.Views.SoftInput.AdjustNothing);
            if (!TryAttachInsetListener())
                target.HandlerChanged += OnTargetHandlerChanged;
        }
        else if (ApplyBottomInset)
        {
            _basePaddingBottom = Padding.Bottom;
            if (!TryAttachInsetListener())
                Loaded += OnPageLoaded;
        }
#endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Lifecycle?.OnDisappearing();
#if ANDROID
        if (KeyboardInsetTarget is { } target)
        {
            // Restore the app-wide default so other pages keep normal keyboard behavior.
            SetWindowSoftInput(Android.Views.SoftInput.AdjustResize);
            target.HandlerChanged -= OnTargetHandlerChanged;
            DetachInsetListener();
        }
        else if (ApplyBottomInset)
        {
            Loaded -= OnPageLoaded;
            DetachInsetListener();
        }
#endif
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        var currentDepth = GetStackDepth();
        var direction = ResolveToDirection(currentDepth);

        _lastStackDepth = currentDepth;

        Lifecycle?.OnNavigatedTo(direction);
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);

        var currentDepth = GetStackDepth();
        var direction = ResolveFromDirection(currentDepth);

        Lifecycle?.OnNavigatedFrom(direction);
        OnNavigatedFrom(direction);

        // If this page is no longer on the stack it has been popped for good — release
        // the transient ViewModel (and everything it owns) so it can be collected.
        if (DisposeViewModelOnPop && Navigation?.NavigationStack?.Contains(this) == false)
            (BindingContext as IDisposable)?.Dispose();
    }
    protected virtual void OnNavigatedFrom(NavigationDirection direction)
    {
    }

    private int GetStackDepth()
    {
        return Navigation?.NavigationStack?.Count ?? 0;
    }

    private NavigationDirection ResolveToDirection(int currentDepth)
    {
        if (_lastStackDepth == 0)
            return NavigationDirection.Unknown;

        if (currentDepth < _lastStackDepth)
            return NavigationDirection.FromChild;

        if (currentDepth > _lastStackDepth)
            return NavigationDirection.ToChild;

        return NavigationDirection.Unknown;
    }

    private NavigationDirection ResolveFromDirection(int currentDepth)
    {
        if (_lastStackDepth == 0)
            return NavigationDirection.Unknown;

        if (currentDepth > _lastStackDepth)
            return NavigationDirection.ToChild;

        if (currentDepth < _lastStackDepth)
            return NavigationDirection.FromChild;

        return NavigationDirection.Unknown;
    }

#if ANDROID
    private static void SetWindowSoftInput(Android.Views.SoftInput mode)
        => Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window?.SetSoftInputMode(mode);

    private void OnTargetHandlerChanged(object? sender, EventArgs e)
    {
        if (TryAttachInsetListener() && KeyboardInsetTarget is { } target)
            target.HandlerChanged -= OnTargetHandlerChanged;
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        if (TryAttachInsetListener())
            Loaded -= OnPageLoaded;
    }

    /// <summary>Sets the page's bottom padding to its base value plus the nav-bar inset.</summary>
    private void ApplyBottomInsetPadding(double navBottomDip) =>
        Padding = new Thickness(Padding.Left, Padding.Top, Padding.Right, _basePaddingBottom + navBottomDip);

    private bool TryAttachInsetListener()
    {
        if (_kbListenerAttached) return true;

        // Attach at the activity content root rather than a MAUI view: the IME inset is
        // reliably dispatched there, whereas MAUI's own inset handling on the page tree
        // swallows it before it reaches a nested input row. The listener lifts the input
        // row manually and consumes only the bottom insets (nav bar + IME) — otherwise the
        // nav-bar inset is applied twice (here + MAUI's safe area), leaving a blank band
        // below the input row on devices with on-screen navigation buttons. The top
        // (status bar) inset is preserved so page titles stay below the system clock.
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity?.Window?.DecorView?.FindViewById(Android.Resource.Id.Content) is Android.Views.View root)
        {
            _insetRoot = root;
            AndroidX.Core.View.ViewCompat.SetOnApplyWindowInsetsListener(root, new InsetsListener(this));
            AndroidX.Core.View.ViewCompat.RequestApplyInsets(root);
            _kbListenerAttached = true;
            return true;
        }
        return false;
    }

    private void DetachInsetListener()
    {
        if (_insetRoot is { } root)
        {
            // Our listener consumed the bottom inset on the shared activity content root. Remove
            // it and force a fresh inset pass so Shell (and its tab bar) receives the FULL,
            // unmodified insets again — otherwise the tab bar keeps the last bottom-consumed
            // insets and slides under the system nav bar when popping back to a tab root.
            AndroidX.Core.View.ViewCompat.SetOnApplyWindowInsetsListener(root, null);

            // Posted, not called synchronously: OnDisappearing fires while this page's pop
            // transition is still animating, before the reappearing tab root's own views —
            // Shell's tab bar included — are necessarily back in a state ready to receive a fresh
            // dispatch. Requesting synchronously here left the tab bar applying whatever
            // bottom-consumed insets it had last cached, i.e. exactly the "hidden behind the system
            // nav bar" symptom the comment above used to only warn about, never actually prevent.
            // Posting to the next UI-thread frame — after the current layout pass settles — is what
            // reliably gets a fresh dispatch to actually reach it.
            root.Post(() => AndroidX.Core.View.ViewCompat.RequestApplyInsets(root));
            _insetRoot = null;
        }
        _kbListenerAttached = false;
        if (KeyboardInsetTarget is { } target)
            target.Margin = new Thickness(0);
        else if (ApplyBottomInset)
            Padding = new Thickness(Padding.Left, Padding.Top, Padding.Right, _basePaddingBottom);
    }

    protected virtual void OnKeyboardChanged(double imeBottomDip, double navBottomDip)
    {
        // Lift the input row above the keyboard; when the keyboard is hidden keep it
        // above the navigation bar (imeBottomDip == 0 there, so fall back to nav inset).
        if (KeyboardInsetTarget is { } target)
            target.Margin = new Thickness(0, 0, 0, Math.Max(imeBottomDip, navBottomDip));
    }

    private sealed class InsetsListener : Java.Lang.Object, AndroidX.Core.View.IOnApplyWindowInsetsListener
    {
        private readonly ToolkitPage _page;
        public InsetsListener(ToolkitPage page) => _page = page;

        public AndroidX.Core.View.WindowInsetsCompat OnApplyWindowInsets(Android.Views.View? v, AndroidX.Core.View.WindowInsetsCompat? insets)
        {
            var sys = insets?.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars());
            var ime = insets?.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.Ime());

            if (sys is null)
                return insets!;
            double density = v.Resources?.DisplayMetrics?.Density ?? 1;
            if (density <= 0)
                density = 1;

            if (_page.KeyboardInsetTarget is not null)
                _page.OnKeyboardChanged((ime?.Bottom ?? 0) / density, sys.Bottom / density);
            else
                // No input row to lift — just pad the page bottom by the nav-bar inset.
                _page.ApplyBottomInsetPadding(sys.Bottom / density);

            // Consume only the BOTTOM insets (nav bar + IME) — the input row is lifted
            // manually, so letting MAUI apply them too would leave a blank band below it.
            // Keep the TOP (status bar) inset intact, otherwise the page title/content
            // rides up under the status bar and the system clock.
            var keepTop = AndroidX.Core.Graphics.Insets.Of(sys.Left, sys.Top, sys.Right, 0);
            return new AndroidX.Core.View.WindowInsetsCompat.Builder(insets!)
                .SetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars(), keepTop)
                .SetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.Ime(), AndroidX.Core.Graphics.Insets.None!)
                .Build();
        }
    }
#endif
}
