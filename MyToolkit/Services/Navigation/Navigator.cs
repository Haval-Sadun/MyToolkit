using Microsoft.Extensions.DependencyInjection;

namespace MyToolkit.Services.Navigation;

/// <summary>
/// Thin, centralized wrapper around Shell navigation. Every navigation:
///   - runs on the UI thread (callers may be on a background continuation),
///   - is guarded against re-entrancy so a double-tap can't push the same page twice,
///   - never throws to the caller — failures are routed to <see cref="INavigationDiagnostics"/>.
/// This is intentionally a static helper, not a DI service: navigation has no per-call
/// state and a single global re-entrancy guard is exactly what we want. Diagnostics
/// (logging + error surfacing) are resolved optionally from DI, so each app plugs in
/// its own logger/error UX without the toolkit depending on either.
/// </summary>
public static class Navigator
{
    // 0 = idle, 1 = a navigation is in flight. Interlocked makes the check atomic.
    private static int _navigating;

    private static INavigationDiagnostics? Diagnostics =>
        IPlatformApplication.Current?.Services.GetService<INavigationDiagnostics>();

    public static Task PushAsync(Page page) =>
        RunGuarded(nameof(PushAsync), nav => nav.PushAsync(page));

    public static Task PopAsync() =>
        RunGuarded(nameof(PopAsync), nav => nav.PopAsync());

    public static Task PopToRootAsync() =>
        RunGuarded(nameof(PopToRootAsync), nav => nav.PopToRootAsync());

    public static Task GoToAsync(string route) =>
        RunGuarded($"GoToAsync({route})", _ => Shell.Current!.GoToAsync(route));

    private static async Task RunGuarded(string op, Func<INavigation, Task> action)
    {
        // Drop overlapping navigations (e.g. rapid double-taps) rather than racing them.
        if (Interlocked.CompareExchange(ref _navigating, 1, 0) != 0)
        {
            Diagnostics?.Log($"Navigation '{op}' skipped — another navigation is in progress.");
            return;
        }

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var nav = Shell.Current?.Navigation;
                if (Shell.Current is null || nav is null)
                {
                    Diagnostics?.Log($"Navigation '{op}' skipped — Shell not ready.");
                    return;
                }

                Diagnostics?.Log($"Navigation '{op}'");
                await action(nav);
            });
        }
        catch (Exception ex)
        {
            Diagnostics?.HandleError(ex, $"Navigation '{op}'");
        }
        finally
        {
            Interlocked.Exchange(ref _navigating, 0);
        }
    }
}
