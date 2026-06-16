using System.Text;
using MyToolkit.Views;

namespace MyToolkit.Services.Errors;

/// <summary>
/// A fully-rendered error report, ready to show on <c>ErrorReportPage</c> and to
/// write to the log. <see cref="FullDetail"/> is the scrollable, copyable dump.
/// User-facing chrome (title, buttons) comes from <see cref="IErrorTextProvider"/>,
/// not from this data holder. Produced by <see cref="ErrorHandler"/>; app code rarely
/// touches it directly.
/// </summary>
public class ErrorReport
{
    public string TraceId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string FullDetail { get; set; } = string.Empty;
    public string TimestampLocal { get; set; } = string.Empty;
    public string Severity { get; set; } = "minor";
}

/// <summary>
/// Central exception sink. Every caught or unhandled exception flows through
/// <see cref="HandleAsync"/>. Behaviour depends on severity:
///
///   • Minor     → logged AND a brief, non-blocking toast/floater is shown with a
///                 friendly message (via the app's <see cref="IErrorToastPresenter"/>).
///   • Important → logged AND a modal report screen is shown containing the full,
///                 scrollable exception detail: type, message, stack trace with
///                 line numbers, the complete inner-exception chain, and any
///                 server-side trace id / stack returned by the backend.
///
/// In both cases the exception is logged with its full detail. User-facing copy
/// comes from the app's <see cref="IErrorTextProvider"/>; the toast presenter is
/// app-supplied (defaults to a no-op when none is registered).
/// </summary>
public class ErrorHandler
{
    private readonly AppLogger _logger;
    private readonly IErrorTextProvider _text;
    private readonly IErrorToastPresenter _toast;
    private bool _reportVisible;

    public ErrorHandler(AppLogger logger, IErrorTextProvider text, IErrorToastPresenter? toast = null)
    {
        _logger = logger;
        _text = text;
        _toast = toast ?? new NoOpErrorToastPresenter();
    }

    public Task HandleAsync(Exception ex, ErrorSeverity? severity = null, string? context = null)
    {
        var sev = severity ?? Classify(ex);
        var report = Build(ex, sev, context);
        LogReport(report, context);

        return sev == ErrorSeverity.Important
            ? ShowReportAsync(report)
            : ShowToastAsync(FriendlyMessage(ex));
    }

    /// <summary>
    /// Logs an exception and shows the user-facing surface for its severity
    /// (Minor → toast, Important → modal report). Fire-and-forget convenience for
    /// non-async call sites (event handlers, code-behind).
    /// </summary>
    public void Handle(Exception ex, string context) => _ = HandleAsync(ex, null, context);

    /// <summary>
    /// Logs a backend API error and shows its message as a brief toast. Use when a
    /// non-throwing API surface returns an <see cref="IApiError"/> rather than an exception.
    /// </summary>
    public void Handle(IApiError error, string context)
    {
        _logger.LogApiError(error, context);
        _ = ShowToastAsync(error.Message);
    }

    /// <summary>Logs an exception with full detail but shows no UI (no toast, no report).</summary>
    public void HandleSilent(Exception ex, string context)
        => LogReport(Build(ex, Classify(ex), context), context);

    private void LogReport(ErrorReport report, string? context)
        => _logger.Log(report.Severity == "important" ? "ERROR" : "WARN",
            $"{report.TraceId} {context}\n{report.FullDetail}");

    /// <summary>Maps an exception to a friendly, localized one-line message for the toast.</summary>
    private string FriendlyMessage(Exception ex) => ex switch
    {
        AppException app when !string.IsNullOrWhiteSpace(app.UserMessage) => app.UserMessage,
        ApiException api when !string.IsNullOrWhiteSpace(api.Message) => api.Message,
        TaskCanceledException or OperationCanceledException => _text.TimeoutError,
        HttpRequestException => _text.NetworkError,
        _ => _text.UnexpectedError
    };

