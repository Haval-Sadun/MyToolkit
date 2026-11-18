using MyToolkit.Theme;

namespace MyToolkit.Controls;

/// <summary>A grey placeholder block that pulses softly while real content loads — a skeleton in the shape of what is coming, instead of a lone spinner on an empty screen.</summary>
/// <remarks>The pulse runs only while the block is on screen and visible: it starts when the control gets a handler and is aborted when it loses it or is hidden, so a finished load leaves no animation running. Generic, no app types.</remarks>
public sealed class SkeletonBlock : BoxView
{
    private const string PulseName = "skeleton-pulse";
    private const uint PulseLength = 1400;

    public SkeletonBlock()
    {
        CornerRadius = 8;
        InputTransparent = true;
        ThemeColors.Set(this, ColorProperty, "SurfaceContainerHigh");
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        UpdatePulse();
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == IsVisibleProperty.PropertyName)
            UpdatePulse();
    }

    private void UpdatePulse()
    {
        if (Handler is null || !IsVisible)
        {
            this.AbortAnimation(PulseName);
            Opacity = 1;
            return;
        }

        if (this.AnimationIsRunning(PulseName))
            return;

        var pulse = new Animation
        {
            { 0, 0.5, new Animation(value => Opacity = value, 0.5, 1, Easing.SinInOut) },
            { 0.5, 1, new Animation(value => Opacity = value, 1, 0.5, Easing.SinInOut) },
        };
        pulse.Commit(this, PulseName, length: PulseLength, repeat: () => true);
    }
}
