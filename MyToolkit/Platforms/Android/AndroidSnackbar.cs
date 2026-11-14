using Android.Views;
using Android.Widget;
using AndroidX.CoordinatorLayout.Widget;
using Google.Android.Material.BottomNavigation;
using MaterialSnackbar = Google.Android.Material.Snackbar.Snackbar;

namespace MyToolkit.Platforms.Android;

/// <summary>Native Material snackbar that can be swiped away. The toolkit's Snackbar can't be: Material only adds swipe-to-dismiss inside a CoordinatorLayout, which MAUI pages don't have.</summary>
/// <remarks>Hosts it in a wrap-content CoordinatorLayout pinned to the bottom of the activity's content — zero height while empty, so it never blocks touches to the page.</remarks>
public static class AndroidSnackbar
{
    // Weak: both hold the Activity — a strong static would keep a destroyed one (after rotation/theme change) alive.
    private static WeakReference<CoordinatorLayout>? _host;
    private static WeakReference<MaterialSnackbar>? _current;

    /// <summary>Optional app hook: the native view the snackbar should sit directly above (e.g. a mini player) for the current page; null falls back to the bottom navigation bar.</summary>
    public static Func<Page, global::Android.Views.View?>? AnchorResolver { get; set; }

    /// <summary>Must run on the UI thread. One snackbar at a time — a new one replaces the old.</summary>
    public static void Show(string message, TimeSpan duration, Action? action = null, string? actionText = null)
    {
        if (Platform.CurrentActivity is not { } activity || ContentOf(activity) is not { } content)
            return;

        if (_current is not null && _current.TryGetTarget(out var previous))
            previous.Dismiss();

        var host = EnsureHost(activity, content);
        var bar = MaterialSnackbar.Make(host, message, (int)duration.TotalMilliseconds);
        if (action is not null)
            bar.SetAction(actionText ?? "OK", _ => action());

        // Sit directly above whatever is really at the bottom — no guessed margin, which drifted up toward the middle.
        if (FindAnchor(content) is { } anchor)
            bar.SetAnchorView(anchor);

        _current = new WeakReference<MaterialSnackbar>(bar);
        bar.Show();
    }

    // The Player modal lives in its own window and hides anything in the activity content, so host the snackbar in the top modal's window.
    private static FrameLayout? ContentOf(global::Android.App.Activity activity)
    {
        var modal = Shell.Current?.Navigation.ModalStack.LastOrDefault();
        var root = (modal?.Handler?.PlatformView as global::Android.Views.View)?.RootView;
        return root?.FindViewById<FrameLayout>(global::Android.Resource.Id.Content) ?? activity.FindViewById<FrameLayout>(global::Android.Resource.Id.Content);
    }

    /// <summary>The app anchor when it supplies one, otherwise the tab bar; null on a screen with neither.</summary>
    private static global::Android.Views.View? FindAnchor(ViewGroup content)
    {
        if (Shell.Current?.CurrentPage is { } page && AnchorResolver?.Invoke(page) is { IsShown: true } anchor)
            return anchor;

        return FindShownBottomNavigation(content);
    }

    private static BottomNavigationView? FindShownBottomNavigation(ViewGroup parent)
    {
        for (var i = 0; i < parent.ChildCount; i++)
        {
            switch (parent.GetChildAt(i))
            {
                case BottomNavigationView { IsShown: true } navigation:
                    return navigation;
                case ViewGroup group when FindShownBottomNavigation(group) is { } found:
                    return found;
            }
        }

        return null;
    }

    // Rebuilt when the activity is recreated (rotation, theme change) — the old host belongs to a dead window.
    private static CoordinatorLayout EnsureHost(global::Android.App.Activity activity, FrameLayout content)
    {
        CoordinatorLayout? existing = null;
        if (_host?.TryGetTarget(out existing) == true && ReferenceEquals(existing!.Parent, content))
            return existing;

        if (existing?.Parent is ViewGroup oldParent)
            oldParent.RemoveView(existing);

        // The content view owns the host from here; the static only remembers it weakly.
        var host = new CoordinatorLayout(activity)
        {
            LayoutParameters = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Bottom),
        };
        content.AddView(host);
        _host = new WeakReference<CoordinatorLayout>(host);
        return host;
    }
}
