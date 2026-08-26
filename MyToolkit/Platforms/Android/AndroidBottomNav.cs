using Android.Views;
using Google.Android.Material.BottomNavigation;

namespace MyToolkit.Platforms.Android;

/// <summary>Shared view-tree walk for locating Shell's native BottomNavigationView.</summary>
internal static class AndroidBottomNav
{
    internal static BottomNavigationView? Find(ViewGroup parent)
    {
        for (int i = 0; i < parent.ChildCount; i++)
        {
            var child = parent.GetChildAt(i);
            if (child is BottomNavigationView bnv) return bnv;
            if (child is ViewGroup vg)
            {
                var found = Find(vg);
                if (found is not null) return found;
            }
        }
        return null;
    }
}
