using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MyToolkit.Services.Net.Http;

/// <summary>
/// Inner pipeline handler (sits below <see cref="AuthHandler"/>, directly above the
/// raw <see cref="HttpClient"/>). Its ONLY job is the 401 → refresh → single-retry
/// flow: on exactly one <c>401 Unauthorized</c> it exchanges the refresh token for a
/// new pair and replays the request once with the new bearer token. It never loops
/// (the replay is sent straight to the inner handler and its result is returned as-is)
/// and never refreshes for auth endpoints.
/// </summary>
public sealed class RefreshTokenHandler : DelegatingHandler
{
    private readonly ITokenStore _tokenStore;
    private readonly ApiClientOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISessionExpiredHandler _sessionExpired;

    // Serializes concurrent refreshes so a token-expiry storm triggers one refresh,
    // not one per in-flight request.
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);

    public RefreshTokenHandler(
        ITokenStore tokenStore, ApiClientOptions options,
        IHttpClientFactory httpClientFactory, ISessionExpiredHandler sessionExpired)
    {
        _tokenStore = tokenStore;
        _options = options;
        _httpClientFactory = httpClientFactory;
        _sessionExpired = sessionExpired;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Auth endpoints: a 401 means bad credentials, not an expired session — never
        // refresh/retry them (would recurse and mask the real error).
        if (_options.ShouldSkipAuth(request.RequestUri))
            return await base.SendAsync(request, cancellationToken);

        // Buffer the body up front: an HttpRequestMessage can only be sent once, so a
        // retry needs a re-readable clone.
        var bufferedBody = await BufferContentAsync(request, cancellationToken);
        var tokenBeforeSend = request.Headers.Authorization?.Parameter;

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // A 401 on a request that carried NO token is an unauthenticated call (e.g. a
        // tab loading its feed before login), not an expired session. Refreshing/logging
        // out here would cause a redirect storm that races a real login — so leave it.
        if (string.IsNullOrEmpty(tokenBeforeSend))
            return response;

        var newToken = await TryRefreshAsync(tokenBeforeSend, cancellationToken);
        if (string.IsNullOrWhiteSpace(newToken))
        {
            // The session is genuinely expired (had a token, refresh failed). Let the app
            // decide what to do (KurdishConnect logs out + redirects; default is no-op).
            await _sessionExpired.OnSessionExpiredAsync();
            return response;
        }

        // Replay exactly once with the refreshed token. The result is returned
        // verbatim — there is no second refresh, so the flow cannot loop.
        response.Dispose();
        var retry = CloneRequest(request, bufferedBody);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
        return await base.SendAsync(retry, cancellationToken);
    }

    /// <summary>
    /// Refreshes the JWT pair, returning the new access token, or null when refresh is
    /// impossible (no refresh token) or fails. On an auth failure during refresh the
    /// stored pair is cleared. Single-flight via <see cref="RefreshGate"/>.
    /// </summary>
    private async Task<string?> TryRefreshAsync(string? tokenBeforeSend, CancellationToken ct)
    {
        await RefreshGate.WaitAsync(ct);
        try
        {
            // Another request may have refreshed while we waited on the gate — if the
            // stored token already changed, reuse it instead of refreshing again.
            var current = await _tokenStore.GetAccessTokenAsync();
            if (!string.IsNullOrWhiteSpace(current) && current != tokenBeforeSend)
                return current;

            var refreshToken = await _tokenStore.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refreshToken))
                return null;

            // Use a clean client (no auth pipeline) so refresh cannot recurse into this
            // handler. The request shape is app-configurable (body vs query-string token).
            var client = _httpClientFactory.CreateClient(_options.RefreshHttpClientName);
            using var request = _options.RefreshRequestFactory(refreshToken, _options);

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    _tokenStore.Clear();
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var refreshed = JsonSerializer.Deserialize<RefreshTokenResponse>(json, _options.JsonOptions);
            if (string.IsNullOrWhiteSpace(refreshed?.Access))
                return null;

            await _tokenStore.SetTokensAsync(refreshed.Access, refreshed.Refresh);
            return refreshed.Access;
        }
        finally
        {
            RefreshGate.Release();
        }
    }

    private static async Task<byte[]?> BufferContentAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Content is null)
            return null;

        var body = await request.Content.ReadAsByteArrayAsync(ct);

        // Replace the now-consumed content with a re-readable copy, preserving headers
        // such as Content-Type (including any multipart boundary).
        var buffered = new ByteArrayContent(body);
        foreach (var header in request.Content.Headers)
            buffered.Headers.TryAddWithoutValidation(header.Key, header.Value);
        request.Content = buffered;

        return body;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request, byte[]? body)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (body is not null)
        {
            clone.Content = new ByteArrayContent(body);
            if (request.Content is not null)
                foreach (var header in request.Content.Headers)
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    private sealed class RefreshTokenResponse
    {
        public string Access { get; set; } = string.Empty;
        public string Refresh { get; set; } = string.Empty;
    }
}
