using System.Diagnostics;
using System.Windows.Input;
using MyToolkit.Theme;

namespace MyToolkit.Behaviors;

/// <summary>What a drag looked like at one moment.</summary>
/// <param name="Distance">Dip dragged in the dismiss direction (never negative).</param>
/// <param name="Fraction">How far through the dragged view's size that is, 0 to 1.</param>
/// <param name="ReachedThreshold">Whether letting go now would dismiss it (by distance; a fling can also dismiss).</param>
public sealed record DragToDismissEventArgs(double Distance, double Fraction, bool ReachedThreshold);

/// <summary>
/// Hold a view (a sheet's handle or header) and drag: <see cref="Target"/> follows the finger; let go far or fast enough and it slides away and <see cref="Dismissed"/> fires, otherwise it springs back.
/// Attach to the view that is held, name what moves with <see cref="Target"/> (default: the held view itself), and do the actual hiding in <see cref="Dismissed"/> or <see cref="DismissCommand"/>.
/// <code>&lt;Grid.Behaviors&gt;&lt;tk:DragToDismissBehavior Target="{x:Reference Sheet}" DismissCommand="{Binding HideCommand}" /&gt;&lt;/Grid.Behaviors&gt;</code>
/// </summary>
public sealed class DragToDismissBehavior : Behavior<View>
{
    public static readonly BindableProperty TargetProperty = BindableProperty.Create(nameof(Target), typeof(View), typeof(DragToDismissBehavior));
    public static readonly BindableProperty DirectionProperty = BindableProperty.Create(nameof(Direction), typeof(DragDirection), typeof(DragToDismissBehavior), DragDirection.Down);
    public static readonly BindableProperty CloseProgressProperty = BindableProperty.Create(nameof(CloseProgress), typeof(double), typeof(DragToDismissBehavior), 0.5);
    public static readonly BindableProperty MinimumCloseDistanceProperty = BindableProperty.Create(nameof(MinimumCloseDistance), typeof(double), typeof(DragToDismissBehavior), 0d);
    public static readonly BindableProperty FlingVelocityProperty = BindableProperty.Create(nameof(FlingVelocity), typeof(double), typeof(DragToDismissBehavior), 700d);
    public static readonly BindableProperty MinimumFlingDistanceProperty = BindableProperty.Create(nameof(MinimumFlingDistance), typeof(double), typeof(DragToDismissBehavior), 8d);
    public static readonly BindableProperty SlideOutDurationProperty = BindableProperty.Create(nameof(SlideOutDuration), typeof(uint), typeof(DragToDismissBehavior), 200u);
    public static readonly BindableProperty SnapBackDurationProperty = BindableProperty.Create(nameof(SnapBackDuration), typeof(uint), typeof(DragToDismissBehavior), 180u);
    public static readonly BindableProperty IsEnabledProperty = BindableProperty.Create(nameof(IsEnabled), typeof(bool), typeof(DragToDismissBehavior), true);
    public static readonly BindableProperty ResetAfterDismissProperty = BindableProperty.Create(nameof(ResetAfterDismiss), typeof(bool), typeof(DragToDismissBehavior), true);
    public static readonly BindableProperty DismissCommandProperty = BindableProperty.Create(nameof(DismissCommand), typeof(ICommand), typeof(DragToDismissBehavior));

    private readonly PanGestureRecognizer _pan = new();
    private readonly DragVelocity _velocity = new();
    private View? _held;
    private double _distance;
    private bool _settling;

