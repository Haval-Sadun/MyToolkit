namespace MyToolkit.Services;

/// <summary>Relaunches the app so start-up-only settings (a color palette, the language) take effect. Android keeps the process alive when the
/// user just closes the app, so "reopen it" alone never reloads them: the old process has to end.</summary>
public static class AppRestarter
{
    public static bool IsSupported =>
#if ANDROID || WINDOWS
        true;
#else
        false;
#endif

    /// <summary>Starts a fresh instance, then ends this one. Returns only if restarting isn't supported here.</summary>
    public static void Restart()
    {
#if ANDROID
        var context = Microsoft.Maui.ApplicationModel.Platform.AppContext;
        var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
        if (launch?.Component is null)
            return;

        // A new task for the launcher activity, then end the process: the relaunch spawns a fresh one that reloads resources.
        var restart = Android.Content.Intent.MakeRestartActivityTask(launch.Component);
        context.StartActivity(restart);
        Java.Lang.Runtime.GetRuntime()?.Exit(0);
#elif WINDOWS
        if (Environment.ProcessPath is not { } exe)
            return;

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
        Application.Current?.Quit();
#endif
    }
}
