using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyToolkit.Services.Net.Http;

namespace MyToolkit.Services.Net;

/// <summary>
/// One-call wiring for the toolkit HTTP stack, so every consuming app registers the
/// pipeline identically and in the correct handler order.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ApiService"/> (both throwing and non-throwing Try* surfaces) and its full pipeline:
    /// <list type="bullet">
    /// <item>the primary named client with the chain <c>AuthHandler → RefreshTokenHandler → HttpClient</c>;</item>
    /// <item>a clean "{name}-refresh" client (same BaseAddress, no handlers) used only to refresh tokens;</item>
    /// <item><see cref="ITokenStore"/>, <see cref="ApiExceptionFactory"/>, and both handlers.</item>
    /// </list>
    /// <paramref name="configureClient"/> sets BaseAddress / default headers / timeout and is
    /// applied to BOTH clients so the refresh call targets the same backend.
    /// </summary>
    public static IServiceCollection AddApiService(
        this IServiceCollection services,
        ApiClientOptions options,
        Action<HttpClient> configureClient)
    {
        services.AddSingleton(options);
        services.AddSingleton<ApiExceptionFactory>();

        // App-overridable seams: register your own ITokenStore / ISessionExpiredHandler
        // BEFORE calling AddApiService to replace these defaults. SecureTokenStore uses
        // SecureStorage; the default session handler is a no-op.
        services.TryAddSingleton<ITokenStore, SecureTokenStore>();
        services.TryAddSingleton<ISessionExpiredHandler, NoOpSessionExpiredHandler>();

        // Handlers are transient — the recommended lifetime for DelegatingHandlers
        // resolved by IHttpClientFactory (one instance per handler chain construction).
        services.AddTransient<AuthHandler>();
        services.AddTransient<RefreshTokenHandler>();

        // Primary client: AuthHandler is added first, so it is the OUTERMOST handler,
        // giving the required order AuthHandler → RefreshTokenHandler → HttpClient.
        services.AddHttpClient(options.HttpClientName, configureClient)
            .AddHttpMessageHandler<AuthHandler>()
            .AddHttpMessageHandler<RefreshTokenHandler>();

        // Clean client for the refresh call — no handlers, so a refresh cannot recurse
        // back into RefreshTokenHandler.
        services.AddHttpClient(options.RefreshHttpClientName, configureClient);

        services.AddSingleton<ApiService>();
        return services;
    }
}
