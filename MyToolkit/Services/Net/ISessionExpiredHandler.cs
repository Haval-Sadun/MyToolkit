namespace MyToolkit.Services.Net;

/// <summary>
/// App-supplied callback invoked by <c>RefreshTokenHandler</c> when a request that
/// carried a token gets a 401 and the session could NOT be refreshed (the refresh
/// token is missing or rejected). Lets each app decide its session-expiry UX — e.g.
/// KurdishConnect logs out and redirects to login; SyriaBet uses the default no-op
/// and lets its own auto-login flow handle re-auth.
/// </summary>
public interface ISessionExpiredHandler
{
    Task OnSessionExpiredAsync();
}

/// <summary>Default no-op handler, registered when an app supplies none.</summary>
public sealed class NoOpSessionExpiredHandler : ISessionExpiredHandler
{
    public Task OnSessionExpiredAsync() => Task.CompletedTask;
}
