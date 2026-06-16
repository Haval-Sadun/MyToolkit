# MyToolkit · Services/Net

A production-grade, JWT-authenticated HTTP stack shared by every app. One pipeline, two
call surfaces, pluggable on exactly the axes backends differ.

## Files

| File | Types | Role |
|------|-------|------|
| `ApiService.cs` | `ApiService` | **throwing** surface — send / deserialize / throw `ApiException`. Orchestration only; knows nothing about tokens. |
| `IResultApiService.cs` | `IResultApiService`, `ResultApiService`, `Result<T>`, `SimpleApiError` | **non-throwing** surface — same calls, returns `Result<T>`. |
| `ApiClientOptions.cs` | `ApiClientOptions` | per-app config: client names, token keys, auth-skip prefixes, JSON, refresh-request shape. |
| `ApiExceptionFactory.cs` | `ApiExceptionFactory` | the only place that parses a backend error body into `ApiException`. |
| `ITokenStore.cs` | `ITokenStore`, `SecureTokenStore` | the only place that reads/writes the JWT pair. |
| `ISessionExpiredHandler.cs` | `ISessionExpiredHandler`, `NoOpSessionExpiredHandler` | app hook for "refresh failed → session is gone". |
| `Http/AuthHandler.cs` | `AuthHandler` | outermost handler — injects the bearer token per request. |
| `Http/RefreshTokenHandler.cs` | `RefreshTokenHandler` | inner handler — 401 → refresh → single retry. |
| `ServiceCollectionExtensions.cs` | `AddApiService` | one-call wiring of the whole stack. |

## The pipeline

```
ApiService ─▶ HttpClient ─▶ AuthHandler ─▶ RefreshTokenHandler ─▶ network
                            (adds Bearer)    (401 → refresh → retry once)
```

`AuthHandler` is added first, so it is **outermost**. The refresh call goes out on a
separate clean client (`{name}-refresh`, no handlers) so it can never recurse.

## Wiring it up in an end app

Register any app-specific **seams first** (they win over the toolkit defaults because
`AddApiService` uses `TryAdd`), then call `AddApiService`:

```csharp
// Optional seams — override only what differs from the defaults:
builder.Services.AddSingleton<ITokenStore, MyTokenStore>();             // default: SecureTokenStore
builder.Services.AddSingleton<ISessionExpiredHandler, MyLogoutOnExpiry>(); // default: no-op

builder.Services.AddApiService(
    new ApiClientOptions
    {
        HttpClientName        = "myapp",
        RefreshHttpClientName = "myapp-refresh",
        AccessTokenKey        = "access_token",
        RefreshTokenKey       = "refresh_token",
        AuthSkipPrefixes      = new[] { "auth/login", "auth/register", "auth/refresh" },
        // Default refresh posts { "refresh": <token> } to RefreshEndpoint (DRF SimpleJWT).
        // Override only if your backend differs, e.g. token in the query string:
        // RefreshRequestFactory = (t, _) => new HttpRequestMessage(
        //     HttpMethod.Post, $"auth/refresh?refresh={Uri.EscapeDataString(t)}"),
    },
    client =>
    {
        client.BaseAddress = new Uri(Constants.BaseUrl);
        client.Timeout     = TimeSpan.FromSeconds(30);
    });
```

This registers `ApiService`, `IResultApiService` (→ `ResultApiService`), `ApiExceptionFactory`,
both handlers, and both named clients.

## Choosing a surface

Each app picks **one** and injects it everywhere. Both run the identical pipeline.

**Throwing (`ApiService`)** — pairs naturally with `ErrorHandler`/try-catch (SyriaBet):

```csharp
public class FeedService(ApiService api)
{
    public Task<List<Post>> GetFeedAsync(CancellationToken ct = default)
        => api.GetListAsync<Post>("feed/", ct);   // throws ApiException on failure
}
```

**Result (`IResultApiService`)** — pairs with explicit branching (KurdishConnect):

```csharp
public class FeedService(IResultApiService api)
{
    public async Task<List<Post>> GetFeedAsync(CancellationToken ct = default)
    {
        var res = await api.GetAsync<List<Post>>("feed/", ct);
        return res.IsSuccess ? res.Value! : [];   // res.Error is IApiError
    }
}
```

## Notes

- **Tokens** are read per request by the pipeline; never push a token into the client or
  the service. The login flow only needs to call `ITokenStore.SetTokensAsync(...)`.
- **Multipart**: use `PostMultipartAsync` / `PutMultipartAsync` / `PatchMultipartAsync`
  for file uploads — the caller owns and disposes the `MultipartFormDataContent`.
- **Lists**: `GetListAsync<T>` transparently unwraps DRF pagination (`{ results: [...] }`)
  and also accepts a bare JSON array.
- **`CancellationToken`** flows through every method.
