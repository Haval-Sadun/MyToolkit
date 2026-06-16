using MyToolkit.Services.Errors;
using MyToolkit.Services.Net;

namespace MyToolkit.Services.Auth;

/// <summary>
/// Single authentication base for every app. Owns the logout contract,
/// idempotent startup initialization, the <see cref="UserChanged"/> event,
/// and — when <see cref="Api"/> and <see cref="Tokens"/> are injected — the full
/// JWT REST lifecycle: login, register, refresh, auto-login.
///
/// <para>Apps override <see cref="LoginEndpoint"/>, <see cref="RegisterEndpoint"/>,
/// and <see cref="RedirectToLoginAsync"/> to adapt to their backend shape.
/// <see cref="LoginCoreAsync"/> and <see cref="RegisterCoreAsync"/> return
/// <see cref="Result{TUser}"/> — never throw — so subclasses can expose any
/// public signature they need.</para>
/// </summary>
public abstract class AuthServiceBase<TUser> where TUser : class
{
    protected ApiService? Api { get; }
    protected ITokenStore? Tokens { get; }

    private TUser? _currentUser;

    /// <summary>Fires whenever <see cref="CurrentUser"/> changes (login, logout, profile refresh).</summary>
    public event EventHandler<TUser?>? UserChanged;

    public TUser? CurrentUser
    {
        get => _currentUser;
        protected set
        {
            if (ReferenceEquals(_currentUser, value)) return;
            _currentUser = value;
            UserChanged?.Invoke(this, value);
        }
    }

    public bool IsAuthenticated => _currentUser is not null;

    protected AuthServiceBase(ApiService? api = null, ITokenStore? tokens = null)
    {
        Api = api;
        Tokens = tokens;
    }

    // ── Startup initialization ────────────────────────────────────────────────

    private Task? _initTask;
    private readonly object _initLock = new();

    /// <summary>
    /// Idempotent startup initializer. The first caller starts the work; every later
    /// caller (including background handlers) awaits the same task, so no request
    /// goes out before persisted session state is restored.
    ///
    /// The TCS is pinned to <c>_initTask</c> before <see cref="InitializeCoreAsync"/>
    /// starts so that re-entrant calls (e.g. a <see cref="UserChanged"/> subscriber
    /// that calls back here synchronously in DEBUG mode) find a non-null task and
    /// simply await the pending result instead of entering the lock again.
    /// </summary>
    public Task InitializeAsync()
    {
        if (_initTask is null)
            lock (_initLock)
                if (_initTask is null)
                {
                    var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    _initTask = tcs.Task;
                    _ = CompleteInitAsync(tcs);
                }
        return _initTask;
    }

    private async Task CompleteInitAsync(TaskCompletionSource tcs)
    {
        try { await InitializeCoreAsync(); tcs.SetResult(); }
        catch (Exception ex) { tcs.SetException(ex); }
    }

    /// <summary>
    /// Override to restore persisted session state (tokens, user) on startup.
    /// Called exactly once by <see cref="InitializeAsync"/>; re-entrancy is handled there.
    /// REST apps that drive startup via <see cref="TryAutoLoginAsync"/> may leave this as a no-op.
    /// </summary>
    protected virtual Task InitializeCoreAsync() => Task.CompletedTask;

    // ── Logout contract ────────────────────────────────────────────────────────

    /// <summary>
    /// Navigate to the login (or startup) screen after logout or session expiry.
    /// Must marshal to the UI thread and must not throw.
    /// </summary>
    protected abstract Task RedirectToLoginAsync();

    /// <summary>
    /// True while a session worth clearing exists. Default: <see cref="CurrentUser"/> is
    /// non-null. Override when your "logged out" state is a non-null sentinel (e.g. an
    /// empty <c>User</c> object) or depends on a cached token string.
    /// </summary>
    protected virtual bool HasSession => _currentUser is not null;

