namespace MyToolkit.Services;

/// <summary>Haptic feedback that can never fail the caller: a missing vibrate permission, no vibrator or an unsupported platform just means no click.</summary>
public static class Haptics
{
    public static void Click()
    {
        try
        {
            if (HapticFeedback.Default.IsSupported)
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch (Exception)
        {
            // Best-effort: feedback is a nicety, and it runs inside property-changed callbacks and gesture handlers that must not throw.
        }
    }
}
