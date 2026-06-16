using System.Net;

namespace MyToolkit.Services.Errors;

// ─────────────────────────────────────────────────────────────────────────────
// The error model in one file: the severity scale, the app-agnostic error contract
// (IApiError) and its primary implementation (ApiException), plus AppException for
// app-raised failures. ErrorSeverity is shared by both exception types, so it leads.
// All of these flow into ErrorHandler.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Decides how the central <see cref="ErrorHandler"/> reacts to an exception.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>Expected/recoverable (validation, connectivity). Logged only.</summary>
    Minor,

    /// <summary>Unexpected failure. Logged AND shown on the full report screen.</summary>
    Important
}

/// <summary>
/// App-agnostic view of a backend error envelope. Lets toolkit types (e.g. a
/// <c>Result&lt;T&gt;</c> failure, the central error handler) read the essentials of
/// an API error without coupling to any one app's concrete error model. Implemented by
/// <see cref="ApiException"/> (parsed HTTP failures) and <c>SimpleApiError</c> (transport
/// faults). An app can also mark its own error DTO <c>: IApiError</c>.
/// </summary>
/// <example>
/// Consume it from a <c>Result</c> failure without knowing the concrete type:
/// <code>
/// if (!res.IsSuccess)
///     _errorHandler.Handle(res.Error!, "save profile");   // Handle(IApiError, string)
/// </code>
/// </example>
public interface IApiError
{
    /// <summary>HTTP status code of the failed response.</summary>
    int StatusCode { get; }

    /// <summary>User-safe message describing the failure.</summary>
    string Message { get; }

    /// <summary>Optional machine-readable backend error code.</summary>
    string? ErrorCode { get; }
}

/// <summary>
/// Thrown by the API layer for any non-2xx response. Captures the backend error
/// envelope so the report screen can display server-side detail and the
/// <see cref="ServerTraceId"/> that correlates with the server log. Implements
/// <see cref="IApiError"/> so generic consumers can read its essentials. Built only by
/// <c>ApiExceptionFactory</c> — app code catches it (or reads it via <c>Result.Error</c>),
/// it never constructs one.
/// </summary>
public class ApiException : Exception, IApiError
{
    public HttpStatusCode StatusCode { get; }
    public string Method { get; }
    public string Endpoint { get; }
    public string? ServerCode { get; }
    public string? ServerTraceId { get; }
    public string? ServerStackTrace { get; }
    public string RawBody { get; }
    public ErrorSeverity Severity { get; }

    public ApiException(
        HttpStatusCode statusCode,
        string method,
        string endpoint,
        string rawBody,
        string? userMessage,
        string? serverCode,
        string? serverTraceId,
        string? serverStackTrace,
        ErrorSeverity severity)
        : base(string.IsNullOrWhiteSpace(userMessage)
            ? $"API {(int)statusCode} {statusCode}"
            : userMessage)
    {
        StatusCode = statusCode;
        Method = method;
        Endpoint = endpoint;
        RawBody = rawBody ?? string.Empty;
        ServerCode = serverCode;
        ServerTraceId = serverTraceId;
        ServerStackTrace = serverStackTrace;
        Severity = severity;
    }

    // IApiError — StatusCode name-clashes the HttpStatusCode property, so map explicitly.
    int IApiError.StatusCode => (int)StatusCode;
    string? IApiError.ErrorCode => ServerCode;
}

/// <summary>
/// Base for application-raised exceptions. Carries a user-facing message and a
/// <see cref="ErrorSeverity"/> that tells the central error handler whether to open
/// the full report screen or just log.
/// </summary>
/// <example>
/// Throw it from app logic when you want the central handler to take over:
/// <code>
/// throw new AppException(_text.Get("Profile_SaveFailed"), ErrorSeverity.Important);
/// </code>
/// </example>
public class AppException : Exception
{
    public ErrorSeverity Severity { get; }

    /// <summary>Message safe to show the user (already localized by the caller).</summary>
    public string UserMessage { get; }

    public AppException(
        string userMessage,
        ErrorSeverity severity = ErrorSeverity.Minor,
        Exception? inner = null)
        : base(userMessage, inner)
    {
        UserMessage = userMessage;
        Severity = severity;
    }
}
