using Android.App;
using Android.Views;
using MyToolkit.Controls;

namespace MyToolkit.Platforms.Android;

/// <summary>Routes a sideways drag over a <see cref="SegmentedControl"/>'s content to that control, before any view sees it.
/// A MAUI pan recognizer on a parent never hears a drag that a scrolling list (RecyclerView) child already took; the activity sees every touch first, so the router can follow the finger itself.
/// It steps aside for a drag that starts at the screen edge (the system's back gesture), that starts out vertical (a scroll), or that a sideways-scrolling view under the finger can still use (a carousel).</summary>
public static class SegmentSwipeRouter
{
    /// <summary>How far, in dp, a drag must go before it is told apart from a scroll.</summary>
    private const double DecideAfterDp = 10;

    private const double HorizontalBias = 1.5;

    private enum State
    {
        Idle,
        Pending,
        Dragging,
    }

    // Weak: a control that was never unregistered must not be kept alive by this static list.
    private static readonly List<WeakReference<SegmentedControl>> Controls = [];

    private static State _state = State.Idle;
    private static SegmentedControl? _active;
    private static global::Android.Views.View? _downView;
    private static float _downX;
    private static float _downY;
    private static float _startX;

    public static void Register(SegmentedControl control)
    {
        Controls.RemoveAll(reference => !reference.TryGetTarget(out _));
        if (!Controls.Any(reference => reference.TryGetTarget(out var known) && known == control))
            Controls.Add(new WeakReference<SegmentedControl>(control));
    }

    public static void Unregister(SegmentedControl control)
    {
        Controls.RemoveAll(reference => !reference.TryGetTarget(out var known) || known == control);
        if (_active == control)
            Reset();
    }

    /// <summary>Call from the activity's <c>DispatchTouchEvent</c>: <c>if (SegmentSwipeRouter.Handle(this, e, cancel => base.DispatchTouchEvent(cancel))) return true;</c>. True when the router took the event (the rest of a drag it follows); false lets it through to the views.</summary>
    public static bool Handle(Activity activity, MotionEvent e, Func<MotionEvent, bool> dispatch)
    {
        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                // Noted before anything handles it: a swipe asks where its finger started, to leave the system's edge back gesture alone.
                EdgeSwipeGuard.RecordDown(e.RawX);
                Reset();
                if (e.PointerCount == 1 && !EdgeSwipeGuard.StartedAtEdge() && FindControl(e.RawX, e.RawY) is { } control)
                {
                    _active = control;
                    _downX = e.RawX;
                    _downY = e.RawY;
                    _downView = HitView(activity.Window?.DecorView, (int)e.RawX, (int)e.RawY);
                    _state = State.Pending;
                }

                return false;

            case MotionEventActions.PointerDown:
                // A second finger: not a swipe.
                return EndDrag(cancelled: true);

            case MotionEventActions.Move:
                return OnMove(activity, e, dispatch);

            case MotionEventActions.Up:
                return EndDrag(cancelled: false, e.RawX);

            case MotionEventActions.Cancel:
                return EndDrag(cancelled: true);

            default:
                return _state == State.Dragging;
        }
    }

    private static bool OnMove(Activity activity, MotionEvent e, Func<MotionEvent, bool> dispatch)
    {
        if (_active is not { } control)
            return false;

        var density = Density(activity);
        if (_state == State.Pending)
        {
            var dx = (e.RawX - _downX) / density;
            var dy = (e.RawY - _downY) / density;
            if (Math.Abs(dx) < DecideAfterDp && Math.Abs(dy) < DecideAfterDp)
                return false;

            // Up or down first: a scroll, leave it to the list. Sideways but a carousel under the finger can still scroll that way: leave it to the carousel.
            if (Math.Abs(dx) <= Math.Abs(dy) * HorizontalBias || CanScrollSideways(dx < 0 ? 1 : -1) || !control.TryBeginDrag())
            {
                Reset();
                return false;
            }

            // The views under the finger are told the touch is over, so a row does not stay pressed or a list start scrolling while the content moves.
            if (MotionEvent.Obtain(e) is { } cancel)
            {
                cancel.Action = MotionEventActions.Cancel;
                dispatch(cancel);
                cancel.Recycle();
            }

            _startX = e.RawX;
            _state = State.Dragging;
        }

        if (_state != State.Dragging)
            return false;

        control.DragTo((e.RawX - _startX) / density);
        return true;
    }

    private static bool EndDrag(bool cancelled, float rawX = 0)
    {
        var dragging = _state == State.Dragging;
        if (dragging && _active is { } control)
            control.EndDrag(cancelled ? 0 : (rawX - _startX) / Density(null));

        Reset();
        return dragging;
    }

    private static void Reset()
    {
        _state = State.Idle;
        _active = null;
        _downView = null;
    }

    private static float Density(Activity? activity) => (activity ?? global::Microsoft.Maui.ApplicationModel.Platform.CurrentActivity)?.Resources?.DisplayMetrics?.Density is { } density and > 0 ? density : 1;

    /// <summary>Whether the view the finger went down on, or any parent of it, can still scroll sideways in that direction (a carousel).</summary>
    private static bool CanScrollSideways(int direction)
    {
        for (var view = _downView; view is not null; view = view.Parent as global::Android.Views.View)
        {
            if (view.CanScrollHorizontally(direction))
                return true;
        }

        return false;
    }

    /// <summary>The control whose content is on screen under this point.</summary>
    private static SegmentedControl? FindControl(float rawX, float rawY)
    {
        foreach (var reference in Controls)
        {
            if (!reference.TryGetTarget(out var control) || control.SwipeTarget?.Handler?.PlatformView is not global::Android.Views.View target || !target.IsShown)
                continue;

            var location = new int[2];
            target.GetLocationOnScreen(location);
            if (rawX >= location[0] && rawX < location[0] + target.Width && rawY >= location[1] && rawY < location[1] + target.Height)
                return control;
        }

        return null;
    }

    /// <summary>The deepest visible view under this screen point.</summary>
    private static global::Android.Views.View? HitView(global::Android.Views.View? root, int x, int y)
    {
        if (root is not ViewGroup group)
            return root;

        for (var i = group.ChildCount - 1; i >= 0; i--)
        {
            var child = group.GetChildAt(i);
            if (child is null || child.Visibility != ViewStates.Visible)
                continue;

            var location = new int[2];
            child.GetLocationOnScreen(location);
            if (x >= location[0] && x < location[0] + child.Width && y >= location[1] && y < location[1] + child.Height)
                return HitView(child, x, y) ?? child;
        }

        return root;
    }
}
