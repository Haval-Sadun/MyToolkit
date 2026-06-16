namespace MyToolkit.Services.Net;

/// <summary>
/// The single place that reads and writes the JWT pair. Introduced so that no other
/// layer (least of all <see cref="ApiService"/>) touches token storage directly —
/// the auth/refresh handlers and the app's AuthService all go through this seam.
/// </summary>
/// <remarks>
/// The toolkit registers <see cref="SecureTokenStore"/> by default. An app overrides it
/// by registering its own <c>ITokenStore</c> <b>before</b> calling <c>AddApiService</c>
/// (e.g. KurdishConnect wraps its Preferences/SecureStorage store to share one token
/// location with code that predates the toolkit).
/// </remarks>
public interface ITokenStore
{
    Task<string?> GetAccessTokenAsync();
    Task<string?> GetRefreshTokenAsync();
    Task SetTokensAsync(string accessToken, string? refreshToken);
    void Clear();
}

/// <summary>
/// Default <see cref="ITokenStore"/>, backed by <see cref="SecureStorage"/> and keyed by
/// the access/refresh keys carried in <see cref="ApiClientOptions"/>. This is the only
/// type that names those keys against the platform secure store. Registered automatically
/// by <c>AddApiService</c> unless the app supplies its own store first.
/// </summary>
public sealed class SecureTokenStore : ITokenStore
{
    private readonly ApiClientOptions _options;

    public SecureTokenStore(ApiClientOptions options) => _options = options;

    public Task<string?> GetAccessTokenAsync() => SecureStorage.GetAsync(_options.AccessTokenKey);

    public Task<string?> GetRefreshTokenAsync() => SecureStorage.GetAsync(_options.RefreshTokenKey);

    public async Task SetTokensAsync(string accessToken, string? refreshToken)
    {
        await SecureStorage.SetAsync(_options.AccessTokenKey, accessToken);
        if (!string.IsNullOrWhiteSpace(refreshToken))
            await SecureStorage.SetAsync(_options.RefreshTokenKey, refreshToken);
    }

    public void Clear()
    {
        SecureStorage.Remove(_options.AccessTokenKey);
        SecureStorage.Remove(_options.RefreshTokenKey);
    }
}
