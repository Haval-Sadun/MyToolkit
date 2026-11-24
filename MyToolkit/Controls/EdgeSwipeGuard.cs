namespace MyToolkit.Controls;

/// <summary>Tells whether the touch that just swiped began at a screen edge, where the system's own back gesture lives, so an in-app swipe can step aside and leave that edge to the system.</summary>
/// <remarks>The system claims a touch that starts in its edge strip before the app sees any of it, so this only has to cover the sliver the app can still see: a narrow strip, never the wide inset some phones report for a side panel, which would swallow ordinary swipes.</remarks>
public static class EdgeSwipeGuard
{
    /// <summary>The narrowest edge strip left to the system, in dp, even where the system reports none (3-button navigation).</summary>
    private const double MinEdgeDp = 16;

    /// <summary>The widest strip, in dp, however much the system reports: the back gesture itself is about this wide.</summary>
    private const double MaxEdgeDp = 32;

    private static float _downX = -1;

    /// <summary>Where the last touch went down, in pixels; -1 when none was recorded.</summary>
    public static float LastDownX => _downX;

    /// <summary>Called for every touch-down by the Android activity. Elsewhere nothing records, so no swipe counts as an edge swipe.</summary>
    public static void RecordDown(float rawX) => _downX = rawX;

    /// <summary>Pure, so it is unit-tested. The strip follows the system's gesture inset on that side, kept between <see cref="MinEdgeDp"/> and <see cref="MaxEdgeDp"/>.</summary>
    public static bool IsInEdgeZone(float downX, float widthPx, float leftInsetPx, float rightInsetPx, float density)
    {
        if (downX < 0 || widthPx <= 0)
            return false;

        var scale = Math.Max(1, density);
        var minimum = (float)(MinEdgeDp * scale);
        var maximum = (float)(MaxEdgeDp * scale);
        return downX <= Math.Clamp(leftInsetPx, minimum, maximum) || downX >= widthPx - Math.Clamp(rightInsetPx, minimum, maximum);
    }

#if ANDROID
#pragma warning disable CA1422
    /// <summary>Whether the last touch went down in the system gesture strip at either edge.</summary>
    public static bool StartedAtEdge()
    {
        if (_downX < 0 || Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not { } activity || activity.Resources?.DisplayMetrics is not { } metrics)
            return false;

        var insets = activity.Window?.DecorView.RootWindowInsets?.SystemGestureInsets;
        return IsInEdgeZone(_downX, metrics.WidthPixels, insets?.Left ?? 0, insets?.Right ?? 0, metrics.Density);
    }
#pragma warning restore CA1422
#else
    public static bool StartedAtEdge() => false;
#endif
}
