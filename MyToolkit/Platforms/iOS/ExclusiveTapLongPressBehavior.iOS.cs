using UIKit;

namespace MyToolkit.Behaviors;

// Why RequireGestureRecognizerToFail is the correct iOS approach:
// iOS UIGestureRecognizer has a built-in dependency mechanism.
// tapRecognizer.RequireGestureRecognizerToFail(longPressRecognizer) tells UIKit:
//   "delay recognising the tap until the long press has definitively failed".
// The long press fails when the finger lifts before MinimumPressDuration elapses.
// If the long press succeeds (Began fires), UIKit puts the tap into the Failed
// state automatically — it never calls the tap action. This is platform-native
// mutual exclusivity; no timer or CAS needed on iOS.
public partial class ExclusiveTapLongPressBehavior
{
    private UIView? _iosView;
    private UITapGestureRecognizer? _tapRecognizer;
    private UILongPressGestureRecognizer? _longPressRecognizer;

    partial void AttachNative(View view)
    {
        if (view.Handler?.PlatformView is not UIView platformView)
            return;

        if (ReferenceEquals(_iosView, platformView))
            return;

        DetachIos();

        _iosView = platformView;

        _longPressRecognizer = new UILongPressGestureRecognizer(OnLongPress)
        {
            MinimumPressDuration = LongPressDuration / 1000.0,
            // Don't cancel touch delivery to views — lets parent UIScrollView
            // still receive touches for scrolling decisions.
            CancelsTouchesInView = false
        };

        _tapRecognizer = new UITapGestureRecognizer(OnTap)
        {
            CancelsTouchesInView = false
        };

        // The tap will only be recognised after the system confirms the long press
        // did NOT begin. If the long press fires, the tap is silently cancelled.
        _tapRecognizer.RequireGestureRecognizerToFail(_longPressRecognizer);

        platformView.AddGestureRecognizer(_longPressRecognizer);
        platformView.AddGestureRecognizer(_tapRecognizer);
        platformView.UserInteractionEnabled = true;
    }

    partial void DetachNative(View view) => DetachIos();

    private void DetachIos()
    {
        if (_tapRecognizer is not null)
        {
            _iosView?.RemoveGestureRecognizer(_tapRecognizer);
            _tapRecognizer.Dispose();
            _tapRecognizer = null;
        }
        if (_longPressRecognizer is not null)
        {
            _iosView?.RemoveGestureRecognizer(_longPressRecognizer);
            _longPressRecognizer.Dispose();
            _longPressRecognizer = null;
        }
        _iosView = null;
    }

    private void OnLongPress(UILongPressGestureRecognizer recognizer)
    {
        // UILongPressGestureRecognizer transitions: Began → Changed → Ended/Cancelled.
        // Fire exactly once, on Began. Subsequent state changes are ignored.
        if (recognizer.State == UIGestureRecognizerState.Began)
            FireLongPress();
    }

    private void OnTap(UITapGestureRecognizer recognizer) => FireTap();
}
