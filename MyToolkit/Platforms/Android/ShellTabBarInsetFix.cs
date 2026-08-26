using AndroidX.Core.View;
using Google.Android.Material.BottomNavigation;
// No "using Android.Views;"/"using Android.App;" — View and other Android types collide with
// Microsoft.Maui.Controls counterparts pulled in project-wide via GlobalUsings (see ToolkitPage.cs).
using AActivity = Android.App.Activity;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;
using AViewTreeObserver = Android.Views.ViewTreeObserver;

namespace MyToolkit.Platforms.Android;

/// <summary>
/// Android 15/16 edge-to-edge enforcement leaves Shell's native BottomNavigationView unpadded —
/// MAUI never applies the system nav-bar inset to it, so tab icons render underneath the
/// 3-button/gesture bar. Call <see cref="Install"/> once from MainActivity.OnCreate.
/// </summary>
/// <remarks>
/// A listener attached directly to the BottomNavigationView (the first thing tried here) never
/// fires with a nonzero inset: Shell's own internal container consumes the system-bar inset as
/// its own padding before dispatch reaches the tab bar. Reading <c>DecorView.RootWindowInsets</c>
/// instead sidesteps that consumption order entirely — same technique already proven in
/// KurdishConnect's <c>KurdishErrorToastPresenter.ShowNativeSnackbar</c>. Driven by a permanent
/// <see cref="AViewTreeObserver.IOnGlobalLayoutListener"/> (not an insets listener) so this never
/// competes with <c>ToolkitPage</c>'s own <c>SetOnApplyWindowInsetsListener</c> churn on the
/// content root as pages push/pop.
/// </remarks>
public static class ShellTabBarInsetFix
{
    public static void Install(AActivity activity)
    {
        var decor = activity.Window?.DecorView;
        if (decor is null) return;

        decor.ViewTreeObserver?.AddOnGlobalLayoutListener(new Watcher(activity, decor));
    }

    private sealed class Watcher : Java.Lang.Object, AViewTreeObserver.IOnGlobalLayoutListener
    {
        private readonly AActivity _activity;
        private readonly AView _decor;

        public Watcher(AActivity activity, AView decor)
        {
            _activity = activity;
            _decor = decor;
        }

        public void OnGlobalLayout()
        {
            if (_decor.RootView is not AViewGroup root) return;

            var bottomNav = AndroidBottomNav.Find(root);
            if (bottomNav is null) return;

            var rootInsets = _activity.Window?.DecorView?.RootWindowInsets;
            if (rootInsets is null) return;

            var navBarBottom = WindowInsetsCompat.ToWindowInsetsCompat(rootInsets)
                .GetInsets(WindowInsetsCompat.Type.SystemBars())
                .Bottom;

            // View.SetPadding no-ops (no re-layout) when the values are unchanged, so this is
            // safe to call on every layout pass without feedback-looping GlobalLayout forever.
            if (bottomNav.PaddingBottom != navBarBottom)
                bottomNav.SetPadding(bottomNav.PaddingLeft, bottomNav.PaddingTop, bottomNav.PaddingRight, navBarBottom);
        }
    }
}
