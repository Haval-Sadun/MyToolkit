namespace MyToolkit.Services.Auth;

/// <summary>
/// Non-generic auth state surface consumed by toolkit services that only need to know
/// whether a user is signed in — not the full user model.
/// Implemented by <see cref="AuthServiceBase{TUser}"/>.
/// </summary>
public interface IAuthStateProvider
{
    bool IsAuthenticated { get; }

    /// <summary>Fires when the user transitions from logged-out to logged-in.</summary>
    event EventHandler? LoggedIn;

    /// <summary>Fires when the user transitions from logged-in to logged-out.</summary>
    event EventHandler? LoggedOut;
}