    /// <summary>
    /// Clears tokens and resets <see cref="CurrentUser"/> to null. Override when the app
    /// uses a non-null anonymous sentinel or needs extra cleanup (SecureStorage, Preferences).
    /// Does NOT navigate — <see cref="LogoutAsync"/> handles that.
    /// </summary>
    protected virtual Task ClearSessionAsync()
    {
        Tokens?.Clear();
        CurrentUser = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Idempotent logout: clears the session then redirects to login.
    /// No-op when already logged out so a stale 401 cannot wipe a fresh session.
    /// </summary>
    public async Task LogoutAsync()
    {
        if (!HasSession) return;
        await ClearSessionAsync();
        await RedirectToLoginAsync();
    }

    // ── REST lifecycle (requires Api + Tokens injected via constructor) ────────

    protected virtual string LoginEndpoint    => "auth/login/";
    protected virtual string RegisterEndpoint => "auth/register/";
    protected virtual string RefreshEndpoint  => "auth/refresh/";
    protected virtual string MeEndpoint       => "auth/users/me/";

    /// <summary>
    /// Gate applied after every successful authentication. Default accepts everyone;
    /// a staff-only app overrides this to reject non-staff users.
    /// </summary>
    protected virtual bool AcceptUser(TUser user) => true;

    /// <summary>
    /// POSTs <paramref name="requestBody"/> to the login endpoint.
    /// Returns a <see cref="Result{TUser}"/> — success carries the logged-in user,
    /// failure carries the API or transport error. Never throws.
    /// </summary>
    protected Task<Result<TUser>> LoginCoreAsync(object requestBody)
        => AuthenticateAsync(LoginEndpoint, requestBody);

    /// <summary>
    /// POSTs <paramref name="requestBody"/> to the register endpoint.
    /// Returns a <see cref="Result{TUser}"/> — success carries the registered user,
    /// failure carries the API or transport error. Never throws.
    /// </summary>
    protected Task<Result<TUser>> RegisterCoreAsync(object requestBody)
        => AuthenticateAsync(RegisterEndpoint, requestBody);

    private async Task<Result<TUser>> AuthenticateAsync(string endpoint, object requestBody)
    {
        var res = await Api!.TryPostAsync<AuthEnvelope<TUser>>(endpoint, requestBody);
        if (!res.IsSuccess)
            return Result<TUser>.Fail(res.Error!);

        var envelope = res.Value;
        if (envelope is null || string.IsNullOrEmpty(envelope.Access) || envelope.User is null)
            return Result<TUser>.Fail(new SimpleApiError { Message = "Invalid server response.", ErrorCode = "INVALID_RESPONSE" });

        if (!AcceptUser(envelope.User))
            return Result<TUser>.Fail(new SimpleApiError { Message = "Access denied.", ErrorCode = "ACCESS_DENIED", StatusCode = 403 });

        await Tokens!.SetTokensAsync(envelope.Access, envelope.Refresh);
        CurrentUser = envelope.User;
        return Result<TUser>.Ok(envelope.User);
    }

    /// <summary>
    /// Exchanges the stored refresh token for a new pair. Returns false on any failure
    /// (no token, server rejected it). On hard auth failure the session is cleared.
    /// </summary>
    protected virtual async Task<bool> TryRefreshSessionAsync()
    {
        var refreshToken = await Tokens!.GetRefreshTokenAsync();
        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;

        var res = await Api!.TryPostAsync<TokenPair>(RefreshEndpoint, new { refresh = refreshToken });
        if (!res.IsSuccess || string.IsNullOrWhiteSpace(res.Value?.Access))
        {
            if (res.Error is ApiException { StatusCode: System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden })
                await ClearSessionAsync();
            return false;
        }

        await Tokens.SetTokensAsync(res.Value.Access, res.Value.Refresh);
        return true;
    }

    /// <summary>
    /// Restores a session on cold start: ensures a valid token, loads the user from
    /// the <c>me</c> endpoint, and applies <see cref="AcceptUser"/>. Returns true on success.
    /// </summary>
    public async Task<bool> TryAutoLoginAsync()
    {
        var token = await Tokens!.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token) && !await TryRefreshSessionAsync())
            return false;

        token = await Tokens.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var userRes = await Api!.TryGetAsync<TUser>(MeEndpoint);
        if (userRes.IsSuccess && userRes.Value is not null && AcceptUser(userRes.Value))
        {
            CurrentUser = userRes.Value;
            return true;
        }

        // Token exists but me/ rejected it — try one refresh then retry
        if (!await TryRefreshSessionAsync())
        {
            await ClearSessionAsync();
            return false;
        }

        var retry = await Api.TryGetAsync<TUser>(MeEndpoint);
        if (retry.IsSuccess && retry.Value is not null && AcceptUser(retry.Value))
        {
            CurrentUser = retry.Value;
            return true;
        }

        await ClearSessionAsync();
        return false;
    }

    /// <summary>Re-fetches the current user (e.g. after a profile change), refreshing once if needed.</summary>
    public async Task RefreshUserAsync()
    {
        var res = await Api!.TryGetAsync<TUser>(MeEndpoint);
        if (res.IsSuccess && res.Value is not null)
        {
            CurrentUser = res.Value;
            return;
        }

        if (res.Error is ApiException { StatusCode: System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden })
        {
            if (await TryRefreshSessionAsync())
            {
                var retry = await Api.TryGetAsync<TUser>(MeEndpoint);
                if (retry.IsSuccess && retry.Value is not null)
                    CurrentUser = retry.Value;
                else
                    await ClearSessionAsync();
            }
            else
            {
                await ClearSessionAsync();
            }
        }
    }
}
