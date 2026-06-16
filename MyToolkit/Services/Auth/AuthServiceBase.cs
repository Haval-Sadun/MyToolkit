namespace MyToolkit.Services.Auth;

/// <summary>
/// Common authentication lifecycle shared by every app's <c>AuthService</c>. It owns the
/// one piece that must behave identically everywhere: <b>logging out clears the session and
/// redirects to the login/startup screen</b>. Each app supplies the <em>how</em> of that
/// redirect (Shell route, <c>Navigator</c>, etc.) by overriding <see cref="RedirectToLoginAsync"/>.
///
/// <para>This base is transport-agnostic: it knows nothing about the HTTP surface, the user
/// model, or token storage — subclasses (e.g. <see cref="RestAuthServiceBase{TUser}"/>) add
/// those. It only orchestrates the logout sequence so no app forgets the redirect.</para>
/// </summary>
public abstract class AuthServiceBase
{
    /// <summary>
    /// Navigate to the login (or startup) screen. Called after an explicit logout and on a
    /// genuine session-expiry. App-specific because the route/navigator differs per app.
    /// Must run on / marshal to the UI thread and must not throw.
    /// </summary>
    protected abstract Task RedirectToLoginAsync();

    /// <summary>True while there is a session worth clearing. Keeps <see cref="LogoutAsync"/> idempotent.</summary>
    protected abstract bool HasSession { get; }

    /// <summary>
    /// Clear tokens, the current user and any persisted user. Does <b>not</b> navigate — the
    /// redirect is handled by <see cref="LogoutAsync"/>. Used directly (without a redirect) on
    /// internal refresh/auto-login failures, where the caller decides navigation.
    /// </summary>
    protected abstract Task ClearSessionAsync();

    /// <summary>
    /// Idempotent logout: if a session exists it is cleared and the app is redirected to login.
    /// A no-op (no redirect) when already logged out, so a late 401 from a request that started
    /// before a fresh login cannot wipe the new session or bounce the user.
    /// </summary>
    public async Task LogoutAsync()
    {
        if (!HasSession) return;
        await ClearSessionAsync();
        await RedirectToLoginAsync();
    }
}
