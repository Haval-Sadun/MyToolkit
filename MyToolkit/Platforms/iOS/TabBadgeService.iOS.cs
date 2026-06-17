using UIKit;

namespace MyToolkit.Services.Notifications;

// Best-effort iOS tab badge. Walks up from the key window's root view controller to find the
// UITabBarController the Shell renders, then sets BadgeValue on the item at the given index.
// Failures are swallowed — the in-page fallback badge remains the on-screen source of truth.
public sealed partial class TabBadgeService
{
    partial void RenderBadge(int tabIndex, int count)
    {
        try
        {
            var window = UIApplication.SharedApplication.ConnectedScenes
                .OfType<UIWindowScene>()
                .SelectMany(s => s.Windows)
                .FirstOrDefault(w => w.IsKeyWindow)
                ?? UIApplication.SharedApplication.KeyWindow;

            var tabController = FindTabBarController(window?.RootViewController);
            if (tabController?.TabBar?.Items is not { } items) return;
            if (tabIndex < 0 || tabIndex >= items.Length) return;

            items[tabIndex].BadgeValue = count > 0 ? count.ToString() : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TabBadge] iOS RenderBadge failed: {ex}");
        }
    }

    private static UITabBarController? FindTabBarController(UIViewController? vc)
    {
        while (vc is not null)
        {
            if (vc is UITabBarController tab) return tab;
            if (vc.PresentedViewController is not null)
            {
                var found = FindTabBarController(vc.PresentedViewController);
                if (found is not null) return found;
            }
            foreach (var child in vc.ChildViewControllers)
            {
                var found = FindTabBarController(child);
                if (found is not null) return found;
            }
            vc = null;
        }
        return null;
    }
}
