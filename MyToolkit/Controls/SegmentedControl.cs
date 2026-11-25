using Microsoft.Maui.Controls.Shapes;
using MyToolkit.Services;
using MyToolkit.Theme;

namespace MyToolkit.Controls;

/// <summary>A two-way switch drawn as a pill with two segments, for dividing a page into two parts (Music / Audiobooks). Tapping a segment picks it; a raised thumb slides under the chosen one. Give it a <see cref="SwipeTarget"/> (the content it switches) and the content follows the finger as it is dragged sideways, then slides to the other segment, or springs back when let go early; a swipe on the pill switches too.
/// A drag that began at a screen edge is ignored (see <see cref="EdgeSwipeGuard"/>), so the system's back gesture keeps working there, and one that starts out vertical is left to the content's own scrolling. On Android a drag is routed from the activity (see <c>SegmentSwipeRouter</c>: the app calls it from <c>DispatchTouchEvent</c>), elsewhere it is a pan gesture.</summary>
/// <remarks>Generic, no app types, like <see cref="PillButton"/>: colors come from the palette tokens (<c>SurfaceContainerHigh</c>, <c>Surface</c>, <c>OnSurface</c>, <c>OnSurfaceVariant</c>).</remarks>
public sealed class SegmentedControl : ContentView
{
    /// <summary>The swipe distance, in dp, that counts as a swipe on the pill.</summary>
    private const uint SwipeThreshold = 80;

    /// <summary>How far, in dp, a drag must go before it is told apart from a scroll.</summary>
    private const double DecideAfter = 10;

    /// <summary>A drag is horizontal when it goes this much further sideways than up or down.</summary>
    private const double HorizontalBias = 1.5;

    /// <summary>The share of the content's width a drag must reach to switch (the lesser of this and <see cref="CommitMaxDistance"/>).</summary>
    private const double CommitFraction = 0.3;

    private const double CommitMaxDistance = 140;

    /// <summary>Dragging towards a segment that does not exist moves the content only this much of the finger's distance: a rubber band.</summary>
    private const double RubberBand = 0.25;

    public static readonly BindableProperty FirstTextProperty = BindableProperty.Create(nameof(FirstText), typeof(string), typeof(SegmentedControl), string.Empty, propertyChanged: (b, _, v) => ((SegmentedControl)b)._firstLabel.Text = (string)v);

    public static readonly BindableProperty SecondTextProperty = BindableProperty.Create(nameof(SecondText), typeof(string), typeof(SegmentedControl), string.Empty, propertyChanged: (b, _, v) => ((SegmentedControl)b)._secondLabel.Text = (string)v);

