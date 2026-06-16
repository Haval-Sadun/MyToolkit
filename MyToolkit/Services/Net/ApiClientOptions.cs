using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyToolkit.Services.Net;

/// <summary>
/// Per-app configuration for the toolkit <see cref="ApiService"/>: which named
/// <see cref="HttpClient"/> to use, the SecureStorage keys the JWT pair is stored
/// under, the refresh endpoint, which endpoints must NOT carry a bearer token (and
/// must not trigger a refresh), and the JSON serializer options. Defaults target a
/// Django REST Framework backend (snake_case, decimals-as-strings); override per app.
/// Registered as a DI singleton.
/// </summary>
public class ApiClientOptions
{
    /// <summary>Name of the registered <see cref="HttpClient"/> (with BaseAddress set).</summary>
    public string HttpClientName { get; init; } = "Api";

    /// <summary>
    /// Name of a second registered <see cref="HttpClient"/> that has the SAME BaseAddress
    /// but NO auth pipeline. Used by <c>RefreshTokenHandler</c> to call the refresh
    /// endpoint without recursing back through itself. Defaults to "{HttpClientName}-refresh".
    /// </summary>
    public string RefreshHttpClientName { get; init; } = "Api-refresh";

    /// <summary>SecureStorage key for the access (bearer) token.</summary>
    public string AccessTokenKey { get; init; } = "access_token";

    /// <summary>SecureStorage key for the refresh token.</summary>
    public string RefreshTokenKey { get; init; } = "refresh_token";

    /// <summary>Relative endpoint used to exchange the refresh token for a new pair.</summary>
    public string RefreshEndpoint { get; init; } = "auth/refresh/";

    /// <summary>
    /// Endpoint prefixes that must be called WITHOUT a bearer token and must not
    /// trigger a 401-refresh retry (login/register/refresh/logout).
    /// </summary>
    public IReadOnlyList<string> AuthSkipPrefixes { get; init; } = new[]
    {
        "auth/login/", "auth/register/", "auth/refresh/", "auth/logout/"
    };

    /// <summary>JSON options applied to every (de)serialization. DRF-shaped by default.</summary>
    public JsonSerializerOptions JsonOptions { get; init; } = DefaultDrfJson();

    /// <summary>
    /// True when the request must be sent WITHOUT a bearer token and must not trigger a
    /// 401-refresh retry (login/register/refresh/logout). Matches a request URI against
    /// <see cref="AuthSkipPrefixes"/> by path segment, so it works on the absolute URI
    /// the handlers see (e.g. "/api/auth/login/").
    /// </summary>
    public bool ShouldSkipAuth(Uri? requestUri)
    {
        var path = requestUri?.AbsolutePath ?? string.Empty;
        return AuthSkipPrefixes.Any(p => path.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>DRF default: snake_case, case-insensitive, numbers readable from strings.</summary>
    public static JsonSerializerOptions DefaultDrfJson() => new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>
    /// Builds the HTTP request that exchanges a refresh token for a new pair. Differs by
    /// backend, so it's pluggable: the default posts <c>{ "refresh": &lt;token&gt; }</c> to
    /// <see cref="RefreshEndpoint"/> (DRF SimpleJWT); KurdishConnect overrides it to post to
    /// <c>auth/refresh?refresh=&lt;token&gt;</c> with an empty body.
    /// </summary>
    public Func<string, ApiClientOptions, HttpRequestMessage> RefreshRequestFactory { get; init; }
        = DefaultRefreshRequest;

    private static HttpRequestMessage DefaultRefreshRequest(string refreshToken, ApiClientOptions options)
    {
        var payload = JsonSerializer.Serialize(new { refresh = refreshToken }, options.JsonOptions);
        return new HttpRequestMessage(HttpMethod.Post, options.RefreshEndpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }
}
