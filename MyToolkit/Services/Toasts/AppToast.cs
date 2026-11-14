namespace MyToolkit.Services.Toasts;

/// <summary>The one way an app shows a toast: confirmations and errors alike. A swipe-dismissable native snackbar on Android and a native banner on Windows (the CommunityToolkit one needs package identity); other platforms show nothing.</summary>
public static class AppToast
{
    /// <summary>Best-effort and fire-and-forget: a failed toast must never be mistaken for a failed action.</summary>
    public static void Show(string message, Action? action = null, string? actionText = null, TimeSpan? duration = null) => MainThread.BeginInvokeOnMainThread(async () =>
    {
        try
        {
            await ShowAsync(message, duration ?? TimeSpan.FromSeconds(action is null ? 3 : 5), action, actionText);
        }
        catch (Exception ex)
        {
            // Never surfaced to the user, only traced.
            ServiceHelper.GetSafe<AppLogger>()?.Log("WARN", $"toast.failed error={ex.GetType().Name}: {ex.Message}");
        }
    });

    /// <summary>Must run on the UI thread. Throws if the platform toast fails; callers decide how to report that.</summary>
    public static async Task ShowAsync(string message, TimeSpan duration, Action? action, string? actionText)
    {
#if WINDOWS
        await MyToolkit.Platforms.Windows.WindowsBanner.ShowAsync(message, duration, action, actionText);
#elif ANDROID
        MyToolkit.Platforms.Android.AndroidSnackbar.Show(message, duration, action, actionText);
        await Task.CompletedTask;
#else
        await Task.CompletedTask;
#endif
    }
}
