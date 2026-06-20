using Android.Content;
using AViews = Android.Views;

namespace MyToolkit.Behaviors;

// Why MotionEvent over GestureDetector:
// GestureDetector delays all taps by the double-tap timeout (~300 ms). We need
// instant tap feedback, so we own the raw MotionEvent stream and implement
// the tap/long-press decision ourselves with an explicit timer.
//
// Mutual exclusivity is guaranteed by a single atomic int (_stateInt) that both
// the main-thread ACTION_UP handler and the background timer race to transition.
// Interlocked.CompareExchange ensures exactly one wins; the loser does nothing.
public partial class ExclusiveTapLongPressBehavior
{
    private AViews.View? _androidView;
    private AndroidTouchListener? _touchListener;

    partial void AttachNative(View view)
    {
        if (view.Handler?.PlatformView is not AViews.View platformView)
            return;

        if (ReferenceEquals(_androidView, platformView))
            return;

        DetachAndroid();
        _androidView = platformView;

        // MAUI's Border/Frame native view is not Clickable by default.
        // Android only dispatches ACTION_DOWN to a touch listener when the view
        // is Clickable or LongClickable — without this, the listener is never called.
        platformView.Clickable = true;

        _touchListener = new AndroidTouchListener(this, platformView.Context!);
        platformView.SetOnTouchListener(_touchListener);
    }

    partial void DetachNative(View view) => DetachAndroid();

    private void DetachAndroid()
    {
        _androidView?.SetOnTouchListener(null);
        _touchListener?.Dispose(); // cancels any in-flight timer; resets state
        _touchListener = null;
        _androidView = null;
    }

    private sealed class AndroidTouchListener : Java.Lang.Object, AViews.View.IOnTouchListener
    {
        // Explicit int values so Interlocked.CompareExchange can work without casts.
        private enum GestureState { Idle = 0, Pressed = 1, Consumed = 2 }

        private readonly ExclusiveTapLongPressBehavior _behavior;
        private readonly float _touchSlop;

        // _stateInt is shared between the UI thread (MotionEvent) and the timer
        // thread. All reads/writes use Volatile or Interlocked.
        private int _stateInt;
        private CancellationTokenSource? _cts; // only touched from the UI thread
        private float _startX, _startY;

        public AndroidTouchListener(ExclusiveTapLongPressBehavior behavior, Context context)
        {
            _behavior = behavior;
            _touchSlop = AViews.ViewConfiguration.Get(context)!.ScaledTouchSlop;
        }

        public bool OnTouch(AViews.View? v, AViews.MotionEvent? e)
        {
            if (v is null || e is null) return false;

            switch (e.ActionMasked)
            {
                case AViews.MotionEventActions.Down:
                    _startX = e.GetX();
                    _startY = e.GetY();
                    Volatile.Write(ref _stateInt, (int)GestureState.Pressed);
                    StartTimer(v);
                    // Prevent the parent RecyclerView/ScrollView from stealing this
                    // touch stream while we are deciding between tap and long press.
                    v.Parent?.RequestDisallowInterceptTouchEvent(true);
                    return true;

                case AViews.MotionEventActions.Move:
                {
                    var state = (GestureState)Volatile.Read(ref _stateInt);
                    if (state == GestureState.Pressed)
                    {
                        float dx = MathF.Abs(e.GetX() - _startX);
                        float dy = MathF.Abs(e.GetY() - _startY);
                        if (dx > _touchSlop || dy > _touchSlop)
                        {
                            // User is scrolling — surrender the stream to the parent.
                            CancelTimer();
                            Volatile.Write(ref _stateInt, (int)GestureState.Idle);
                            v.Parent?.RequestDisallowInterceptTouchEvent(false);
                            return false;
                        }
                    }
                    return state != GestureState.Idle;
                }

                case AViews.MotionEventActions.Up:
                {
                    // Race with the timer: the first CAS from Pressed wins.
                    // If we win  → tap fires; timer is cancelled.
                    // If timer wins first → state is already Consumed; we just reset.
                    if (Interlocked.CompareExchange(ref _stateInt,
                            (int)GestureState.Idle, (int)GestureState.Pressed)
                        == (int)GestureState.Pressed)
                    {
                        CancelTimer();
                        v.Parent?.RequestDisallowInterceptTouchEvent(false);
                        _behavior.FireTap();
                    }
                    else if ((GestureState)Volatile.Read(ref _stateInt) == GestureState.Consumed)
                    {
                        // Long press already fired; lift is a no-op.
                        Volatile.Write(ref _stateInt, (int)GestureState.Idle);
                        v.Parent?.RequestDisallowInterceptTouchEvent(false);
                    }
                    return true;
                }

                case AViews.MotionEventActions.Cancel:
                    CancelTimer();
                    Volatile.Write(ref _stateInt, (int)GestureState.Idle);
                    v.Parent?.RequestDisallowInterceptTouchEvent(false);
                    return true;

                default:
                    return false;
            }
        }

        private void StartTimer(AViews.View view)
        {
            CancelTimer(); // discard any leftover CTS from a previous interaction
            var cts = new CancellationTokenSource();
            _cts = cts;
            var duration = _behavior.LongPressDuration;

            Task.Delay(duration, cts.Token).ContinueWith(t =>
            {
                if (t.IsCanceled) return;

                // Race with ACTION_UP: the first CAS from Pressed wins.
                if (Interlocked.CompareExchange(ref _stateInt,
                        (int)GestureState.Consumed, (int)GestureState.Pressed)
                    == (int)GestureState.Pressed)
                {
                    // Release the scroll lock on the UI thread before firing the command.
                    view.Post(() => view.Parent?.RequestDisallowInterceptTouchEvent(false));
                    _behavior.FireLongPress();
                }
            }, TaskScheduler.Default);
        }

        private void CancelTimer()
        {
            // _cts is only written/read on the UI thread, so no Interlocked needed.
            var cts = _cts;
            _cts = null;
            cts?.Cancel();
            cts?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CancelTimer();
                // Setting Idle here ensures a timer continuation that slipped past
                // the IsCanceled check cannot fire a command after disposal.
                Volatile.Write(ref _stateInt, (int)GestureState.Idle);
            }
            base.Dispose(disposing);
        }
    }
}
