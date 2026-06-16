using System.Net.Http;
using System.Text;
using System.Text.Json;
using MyToolkit.Services.Errors;

namespace MyToolkit.Services.Net;

/// <summary>
/// Thin orchestration layer over a named <see cref="HttpClient"/>. Supports two calling
/// styles: throwing (GetAsync/PostAsync/…) and non-throwing (TryGetAsync/TryPostAsync/…
/// which return <see cref="Result{T}"/> and never throw).
/// Both styles ride the same pipeline — auth, token refresh, retries.
/// </summary>
public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly ApiClientOptions _options;
    private readonly ApiExceptionFactory _exceptionFactory;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiService(
        IHttpClientFactory httpClientFactory, ApiClientOptions options, ApiExceptionFactory exceptionFactory)
    {
        _options = options;
        _exceptionFactory = exceptionFactory;
        _httpClient = httpClientFactory.CreateClient(options.HttpClientName);
        _jsonOptions = options.JsonOptions;
    }

    // ── Throwing style ────────────────────────────────────────────────────────

    public async Task<T?> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
        return await DeserializeAsync<T>(response, "GET", endpoint, cancellationToken);
    }

    /// <summary>
    /// GET a list endpoint, transparently unwrapping DRF pagination
    /// (responses shaped as { "count", "next", "previous", "results": [...] })
    /// while still accepting a bare JSON array.
    /// </summary>
    public async Task<List<T>> GetListAsync<T>(string endpoint, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await _exceptionFactory.CreateAsync(response, "GET", endpoint, cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("results", out var results))
        {
            return JsonSerializer.Deserialize<List<T>>(results.GetRawText(), _jsonOptions) ?? new();
        }
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<T>>(json, _jsonOptions) ?? new();
        }
        return new();
    }

    public async Task<T?> PostAsync<T>(string endpoint, object body, CancellationToken cancellationToken = default)
    {
        using var content = SerializeBody(body);
        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        return await DeserializeAsync<T>(response, "POST", endpoint, cancellationToken);
    }

    // Multipart variants for file/image uploads. The caller owns the
    // MultipartFormDataContent (it composes the parts), so we do not dispose it here.
    public async Task<T?> PostMultipartAsync<T>(
        string endpoint, MultipartFormDataContent content, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        return await DeserializeAsync<T>(response, "POST", endpoint, cancellationToken);
    }

    public async Task<T?> PutMultipartAsync<T>(
        string endpoint, MultipartFormDataContent content, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsync(endpoint, content, cancellationToken);
        return await DeserializeAsync<T>(response, "PUT", endpoint, cancellationToken);
    }

    public async Task<T?> PatchMultipartAsync<T>(
        string endpoint, MultipartFormDataContent content, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, endpoint) { Content = content };
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await DeserializeAsync<T>(response, "PATCH", endpoint, cancellationToken);
    }

    public async Task<T?> PatchAsync<T>(string endpoint, object body, CancellationToken cancellationToken = default)
    {
        using var content = SerializeBody(body);
        using var request = new HttpRequestMessage(HttpMethod.Patch, endpoint) { Content = content };
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await DeserializeAsync<T>(response, "PATCH", endpoint, cancellationToken);
    }

    public async Task<T?> PutAsync<T>(string endpoint, object body, CancellationToken cancellationToken = default)
    {
        using var content = SerializeBody(body);
        using var response = await _httpClient.PutAsync(endpoint, content, cancellationToken);
        return await DeserializeAsync<T>(response, "PUT", endpoint, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync(endpoint, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    // ── Non-throwing style (returns Result<T>, never throws) ─────────────────

    public Task<Result<T>> TryGetAsync<T>(string endpoint, CancellationToken ct = default)
        => Execute(() => GetAsync<T>(endpoint, ct));

    public Task<Result<T>> TryPostAsync<T>(string endpoint, object body, CancellationToken ct = default)
        => Execute(() => PostAsync<T>(endpoint, body, ct));

    public Task<Result<T>> TryPostMultipartAsync<T>(string endpoint, MultipartFormDataContent content, CancellationToken ct = default)
        => Execute(() => PostMultipartAsync<T>(endpoint, content, ct));

    public Task<Result<T>> TryPutAsync<T>(string endpoint, object body, CancellationToken ct = default)
        => Execute(() => PutAsync<T>(endpoint, body, ct));

    public Task<Result<T>> TryPutMultipartAsync<T>(string endpoint, MultipartFormDataContent content, CancellationToken ct = default)
        => Execute(() => PutMultipartAsync<T>(endpoint, content, ct));

    public Task<Result<T>> TryPatchAsync<T>(string endpoint, object body, CancellationToken ct = default)
        => Execute(() => PatchAsync<T>(endpoint, body, ct));

    public Task<Result<T>> TryPatchMultipartAsync<T>(string endpoint, MultipartFormDataContent content, CancellationToken ct = default)
        => Execute(() => PatchMultipartAsync<T>(endpoint, content, ct));

    public async Task<Result<bool>> TryDeleteAsync(string endpoint, CancellationToken ct = default)
    {
        try
        {
            var ok = await DeleteAsync(endpoint, ct);
            return ok
                ? Result<bool>.Ok(true)
                : Result<bool>.Fail(new SimpleApiError { Message = "The request could not be completed.", ErrorCode = "DELETE_FAILED" });
        }
        catch (Exception ex) { return Result<bool>.Fail(ToError(ex)); }
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static async Task<Result<T>> Execute<T>(Func<Task<T?>> send)
    {
        try { return Result<T>.Ok(await send()); }
        catch (Exception ex) { return Result<T>.Fail(ToError(ex)); }
    }

    private static IApiError ToError(Exception ex) => ex switch
    {
        ApiException api => api,
        TaskCanceledException => new SimpleApiError { Message = "Request timed out.", ErrorCode = "TIMEOUT" },
        HttpRequestException => new SimpleApiError { Message = ex.Message, ErrorCode = "NETWORK_ERROR" },
        JsonException => new SimpleApiError { Message = "Unexpected response from server.", ErrorCode = "PARSE_ERROR" },
        _ => new SimpleApiError { Message = ex.Message, ErrorCode = "UNKNOWN" },
    };

    private StringContent SerializeBody(object body) =>
        new(JsonSerializer.Serialize(body, _jsonOptions), Encoding.UTF8, "application/json");

    private async Task<T?> DeserializeAsync<T>(
        HttpResponseMessage response, string method, string endpoint, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
            throw await _exceptionFactory.CreateAsync(response, method, endpoint, cancellationToken);

        // 204 No Content (and any empty 2xx body) has nothing to deserialize — return
        // default instead of letting STJ throw on an empty string.
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return default;

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrEmpty(responseJson))
            return default;

        return JsonSerializer.Deserialize<T>(responseJson, _jsonOptions);
    }
}

/// <summary>
/// A success-or-failure carrier for the non-throwing <c>Try*Async</c> methods on
/// <see cref="ApiService"/>. Failures carry an <see cref="IApiError"/> — typically the
/// <see cref="ApiException"/> the throwing methods produced, or a <see cref="SimpleApiError"/>
/// for transport-level faults (timeout, network, parse errors).
/// </summary>
public sealed class Result<T>
{
    public T? Value { get; private set; }
    public IApiError? Error { get; private set; }
    public bool IsSuccess => Error is null;

    private Result() { }

    public static Result<T> Ok(T? value) => new() { Value = value };
    public static Result<T> Fail(IApiError error) => new() { Error = error };
}

/// <summary>
/// Lightweight <see cref="IApiError"/> for failures that never reached a parsed HTTP
/// response (timeouts, socket errors, deserialization faults).
/// </summary>
public sealed class SimpleApiError : IApiError
{
    public int StatusCode { get; init; }
    public string Message { get; init; } = "An unknown error occurred.";
    public string? ErrorCode { get; init; }

    public override string ToString() => $"[{StatusCode}] {ErrorCode ?? "ERROR"}: {Message}";
}
