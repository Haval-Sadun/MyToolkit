using Microsoft.Extensions.DependencyInjection;

namespace MyToolkit.Services;

/// <summary>
/// Service-locator shim for the few places that can't take constructor injection
/// — the base view-model's <c>RunSafeAsync</c> and the global exception hooks in
/// <c>App</c>. Resolves against the running MAUI service provider; the platform-free build
/// has none until <c>Use</c> is called, so every lookup answers "not available".
/// </summary>
public static class ServiceHelper
{
#if MAUI_PLATFORM
    private static IServiceProvider? Current => IPlatformApplication.Current?.Services;
#else
    private static IServiceProvider? _provider;

    /// <summary>Platform-free build: the container lookups run against.</summary>
    public static void Use(IServiceProvider? provider) => _provider = provider;

    private static IServiceProvider? Current => _provider;
#endif

    public static IServiceProvider Services =>
        Current
        ?? throw new InvalidOperationException("MAUI service provider is not available yet.");

    public static T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    /// <summary>Non-throwing variant for use inside exception hooks.</summary>
    public static T? GetSafe<T>() where T : class
    {
        try
        {
            return Current?.GetService(typeof(T)) as T;
        }
        catch (ObjectDisposedException)
        {
            // The app is shutting down and the container is already gone.
            return null;
        }
    }
}
