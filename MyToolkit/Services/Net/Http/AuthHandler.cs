using System.Net.Http.Headers;

namespace MyToolkit.Services.Net.Http;

/// <summary>
/// Outermost pipeline handler. Its ONLY job is to inject the current bearer token
/// onto the outgoing request — per request, never via
/// <c>HttpClient.DefaultRequestHeaders</c> (which is shared mutable state). Endpoints
/// listed in <see cref="ApiClientOptions.AuthSkipPrefixes"/> (login/register/refresh/
/// logout) are sent without a token.
/// </summary>
public sealed class AuthHandler : DelegatingHandler
{
    private readonly ITokenStore _tokenStore;
    private readonly ApiClientOptions _options;

    public AuthHandler(ITokenStore tokenStore, ApiClientOptions options)
    {
        _tokenStore = tokenStore;
        _options = options;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!_options.ShouldSkipAuth(request.RequestUri))
        {
            var token = await _tokenStore.GetAccessTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
