using System.Text.Json;
using MyToolkit.Services.Errors;

namespace MyToolkit.Services.Net;

// ─────────────────────────────────────────────────────────────────────────────
// The non-throwing HTTP surface, in one file: the contract (IResultApiService), its only
// implementation (ResultApiService), and the carrier types those return (Result<T>,
// SimpleApiError). Together they form the "result style" half of the API layer; the
// "throwing style" half is ApiService. Both ride the SAME pipeline + token store.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Non-throwing HTTP surface returning <see cref="Result{T}"/>. This is the second of
/// the two supported surfaces (the first being the throwing <see cref="ApiService"/>);
/// apps that prefer explicit success/failure values (e.g. KurdishConnect) depend on
/// this, while apps that prefer try/catch use <see cref="ApiService"/> directly.
/// </summary>
/// <example>
/// In a service or view model, inject <c>IResultApiService</c> and branch on the result —
/// no try/catch needed:
/// <code>
/// var res = await _api.GetAsync&lt;List&lt;Post&gt;&gt;("feed/");
/// if (!res.IsSuccess)
/// {
///     _errorHandler.Handle(res.Error!, "load feed");   // res.Error is IApiError
///     return;
/// }
/// Posts = res.Value!;
/// </code>
/// </example>
public interface IResultApiService
{
    Task<Result<T>> GetAsync<T>(string url, CancellationToken cancellationToken = default);
    Task<Result<T>> PostAsync<T>(string url, object body, CancellationToken cancellationToken = default);
    Task<Result<T>> PostMultipartAsync<T>(string url, MultipartFormDataContent content, CancellationToken cancellationToken = default);
    Task<Result<T>> PutAsync<T>(string url, object body, CancellationToken cancellationToken = default);
    Task<Result<T>> PutMultipartAsync<T>(string url, MultipartFormDataContent content, CancellationToken cancellationToken = default);
    Task<Result<T>> PatchMultipartAsync<T>(string url, MultipartFormDataContent content, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="Result{T}"/>-returning adapter over the throwing <see cref="ApiService"/>.
/// It owns NO transport or auth logic — it only translates outcomes: a returned value
/// becomes <c>Ok</c>; an <see cref="ApiException"/> (or a transport fault) becomes
/// <c>Fail</c> with the matching <see cref="IApiError"/>. This is what lets both result
/// styles ride the same single pipeline. Registered (via <c>AddApiService</c>) only when
/// the app hasn't supplied its own <see cref="IResultApiService"/>.
/// </summary>
public sealed class ResultApiService : IResultApiService
{
    private readonly ApiService _api;

    public ResultApiService(ApiService api) => _api = api;

    public Task<Result<T>> GetAsync<T>(string url, CancellationToken ct = default)
        => Execute(() => _api.GetAsync<T>(url, ct));

    public Task<Result<T>> PostAsync<T>(string url, object body, CancellationToken ct = default)
        => Execute(() => _api.PostAsync<T>(url, body, ct));

    public Task<Result<T>> PostMultipartAsync<T>(string url, MultipartFormDataContent content, CancellationToken ct = default)
        => Execute(() => _api.PostMultipartAsync<T>(url, content, ct));

    public Task<Result<T>> PutAsync<T>(string url, object body, CancellationToken ct = default)
        => Execute(() => _api.PutAsync<T>(url, body, ct));

    public Task<Result<T>> PutMultipartAsync<T>(string url, MultipartFormDataContent content, CancellationToken ct = default)
        => Execute(() => _api.PutMultipartAsync<T>(url, content, ct));

    public Task<Result<T>> PatchMultipartAsync<T>(string url, MultipartFormDataContent content, CancellationToken ct = default)
        => Execute(() => _api.PatchMultipartAsync<T>(url, content, ct));

    public async Task<Result<bool>> DeleteAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var ok = await _api.DeleteAsync(url, ct);
            return ok
                ? Result<bool>.Ok(true)
                : Result<bool>.Fail(new SimpleApiError { Message = "The request could not be completed.", ErrorCode = "DELETE_FAILED" });
        }
        catch (Exception ex) { return Result<bool>.Fail(ToError(ex)); }
    }

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
}

/// <summary>
/// A success-or-failure carrier for the <see cref="IResultApiService"/> (non-throwing)
/// surface. Failures carry an <see cref="IApiError"/> — typically the
/// <see cref="ApiException"/> the throwing <see cref="ApiService"/> produced, or a
/// <see cref="SimpleApiError"/> for transport-level faults (timeout/network/parse).
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
