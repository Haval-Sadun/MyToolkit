namespace MyToolkit.Services;

/// <summary>
/// Service-locator shim for the few places that can't take constructor injection
/// — the base view-model's <c>RunSafeAsync</c> and the global exception hooks in
/// <c>App</c>. Resolves against the running MAUI service provider.
/// </summary>
public static class ServiceHelper
{
    public static IServiceProvider Services =>
        IPlatformApplication.Current?.Services
        ?? throw new InvalidOperationException("MAUI service provider is not available yet.");

    public static T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    /// <summary>Non-throwing variant for use inside exception hooks.</summary>
    public static T? GetSafe<T>() where T : class =>
        IPlatformApplication.Current?.Services?.GetService(typeof(T)) as T;
}