    /// <summary>What moves with the finger; null means the held view.</summary>
    public View? Target
    {
        get => (View?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>The way a drag dismisses; any other direction is ignored.</summary>
    public DragDirection Direction
    {
        get => (DragDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    /// <summary>Share of the target's size past which letting go dismisses it (default 0.5, as in Flutter).</summary>
    public double CloseProgress
    {
        get => (double)GetValue(CloseProgressProperty);
        set => SetValue(CloseProgressProperty, value);
    }

    /// <summary>Floor for that distance in dip, so a small target still needs a deliberate drag.</summary>
    public double MinimumCloseDistance
    {
        get => (double)GetValue(MinimumCloseDistanceProperty);
        set => SetValue(MinimumCloseDistanceProperty, value);
    }

    /// <summary>A fling at least this fast (dip per second) dismisses from any distance.</summary>
    public double FlingVelocity
    {
        get => (double)GetValue(FlingVelocityProperty);
        set => SetValue(FlingVelocityProperty, value);
    }

    public double MinimumFlingDistance
    {
        get => (double)GetValue(MinimumFlingDistanceProperty);
        set => SetValue(MinimumFlingDistanceProperty, value);
    }

    public uint SlideOutDuration
    {
        get => (uint)GetValue(SlideOutDurationProperty);
        set => SetValue(SlideOutDurationProperty, value);
    }

    public uint SnapBackDuration
    {
        get => (uint)GetValue(SnapBackDurationProperty);
        set => SetValue(SnapBackDurationProperty, value);
    }

    /// <summary>False while the drag should do nothing (a sheet that is not open).</summary>
    public bool IsEnabled
    {
        get => (bool)GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    /// <summary>Put the target back at its resting place once <see cref="Dismissed"/> has run; the handler only has to hide it.</summary>
    public bool ResetAfterDismiss
    {
        get => (bool)GetValue(ResetAfterDismissProperty);
        set => SetValue(ResetAfterDismissProperty, value);
    }

    public ICommand? DismissCommand
    {
        get => (ICommand?)GetValue(DismissCommandProperty);
        set => SetValue(DismissCommandProperty, value);
    }

    /// <summary>A finger went down and began to drag.</summary>
    public event EventHandler? DragStarted;

    /// <summary>The finger moved and the target is already where it belongs; use this for a fade or a haptic tick.</summary>
    public event EventHandler<DragToDismissEventArgs>? DragChanged;

    /// <summary>Released far or fast enough, and the target has slid out of sight: hide it for real here.</summary>
    public event EventHandler? Dismissed;

    /// <summary>Released too soon: the target has sprung back.</summary>
    public event EventHandler? Settled;

    /// <summary>Last of every drag, after <see cref="Dismissed"/> or <see cref="Settled"/>.</summary>
    public event EventHandler? DragEnded;

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);
        _held = bindable;
        _pan.PanUpdated += OnPanUpdated;
        bindable.GestureRecognizers.Add(_pan);
    }

    protected override void OnDetachingFrom(View bindable)
    {
        _pan.PanUpdated -= OnPanUpdated;
        bindable.GestureRecognizers.Remove(_pan);
        _held = null;
        base.OnDetachingFrom(bindable);
    }

    private DragDismissRules Rules => new(CloseProgress, MinimumCloseDistance, FlingVelocity, MinimumFlingDistance);

    private bool Vertical => Direction is DragDirection.Down or DragDirection.Up;

    // Down and Right move the target by a positive offset, Up and Left by a negative one.
    private double Sign => Direction is DragDirection.Down or DragDirection.Right ? 1 : -1;

    private double Along(double totalX, double totalY) => Sign * (Vertical ? totalY : totalX);

    private double Extent(View view) => Vertical ? view.Height : view.Width;

    private void Place(View view, double distance)
    {
        if (Vertical)
            view.TranslationY = Sign * distance;
        else
            view.TranslationX = Sign * distance;
    }

    private Task SlideAsync(View view, double distance, uint duration, Easing easing) => Vertical ? view.TranslateToAsync(0, Sign * distance, duration, easing) : view.TranslateToAsync(Sign * distance, 0, duration, easing);

    private async void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_settling || !IsEnabled || (Target ?? _held) is not { } view)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                view.CancelAnimations();
                _velocity.Reset();
                _distance = 0;
                DragStarted?.Invoke(this, EventArgs.Empty);
                break;

            case GestureStatus.Running:
                _distance = Math.Max(0, Along(e.TotalX, e.TotalY));
                _velocity.Add(_distance, Stopwatch.GetTimestamp());
                Place(view, _distance);
                var extent = Extent(view);
                DragChanged?.Invoke(this, new DragToDismissEventArgs(_distance, DragDismissRules.Fraction(_distance, extent), Rules.HasReachedClose(_distance, extent)));
                break;

            // The release event often reports no distance on Android, so the last position seen while dragging decides.
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                await FinishAsync(view, Math.Max(_distance, Along(e.TotalX, e.TotalY)));
                break;
        }
    }

    private async Task FinishAsync(View view, double distance)
    {
        _settling = true;
        try
        {
            var extent = Extent(view);
            if (Rules.ShouldDismiss(distance, _velocity.Measure(Stopwatch.GetTimestamp(), Stopwatch.Frequency), extent))
            {
                if (!Motion.Reduced)
                    await SlideAsync(view, Math.Max(extent, distance), SlideOutDuration, Easing.CubicIn);

                Dismissed?.Invoke(this, EventArgs.Empty);
                if (DismissCommand is { } command && command.CanExecute(null))
                    command.Execute(null);

                if (ResetAfterDismiss)
                    Place(view, 0);

                return;
            }

            if (Motion.Reduced)
                Place(view, 0);
            else
                await SlideAsync(view, 0, SnapBackDuration, Easing.CubicOut);

            Settled?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _settling = false;
            DragEnded?.Invoke(this, EventArgs.Empty);
        }
    }
}