    /// <summary>0 for the first segment, 1 for the second.</summary>
    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(SegmentedControl), 0, BindingMode.TwoWay, propertyChanged: (b, _, _) => ((SegmentedControl)b).OnSelectedChanged());

    /// <summary>The content this switch controls: dragging it left or right moves to the other segment.</summary>
    public static readonly BindableProperty SwipeTargetProperty = BindableProperty.Create(nameof(SwipeTarget), typeof(View), typeof(SegmentedControl), null, propertyChanged: (b, _, _) => ((SegmentedControl)b).RefreshSwipe());

    private enum Drag
    {
        Idle,
        Undecided,
        Moving,
        Ignored,
    }

    private readonly Border _thumb;
    private readonly Label _firstLabel;
    private readonly Label _secondLabel;

    private SwipeGestureRecognizer? _pillLeft;
    private SwipeGestureRecognizer? _pillRight;
    private PanGestureRecognizer? _pan;
    private View? _panned;

    /// <summary>What moves with the finger. The gesture's own element must stay still when a pan recognizer measures the finger, or the distance it reports is measured from a frame that moves with it (Windows).</summary>
    private List<View> _movers = [];
    private Drag _drag = Drag.Idle;
    private bool _animating;
    private bool _thumbReady;

    /// <summary>The last distance seen while the finger was moving: the final event of a pan can report 0 (Android), so a release is judged by this.</summary>
    private double _lastX;

    public SegmentedControl()
    {
        _thumb = new Border { StrokeThickness = 0, HorizontalOptions = LayoutOptions.Fill, StrokeShape = new RoundRectangle { CornerRadius = 17 } };
        ThemeColors.Set(_thumb, BackgroundColorProperty, "Surface");

        _firstLabel = new Label { FontSize = 14, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        _secondLabel = new Label { FontSize = 14, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };

        var first = Segment(_firstLabel, 0);
        var second = Segment(_secondLabel, 1);

        var grid = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)] };
        grid.Add(_thumb);
        grid.Add(first);
        grid.Add(second, 1);

        var pill = new Border
        {
            HeightRequest = 40,
            Padding = new Thickness(3),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            Content = grid,
        };
        ThemeColors.Set(pill, BackgroundColorProperty, "SurfaceContainerHigh");

        Content = pill;
        ApplyTone();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string FirstText
    {
        get => (string)GetValue(FirstTextProperty);
        set => SetValue(FirstTextProperty, value);
    }

    public string SecondText
    {
        get => (string)GetValue(SecondTextProperty);
        set => SetValue(SecondTextProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public View? SwipeTarget
    {
        get => (View?)GetValue(SwipeTargetProperty);
        set => SetValue(SwipeTargetProperty, value);
    }

    // A transparent segment holding a label; the tap recognizer is owned by the segment, same lifetime, nothing to unsubscribe.
    private Border Segment(Label label, int index)
    {
        var segment = new Border { StrokeThickness = 0, BackgroundColor = Colors.Transparent, Content = label };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => SelectedIndex = index;
        segment.GestureRecognizers.Add(tap);
        return segment;
    }

    // The chosen label is semibold in the main text color, the other regular in the muted one.
    private void ApplyTone()
    {
        foreach (var (label, selected) in new[] { (_firstLabel, SelectedIndex == 0), (_secondLabel, SelectedIndex == 1) })
        {
            label.FontFamily = selected ? "OpenSansSemibold" : null;
            ThemeColors.Set(label, Label.TextColorProperty, selected ? "OnSurface" : "OnSurfaceVariant");
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _thumb.SizeChanged -= OnThumbSizeChanged;
        _thumb.SizeChanged += OnThumbSizeChanged;
        MoveThumb(false);
        _thumbReady = true;
        RefreshSwipe();
    }

    // Unloaded isn't guaranteed when a page is torn down, so a handler removal detaches too: the recognizers hold this control alive through their handlers.
    private void OnUnloaded(object? sender, EventArgs e) => Detach();

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);
        if (args.NewHandler is null)
            Detach();
    }

    private void Detach()
    {
        _thumbReady = false;
        _thumb.SizeChanged -= OnThumbSizeChanged;
        _thumb.CancelAnimations();
        DetachSwipe();
    }

    // A resize (rotation, window) changes how far the thumb has to travel.
    private void OnThumbSizeChanged(object? sender, EventArgs e) => MoveThumb(false);

    private void OnSelectedChanged()
    {
        ApplyTone();
        if (!_thumbReady)
            return;

        MoveThumb(true);
        Haptics.Click();
    }

    /// <summary>Slides the raised thumb under the chosen segment. TranslationX is physical, so a right-to-left layout (where the first segment is on the right) travels the other way.</summary>
    private void MoveThumb(bool animate)
    {
        _thumb.CancelAnimations();
        var target = SelectedIndex * _thumb.Width * (IsRightToLeft ? -1 : 1);
        if (!animate || Motion.Reduced)
        {
            _thumb.TranslationX = target;
            return;
        }

        _ = _thumb.TranslateToAsync(target, 0, Motion.Standard, Easing.CubicOut);
    }

    private void RefreshSwipe()
    {
        DetachSwipe();
        if (!IsLoaded)
            return;

        // The pill switches on a quick swipe; the content follows the finger.
        _pillLeft = new SwipeGestureRecognizer { Direction = SwipeDirection.Left, Threshold = SwipeThreshold };
        _pillRight = new SwipeGestureRecognizer { Direction = SwipeDirection.Right, Threshold = SwipeThreshold };
        _pillLeft.Swiped += OnPillSwiped;
        _pillRight.Swiped += OnPillSwiped;
        GestureRecognizers.Add(_pillLeft);
        GestureRecognizers.Add(_pillRight);

        if (SwipeTarget is not { } target)
            return;

        _panned = target;
#if ANDROID
        // A pan recognizer on a parent never hears a drag that a scrolling list child already took: the activity routes the drag instead.
        MyToolkit.Platforms.Android.SegmentSwipeRouter.Register(this);
#else
        _pan = new PanGestureRecognizer();
        _pan.PanUpdated += OnPanUpdated;
        target.GestureRecognizers.Add(_pan);
#endif
    }

    private void DetachSwipe()
    {
        if (_pillLeft is not null && _pillRight is not null)
        {
            _pillLeft.Swiped -= OnPillSwiped;
            _pillRight.Swiped -= OnPillSwiped;
            GestureRecognizers.Remove(_pillLeft);
            GestureRecognizers.Remove(_pillRight);
        }

        _pillLeft = null;
        _pillRight = null;

#if ANDROID
        MyToolkit.Platforms.Android.SegmentSwipeRouter.Unregister(this);
#endif
        if (_pan is not null && _panned is not null)
        {
            _pan.PanUpdated -= OnPanUpdated;
            _panned.GestureRecognizers.Remove(_pan);
        }

        Reset(_movers);

        _movers = [];

        _pan = null;
        _panned = null;
        _drag = Drag.Idle;
        _animating = false;
    }

    /// <summary>Android: the router starts a drag. False while the content is still settling from the last one.</summary>
    internal bool TryBeginDrag()
    {
        if (_animating || _panned is not { } panned)
            return false;

        // The router measures the finger itself, so the target can move: one view instead of every child on each frame.
        _movers = [panned];
        _drag = Drag.Moving;
        return true;
    }

    /// <summary>Android: the finger moved to this distance, in dp, from where the drag began.</summary>
    internal void DragTo(double dx)
    {
        _lastX = dx;
        Follow(_movers, dx);
    }

    /// <summary>Android: the finger lifted (or the touch was cancelled: distance 0, so the content springs back).</summary>
    internal void EndDrag(double dx)
    {
        _drag = Drag.Idle;
        _ = FinishAsync(_movers, _panned?.Width ?? 1, dx);
    }

    // The app's chosen reading direction: a view's own FlowDirection is "inherit" and says nothing about it.
    private static bool IsRightToLeft => Direction.IsRightToLeft;

    /// <summary>The segment a drag of this sign leads to, or null when there is none that way. Dragging left reveals what is to the right: the second segment (the other way round in a right-to-left layout).</summary>
    private int? TargetFor(double dx)
    {
        var towardsSecond = dx < 0 != IsRightToLeft;
        return towardsSecond && SelectedIndex == 0 ? 1 : !towardsSecond && SelectedIndex == 1 ? 0 : null;
    }

    private void OnPillSwiped(object? sender, SwipedEventArgs e)
    {
        // Left of the screen is the system's back; leave a swipe that began there to it.
        if (EdgeSwipeGuard.StartedAtEdge())
            return;

        if (TargetFor(e.Direction == SwipeDirection.Left ? -1 : 1) is { } index)
            SelectedIndex = index;
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_panned is not { } panned || _animating)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                // Where the finger went down decides: a drag from the edge belongs to the system's back gesture.
                _drag = EdgeSwipeGuard.StartedAtEdge() ? Drag.Ignored : Drag.Undecided;
                if (_drag == Drag.Ignored)
                    ServiceHelper.GetSafe<AppLogger>()?.LogInfo($"ui.segment_swipe_ignored reason=edge down_x={Math.Round(EdgeSwipeGuard.LastDownX)}");

                _lastX = 0;
                _movers = MoversOf(panned);
                break;

            case GestureStatus.Running:
                if (_drag == Drag.Undecided)
                    _drag = Decide(e.TotalX, e.TotalY);

                if (_drag == Drag.Moving)
                {
                    _lastX = e.TotalX;
                    Follow(_movers, e.TotalX);
                }

                break;

            case GestureStatus.Completed:
                if (_drag == Drag.Moving)
                    _ = FinishAsync(_movers, panned.Width, e.TotalX != 0 ? e.TotalX : _lastX);

                _drag = Drag.Idle;
                break;

            // Windows can report the release of a mouse drag as canceled: judged like a completed one, so a long drag still switches and a short one springs back.
            case GestureStatus.Canceled:
                if (_drag == Drag.Moving)
                    _ = FinishAsync(_movers, panned.Width, _lastX);

                _drag = Drag.Idle;
                break;
        }
    }

    // Sideways far enough and clearly more than up or down: ours. Up or down first: a scroll, leave it.
    private static Drag Decide(double x, double y)
    {
        if (Math.Abs(x) >= DecideAfter && Math.Abs(x) > Math.Abs(y) * HorizontalBias)
            return Drag.Moving;

        return Math.Abs(y) >= DecideAfter ? Drag.Ignored : Drag.Undecided;
    }

    /// <summary>The views that move: a layout's children, a scroll view's or border's content; a view with neither moves itself.</summary>
    private static List<View> MoversOf(View target) => target switch
    {
        Layout layout => layout.Children.OfType<View>().ToList(),
        ScrollView { Content: { } content } => [content],
        ContentView { Content: { } content } => [content],
        Border { Content: { } content } => [content],
        _ => [target],
    };

    // The content moves with the finger, dimming a little as it leaves; towards a segment that does not exist it stretches like a rubber band.
    private void Follow(List<View> movers, double totalX)
    {
        var dx = TargetFor(totalX) is null ? totalX * RubberBand : totalX;
        var width = Math.Max(1, _panned?.Width ?? 1);
        var opacity = 1 - Math.Min(0.4, Math.Abs(dx) / width * 0.8);
        foreach (var mover in movers)
        {
            mover.TranslationX = dx;
            mover.Opacity = opacity;
        }
    }

    private async Task FinishAsync(List<View> movers, double width, double totalX)
    {
        width = Math.Max(1, width);
        var commit = Math.Abs(totalX) >= Math.Min(width * CommitFraction, CommitMaxDistance);
        if (!commit || TargetFor(totalX) is not { } index)
        {
            await SettleAsync(movers);
            return;
        }

        _animating = true;
        try
        {
            var direction = Math.Sign(totalX);
            if (!Motion.Reduced)
            {
                // Out the way it was going, then the other segment comes in from the opposite side.
                await Task.WhenAll(movers.SelectMany(mover => new[] { mover.TranslateToAsync(direction * width, 0, Motion.Fast, Easing.CubicIn), mover.FadeToAsync(0, Motion.Fast, Easing.CubicIn) }));
                SelectedIndex = index;
                foreach (var mover in movers)
                    mover.TranslationX = -direction * width * 0.35;

                await Task.WhenAll(movers.SelectMany(mover => new[] { mover.TranslateToAsync(0, 0, Motion.Standard, Easing.CubicOut), mover.FadeToAsync(1, Motion.Standard, Easing.CubicOut) }));
            }
            else
            {
                SelectedIndex = index;
            }
        }
        finally
        {
            Reset(movers);
            _animating = false;
        }
    }

    // Let go too early, or dragged where there is nothing: back to rest.
    private async Task SettleAsync(List<View> movers)
    {
        _animating = true;
        try
        {
            if (Motion.Reduced)
                return;

            await Task.WhenAll(movers.SelectMany(mover => new[] { mover.TranslateToAsync(0, 0, Motion.Fast, Easing.CubicOut), mover.FadeToAsync(1, Motion.Fast, Easing.CubicOut) }));
        }
        finally
        {
            Reset(movers);
            _animating = false;
        }
    }

    private static void Reset(List<View> movers)
    {
        foreach (var mover in movers)
        {
            mover.CancelAnimations();
            mover.TranslationX = 0;
            mover.Opacity = 1;
        }
    }
}
