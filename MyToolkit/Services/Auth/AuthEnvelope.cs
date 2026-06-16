namespace MyToolkit.Services.Auth;

/// <summary>
/// The shape returned by a typical login/register endpoint: a JWT pair plus the authenticated
/// user. Generic over the app's user model so the shared <see cref="AuthServiceBase{TUser}"/>
/// can parse it without knowing the concrete type. Field names map to the backend's snake_case
/// (<c>access</c>, <c>refresh</c>, <c>user</c>) via the app's JSON options.
/// </summary>
public sealed class AuthEnvelope<TUser>
{
    public string Access { get; set; } = string.Empty;
    public string? Refresh { get; set; }
    public TUser? User { get; set; }
}

/// <summary>A bare JWT pair, returned by the refresh endpoint.</summary>
public sealed class TokenPair
{
    public string Access { get; set; } = string.Empty;
    public string? Refresh { get; set; }
}
