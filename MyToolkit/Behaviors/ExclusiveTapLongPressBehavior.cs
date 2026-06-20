using System.Windows.Input;

namespace MyToolkit.Behaviors;

/// <summary>
/// Attaches mutually exclusive Tap and LongPress commands to any View.
/// Exactly one command fires per interaction — long press consumes the gesture,
/// preventing tap; short release prevents long press.
///
/// State machine:
///   Idle → [DOWN] → Pressed → [timer expires] → Consumed (LongPress fires)
///                           → [UP before timer] → Idle     (Tap fires)
///   Consumed → [UP] → Idle (ignored — LongPress already fired)
/// </summary>
public partial class ExclusiveTapLongPressBehavior : Behavior<View>
{
    public static readonly BindableProperty TapCommandProperty =
        BindableProperty.Create(nameof(TapCommand), typeof(ICommand), typeof(ExclusiveTapLongPressBehavior));

    public static readonly BindableProperty TapCommandParameterProperty =
        BindableProperty.Create(nameof(TapCommandParameter), typeof(object), typeof(ExclusiveTapLongPressBehavior));

    public static readonly BindableProperty LongPressCommandProperty =
        BindableProperty.Create(nameof(LongPressCommand), typeof(ICommand), typeof(ExclusiveTapLongPressBehavior));

    public static readonly BindableProperty LongPressCommandParameterProperty =
        BindableProperty.Create(nameof(LongPressCommandParameter), typeof(object), typeof(ExclusiveTapLongPressBehavior));

    public static readonly BindableProperty LongPressDurationProperty =
        BindableProperty.Create(nameof(LongPressDuration), typeof(int), typeof(ExclusiveTapLongPressBehavior), 500);

    public ICommand? TapCommand
    {
        get => (ICommand?)GetValue(TapCommandProperty);
        set => SetValue(TapCommandProperty, value);
    }

    public object? TapCommandParameter
    {
        get => GetValue(TapCommandParameterProperty);
        set => SetValue(TapCommandParameterProperty, value);
    }

    public ICommand? LongPressCommand
    {
        get => (ICommand?)GetValue(LongPressCommandProperty);
        set => SetValue(LongPressCommandProperty, value);
    }

    public object? LongPressCommandParameter
    {
        get => GetValue(LongPressCommandParameterProperty);
        set => SetValue(LongPressCommandParameterProperty, value);
    }

    public int LongPressDuration
    {
        get => (int)GetValue(LongPressDurationProperty);
        set => SetValue(LongPressDurationProperty, value);
    }

    /// <summary>Fires when a short tap is recognised (before executing <see cref="TapCommand"/>).</summary>
    public event EventHandler? Tapped;

    /// <summary>Fires when a long press is recognised (before executing <see cref="LongPressCommand"/>).</summary>
    public event EventHandler? LongPressed;

    // The view this behavior is currently attached to — valid between OnAttachedTo and OnDetachingFrom.
    private View? _view;

    // Stored so the same delegate instance is unsubscribed — prevents a common leak.
    private readonly EventHandler _handlerChangedHandler;

    public ExclusiveTapLongPressBehavior()
    {
        _handlerChangedHandler = OnHandlerChanged;
    }

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);
        _view = bindable;
        bindable.HandlerChanged += _handlerChangedHandler;
        // Handler may already be present if the view is already in the tree.
        if (bindable.Handler?.PlatformView is not null)
            AttachNative(bindable);
    }

    protected override void OnDetachingFrom(View bindable)
    {
        _view = null;
        bindable?.HandlerChanged -= _handlerChangedHandler;
        DetachNative(bindable);
        base.OnDetachingFrom(bindable);
    }

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is not View view) 
            return;
        if (view.Handler?.PlatformView is not null)
            AttachNative(view);
        else
            DetachNative(view); // Handler cleared (view left visual tree) — clean up native refs.
    }

    // Called on the main thread. Safe to invoke directly or via BeginInvoke.
    private void FireTap()
    {
        Tapped?.Invoke(_view, EventArgs.Empty);
        var cmd = TapCommand;
        var param = TapCommandParameter;
        if (cmd?.CanExecute(param) == true)
            MainThread.BeginInvokeOnMainThread(() => cmd.Execute(param));
    }

    private void FireLongPress()
    {
        LongPressed?.Invoke(_view, EventArgs.Empty);
        var cmd = LongPressCommand;
        var param = LongPressCommandParameter;
        if (cmd?.CanExecute(param) == true)
            MainThread.BeginInvokeOnMainThread(() => cmd.Execute(param));
    }

    // Implemented in Platforms/Android and Platforms/iOS partials.
    // No-op on other platforms (compiler elides the call).
    partial void AttachNative(View view);
    partial void DetachNative(View view);
}