    private async Task ShowToastAsync(string message)
    {
        try { await _toast.ShowAsync(_text.ErrorToastTitle, message); }
        catch (Exception ex) { _logger.Log("ERROR", $"Failed to present error toast: {ex}"); }
    }

    /// <summary>Default severity when the caller doesn't specify one.</summary>
    private static ErrorSeverity Classify(Exception ex) => ex switch
    {
        ApiException api => api.Severity,
        AppException app => app.Severity,
        OperationCanceledException => ErrorSeverity.Minor,
        // Connectivity is expected and recoverable, not a crash report.
        HttpRequestException => ErrorSeverity.Minor,
        _ => ErrorSeverity.Important
    };

    private ErrorReport Build(Exception ex, ErrorSeverity sev, string? context)
    {
        var traceId = (ex as ApiException)?.ServerTraceId
                      ?? Guid.NewGuid().ToString("N")[..12];

        var sb = new StringBuilder();
        sb.AppendLine($"Trace ID : {traceId}");
        sb.AppendLine($"Time     : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Severity : {sev}");
        if (!string.IsNullOrWhiteSpace(context))
            sb.AppendLine($"Context  : {context}");
        sb.AppendLine();
        AppendException(sb, ex, 0);

        var summary = (ex as AppException)?.UserMessage ?? ex.Message;
        if (string.IsNullOrWhiteSpace(summary))
            summary = _text.UnexpectedError;

        return new ErrorReport
        {
            TraceId = traceId,
            Severity = sev.ToString().ToLowerInvariant(),
            Summary = summary,
            TimestampLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            FullDetail = sb.ToString()
        };
    }

    /// <summary>
    /// Renders an exception and recurses through its inner-exception chain
    /// (and every branch of an AggregateException), each with stack trace.
    /// </summary>
    private static void AppendException(StringBuilder sb, Exception ex, int depth)
    {
        var indent = new string(' ', depth * 2);
        var label = depth == 0 ? "Exception" : $"Inner exception (depth {depth})";
        sb.AppendLine($"{indent}── {label} ──");
        sb.AppendLine($"{indent}Type    : {ex.GetType().FullName}");
        sb.AppendLine($"{indent}Message : {ex.Message}");

        if (ex is ApiException api)
        {
            sb.AppendLine($"{indent}HTTP    : {api.Method} {api.Endpoint} → {(int)api.StatusCode} {api.StatusCode}");
            if (!string.IsNullOrEmpty(api.ServerCode))    sb.AppendLine($"{indent}Code    : {api.ServerCode}");
            if (!string.IsNullOrEmpty(api.ServerTraceId)) sb.AppendLine($"{indent}Server  : trace {api.ServerTraceId}");
            if (!string.IsNullOrEmpty(api.RawBody))       sb.AppendLine($"{indent}Body    : {api.RawBody}");
            if (!string.IsNullOrEmpty(api.ServerStackTrace))
            {
                sb.AppendLine($"{indent}Server stack trace:");
                sb.AppendLine(api.ServerStackTrace);
            }
        }

        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            sb.AppendLine($"{indent}Stack trace:");
            sb.AppendLine(ex.StackTrace);
        }

        if (ex is AggregateException agg)
        {
            foreach (var inner in agg.InnerExceptions)
            {
                sb.AppendLine();
                AppendException(sb, inner, depth + 1);
            }
        }
        else if (ex.InnerException != null)
        {
            sb.AppendLine();
            AppendException(sb, ex.InnerException, depth + 1);
        }
    }

    private async Task ShowReportAsync(ErrorReport report)
    {
        if (_reportVisible) return;     // never stack report screens
        _reportVisible = true;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var nav = Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation
                          ?? Shell.Current?.Navigation;
                if (nav == null)
                {
                    _reportVisible = false;
                    return;
                }

                var page = new ErrorReportPage(report, _text);
                page.Disappearing += (_, _) => _reportVisible = false;
                await nav.PushModalAsync(page);
            }
            catch (Exception ex)
            {
                _reportVisible = false;
                _logger.Log("ERROR", $"Failed to present error report: {ex}");
            }
        });
    }
}
