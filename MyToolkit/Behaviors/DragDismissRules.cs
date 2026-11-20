namespace MyToolkit.Behaviors;

/// <summary>Which way a drag has to go to dismiss: a bottom sheet leaves downwards, a side panel sideways.</summary>
public enum DragDirection
{
    Down,
    Up,
    Left,
    Right,
}

/// <summary>When a released drag dismisses what is being dragged, with Flutter's defaults: past half its size, or flung fast enough. Pure and platform-free.</summary>
/// <param name="CloseProgress">Share of the dragged view's size past which a release dismisses it.</param>
/// <param name="MinimumCloseDistance">Never less than this many dip, however small the view (0 for no floor).</param>
/// <param name="FlingVelocity">A fling at least this fast (dip per second) dismisses from any distance.</param>
/// <param name="MinimumFlingDistance">A fling needs this much movement behind it, so a tap never dismisses.</param>
public sealed record DragDismissRules(double CloseProgress = 0.5, double MinimumCloseDistance = 0, double FlingVelocity = 700, double MinimumFlingDistance = 8)
{
    /// <summary>The distance at which a release dismisses, for a view <paramref name="extent"/> dip long along the drag.</summary>
    public double CloseDistance(double extent) => Math.Max(MinimumCloseDistance, extent * CloseProgress);

    /// <summary>Whether letting go at <paramref name="distance"/> would dismiss, ignoring any fling.</summary>
    public bool HasReachedClose(double distance, double extent) => CloseDistance(extent) is var close && close > 0 && distance >= close;

    public bool ShouldDismiss(double distance, double velocity, double extent)
    {
        if (distance <= 0)
            return false;

        return HasReachedClose(distance, extent) || (velocity >= FlingVelocity && distance >= MinimumFlingDistance);
    }

    /// <summary>How far through its own size the view has been dragged, 0 to 1 (0 when its size is not known yet).</summary>
    public static double Fraction(double distance, double extent) => extent <= 0 ? 0 : Math.Clamp(distance / extent, 0, 1);
}

/// <summary>How fast a finger is moving along one axis, from the last few move events: the pan gesture reports positions but no speed. Timestamps are passed in, so tests control the clock.</summary>
internal sealed class DragVelocity
{
    // Only the last moments say how the finger moves at release.
    private const double WindowSeconds = 0.12;

    private readonly List<(double Position, long Ticks)> _samples = [];

    public void Reset() => _samples.Clear();

    public void Add(double position, long ticks) => _samples.Add((position, ticks));

    /// <summary>Signed dip per second up to <paramref name="nowTicks"/>; 0 when there is too little to tell.</summary>
    public double Measure(long nowTicks, long ticksPerSecond)
    {
        var windowTicks = (long)(WindowSeconds * ticksPerSecond);
        var recent = _samples.Where(sample => nowTicks - sample.Ticks <= windowTicks).ToList();
        if (recent.Count < 2)
            return 0;

        var seconds = (recent[^1].Ticks - recent[0].Ticks) / (double)ticksPerSecond;
        return seconds <= 0 ? 0 : (recent[^1].Position - recent[0].Position) / seconds;
    }
}
