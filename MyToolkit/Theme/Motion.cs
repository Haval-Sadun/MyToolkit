namespace MyToolkit.Theme;

/// <summary>The app's motion language: one set of durations and curves, transform/opacity only (no layout work per frame), skipped when the system's animations are off.</summary>
public static class Motion
{
    public const uint Fast = 140;
    public const uint Standard = 240;

    /// <summary>True when the user turned system animations off (Android's "Remove animations"); everything then jumps to its end state.</summary>
    public static bool Reduced
    {
        get
        {
#if ANDROID
            var resolver = Android.App.Application.Context.ContentResolver;
            return resolver is not null && Android.Provider.Settings.Global.GetFloat(resolver, Android.Provider.Settings.Global.AnimatorDurationScale, 1f) == 0f;
#else
            return false;
#endif
        }
    }

    /// <summary>Appears by fading in while rising a few dips: for a view that takes another one's place.</summary>
    public static Task FadeSlideInAsync(VisualElement view, double fromY = 16, uint duration = Standard)
    {
        if (view.Handler is null)
            return Task.CompletedTask;

        view.CancelAnimations();
        if (Reduced)
        {
            Rest(view);
            return Task.CompletedTask;
        }

        view.Opacity = 0;
        view.TranslationY = fromY;
        return Task.WhenAll(view.FadeToAsync(1, duration, Easing.CubicOut), view.TranslateToAsync(0, 0, duration, Easing.CubicOut));
    }

    /// <summary>Settles in from slightly smaller and dimmer: for content that changed in place (a new cover).</summary>
    public static Task PopInAsync(VisualElement view, double fromScale = 0.96, double fromOpacity = 0.35, uint duration = Standard)
    {
        if (view.Handler is null)
            return Task.CompletedTask;

        view.CancelAnimations();
        if (Reduced)
        {
            Rest(view);
            return Task.CompletedTask;
        }

        view.Scale = fromScale;
        view.Opacity = fromOpacity;
        return Task.WhenAll(view.ScaleToAsync(1, duration, Easing.CubicOut), view.FadeToAsync(1, duration, Easing.CubicOut));
    }

    /// <summary>Stops running animations and puts the view back at rest, so a page that is shown again never starts half-faded.</summary>
    public static void Rest(VisualElement view)
    {
        view.CancelAnimations();
        view.Opacity = 1;
        view.Scale = 1;
        view.TranslationX = 0;
        view.TranslationY = 0;
    }
}
