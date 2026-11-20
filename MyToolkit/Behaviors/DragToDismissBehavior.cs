using System.Diagnostics;
using System.Windows.Input;
using MyToolkit.Theme;

namespace MyToolkit.Behaviors;

/// <summary>What a drag looked like at one moment.</summary>
/// <param name="Distance">Dip dragged in the dismiss direction (never negative).</param>
/// <param name="Fraction">How far through the dragged view's size that is, 0 to 1.</param>
/// <param name="ReachedThreshold">Whether letting go now would dismiss it (by distance; a fling can also dismiss).</param>
public readonly record struct DragToDismissEventArgs(double Distance, double Fraction, bool ReachedThreshold);

/// <summary>
/// Hold a view (a sheet's handle or header) and drag: <see cref="Target"/> follows the finger; let go far or fast enough and it slides away and <see cref="Dismissed"/> fires, otherwise it springs back.
/// Attach to the view that is held, name what moves with <see cref="Target"/> (default: the held view itself), and do the actual hiding in <see cref="Dismissed"/> or <see cref="DismissCommand"/>.
/// It also serves a drag that opens something: set <see cref="Direction"/> to the way it opens, <see cref="MoveTarget"/> to false, <see cref="StartDistance"/> and <see cref="Extent"/>, and move the opening view yourself from <see cref="DragChanged"/>; "dismissed" then means "far or fast enough".
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
    public static readonly BindableProperty MoveTargetProperty = BindableProperty.Create(nameof(MoveTarget), typeof(bool), typeof(DragToDismissBehavior), true);
    public static readonly BindableProperty StartDistanceProperty = BindableProperty.Create(nameof(StartDistance), typeof(double), typeof(DragToDismissBehavior), 0d);
    public static readonly BindableProperty ExtentProperty = BindableProperty.Create(nameof(Extent), typeof(double), typeof(DragToDismissBehavior), 0d);

    private readonly PanGestureRecognizer _pan = new();
    private readonly PanVelocityTracker _velocity = new();
    private View? _held;
    private double _distance;
    private bool _settling;
    private bool _started;

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

    /// <summary>False when something else moves with the finger (a page being opened by the drag): the behavior then only measures the drag and reports it, and the caller animates in <see cref="Dismissed"/> and <see cref="Settled"/>.</summary>
    public bool MoveTarget
    {
        get => (bool)GetValue(MoveTargetProperty);
        set => SetValue(MoveTargetProperty, value);
    }

    /// <summary>How far (dip) the finger must travel in the drag direction, mostly along it, before the drag counts. 0 means it counts at once.</summary>
    public double StartDistance
    {
        get => (double)GetValue(StartDistanceProperty);
        set => SetValue(StartDistanceProperty, value);
    }

    /// <summary>The length (dip) the close distance is a share of; 0 means measure <see cref="Target"/>. Needed when nothing is moved by the behavior.</summary>
    public double Extent
    {
        get => (double)GetValue(ExtentProperty);
        set => SetValue(ExtentProperty, value);
    }

    /// <summary>A finger went down and began to drag (after <see cref="StartDistance"/>, if one is set).</summary>
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

    // Built once per setting change, not on every move of the finger.
    private DragDismissRules? _rules;

    private DragDismissRules Rules => _rules ??= new(CloseProgress, MinimumCloseDistance, FlingVelocity, MinimumFlingDistance);

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName is nameof(CloseProgress) or nameof(MinimumCloseDistance) or nameof(FlingVelocity) or nameof(MinimumFlingDistance))
            _rules = null;
    }

    private bool Vertical => Direction is DragDirection.Down or DragDirection.Up;

    // Down and Right move the target by a positive offset, Up and Left by a negative one.
    private double Sign => Direction is DragDirection.Down or DragDirection.Right ? 1 : -1;

    private double Along(double totalX, double totalY) => Sign * (Vertical ? totalY : totalX);

    private double ExtentOf(View view) => Extent > 0 ? Extent : Vertical ? view.Height : view.Width;

    private void Place(View view, double distance)
    {
        if (!MoveTarget)
            return;

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
                _velocity.Reset();
                _distance = 0;
                _started = false;
                if (StartDistance <= 0)
                    Begin(view);

                break;

            case GestureStatus.Running:
                var along = Along(e.TotalX, e.TotalY);
                // Not a drag yet: too short, or more across than along.
                if (!_started && (along < StartDistance || along <= Math.Abs(Vertical ? e.TotalX : e.TotalY)))
                    break;

                if (!_started)
                    Begin(view);

                _distance = Math.Max(0, along);
                _velocity.Add(_distance, Stopwatch.GetTimestamp());
                Place(view, _distance);
                var extent = ExtentOf(view);
                DragChanged?.Invoke(this, new DragToDismissEventArgs(_distance, DragDismissRules.Fraction(_distance, extent), Rules.HasReachedClose(_distance, extent)));
                break;

            // The release event often reports no distance on Android, so the last position seen while dragging decides.
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (_started)
                    await FinishAsync(view, Math.Max(_distance, Along(e.TotalX, e.TotalY)));

                break;
        }
    }

    private void Begin(View view)
    {
        _started = true;
        if (MoveTarget)
            view.CancelAnimations();

        DragStarted?.Invoke(this, EventArgs.Empty);
    }

    private async Task FinishAsync(View view, double distance)
    {
        _settling = true;
        try
        {
            var extent = ExtentOf(view);
            var velocity = _velocity.Velocity(Stopwatch.GetTimestamp(), Stopwatch.Frequency);
            if (Rules.ShouldDismiss(distance, velocity, extent))
            {
                var goal = Math.Max(extent, distance);
                var slide = DragDismissRules.SettleMilliseconds(goal - distance, velocity, SlideOutDuration);
                if (MoveTarget && !Motion.Reduced && slide > 0)
                    await SlideAsync(view, goal, slide, Easing.CubicIn);

                Dismissed?.Invoke(this, EventArgs.Empty);
                if (DismissCommand is { } command && command.CanExecute(null))
                    command.Execute(null);

                if (ResetAfterDismiss)
                    Place(view, 0);

                return;
            }

            var snap = DragDismissRules.SettleMilliseconds(distance, velocity, SnapBackDuration);
            if (Motion.Reduced || !MoveTarget || snap == 0)
                Place(view, 0);
            else
                await SlideAsync(view, 0, snap, Easing.CubicOut);

            Settled?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _settling = false;
            DragEnded?.Invoke(this, EventArgs.Empty);
        }
    }
}
