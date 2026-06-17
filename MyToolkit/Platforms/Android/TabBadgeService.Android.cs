using Android.Views;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Navigation;

namespace MyToolkit.Services.Notifications;

// Best-effort Android tab badge. Walks the current Activity's view tree for the Shell's
// BottomNavigationView and attaches a Material BadgeDrawable to the menu item at the given
// index. Failures are swallowed — the in-page fallback badge remains the source of truth.
public sealed partial class TabBadgeService
{
    partial void RenderBadge(int tabIndex, int count)
    {
        try
        {
            var activity = Platform.CurrentActivity;
            var root = activity?.Window?.DecorView?.RootView;
            if (root is not ViewGroup vg) return;

            var bottomNav = FindBottomNav(vg);
            if (bottomNav is null) return;

            var menu = bottomNav.Menu;
            if (tabIndex < 0 || tabIndex >= menu.Size()) return;

            var item = menu.GetItem(tabIndex);
            if (item is null) return;

            var badge = bottomNav.GetOrCreateBadge(item.ItemId);
            if (count > 0)
            {
                badge.SetVisible(true);
                badge.Number = count;
            }
            else
            {
                badge.SetVisible(false);
                badge.ClearNumber();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TabBadge] Android RenderBadge failed: {ex}");
        }
    }

    private static BottomNavigationView? FindBottomNav(ViewGroup parent)
    {
        for (int i = 0; i < parent.ChildCount; i++)
        {
            var child = parent.GetChildAt(i);
            if (child is BottomNavigationView bnv) return bnv;
            if (child is NavigationBarView nbv && nbv is BottomNavigationView b) return b;
            if (child is ViewGroup vg)
            {
                var found = FindBottomNav(vg);
                if (found is not null) return found;
            }
        }
        return null;
    }
}
