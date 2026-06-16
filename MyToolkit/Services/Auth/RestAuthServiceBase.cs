using System.Net;
using MyToolkit.Services.Errors;
using MyToolkit.Services.Net;

namespace MyToolkit.Services.Auth;

/// <summary>
/// Authentication for apps that talk to a JWT REST backend through the throwing
/// <see cref="ApiService"/> and represent "logged out" as a null <see cref="CurrentUser"/>
/// (re-fetched from the <c>me</c> endpoint on auto-login). Provides the full login / register /
/// refresh / auto-login lifecycle so consuming apps become thin subclasses that only supply the
/// credential request shape and the login redirect.
///
/// <para>Endpoints and the post-login <see cref="AcceptUser"/> gate are overridable — e.g. a
/// staff-only app rejects ordinary users by overriding <see cref="AcceptUser"/>.</para>
/// </summary>
public abstract class RestAuthServiceBase<TUser> : AuthServiceBase where TUser : class
{
    protected ApiService Api { get; }
    protected ITokenStore Tokens { get; }

    private TUser? _currentUser;

    protected RestAuthServiceBase(ApiService api, ITokenStore tokens)
    {
        Api = api;
        Tokens = tokens;
    }

    public TUser? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;

    // ── AuthServiceBase contract ────────────────────────────────────────────
    protected override bool HasSession => _currentUser != null;

    protected override Task ClearSessionAsync()
    {
        Tokens.Clear();
        _currentUser = null;
        return Task.CompletedTask;
    }

    // ── Overridable seams ───────────────────────────────────────────────────
    protected virtual string LoginEndpoint => "auth/login/";
    protected virtual string RegisterEndpoint => "auth/register/";
    protected virtual string RefreshEndpoint => "auth/refresh/";
    protected virtual string MeEndpoint => "auth/users/me/";

    /// <summary>
    /// Decides whether a freshly authenticated user may use this app. Default accepts everyone;
    /// a staff app overrides this to reject ordinary users (e.g. <c>user.Role == "user"</c>).
    /// </summary>
    protected virtual bool AcceptUser(TUser user) => true;

    // ── Lifecycle ───────────────────────────────────────────────────────────

    /// <summary>
    /// POSTs <paramref name="requestBody"/> to the login endpoint, stores the returned tokens and
    /// user, and applies the <see cref="AcceptUser"/> gate. Returns false on bad credentials
    /// (400/401) or a rejected/empty user; rethrows anything else.
    /// </summary>
    protected Task<bool> LoginCoreAsync(object requestBody) => AuthenticateAsync(LoginEndpoint, requestBody);

    /// <summary>As <see cref="LoginCoreAsync"/> but against the register endpoint.</summary>
    protected Task<bool> RegisterCoreAsync(object requestBody) => AuthenticateAsync(RegisterEndpoint, requestBody);

    private async Task<bool> AuthenticateAsync(string endpoint, object requestBody)
    {
        try
        {
            var result = await Api.PostAsync<AuthEnvelope<TUser>>(endpoint, requestBody);
            if (result is null || string.IsNullOrEmpty(result.Access) || result.User is null)
                return false;
            if (!AcceptUser(result.User))
                return false;

            await Tokens.SetTokensAsync(result.Access, result.Refresh);
            _currentUser = result.User;
            return true;
        }
        // Bad credentials / validation errors (duplicate phone, weak password) are expected:
        // return false and let the caller show a friendly message. Other failures propagate.
        catch (ApiException ex) when ((int)ex.StatusCode is 400 or 401)
        {
            return false;
        }
    }

    /// <summary>
    /// Exchanges the stored refresh token for a new pair. On a hard auth failure (401/403) the
    /// session is cleared (no redirect — the caller decides navigation). Returns success.
    /// </summary>
    protected async Task<bool> TryRefreshSessionAsync()
    {
        try
        {
            var refreshToken = await Tokens.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refreshToken))
                return false;

            var refreshed = await Api.PostAsync<TokenPair>(RefreshEndpoint, new { refresh = refreshToken });
            if (refreshed is null || string.IsNullOrWhiteSpace(refreshed.Access))
                return false;

            await Tokens.SetTokensAsync(refreshed.Access, refreshed.Refresh);
            return true;
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            await ClearSessionAsync();
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Restores a session on startup: ensures a valid access token (refreshing if needed), then
    /// loads the current user from the <c>me</c> endpoint and applies <see cref="AcceptUser"/>.
    /// Returns true when an accepted user is loaded.
    /// </summary>
    public async Task<bool> TryAutoLoginAsync()
    {
        try
        {
            var token = await Tokens.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token) && !await TryRefreshSessionAsync())
                return false;

            token = await Tokens.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
                return false;

            if (await LoadCurrentUserAsync())
                return true;
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            if (await TryRefreshSessionAsync() && await LoadCurrentUserAsync())
                return true;

            await ClearSessionAsync();
        }
        catch { }

        return false;
    }

    /// <summary>Re-fetches the current user (e.g. after a profile/balance change), refreshing once on 401/403.</summary>
    public async Task RefreshUserAsync()
    {
        try
        {
            await LoadCurrentUserAsync();
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            if (await TryRefreshSessionAsync())
                await LoadCurrentUserAsync();
            else
                await ClearSessionAsync();
        }
        catch { }
    }

    private async Task<bool> LoadCurrentUserAsync()
    {
        var user = await Api.GetAsync<TUser>(MeEndpoint);
        if (user is null || !AcceptUser(user))
            return false;
        _currentUser = user;
        return true;
    }
}
