using System.Text;
using MyToolkit.Views;

namespace MyToolkit.Services.Errors;

/// <summary>
/// A fully-rendered error report, ready to show on <c>ErrorReportPage</c> and to write
/// to the log. Produced by <see cref="ErrorHandler"/>; app code rarely touches it directly.
/// </summary>
public class ErrorReport
{
    public string TraceId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string FullDetail { get; set; } = string.Empty;
    public string TimestampLocal { get; set; } = string.Empty;
}

/// <summary>
/// Central exception sink. Every caught or unhandled exception flows through
/// <see cref="HandleAsync"/>. Every error is handled the same way: logged with full
/// detail, then shown as a brief non-blocking toast. The toast carries a tappable
/// "Details" action that opens the full scrollable <see cref="ErrorReportPage"/> on demand
/// (exception type, message, stack trace, inner-exception chain, server trace id / stack).
///
/// User-facing copy comes from the app's <see cref="IErrorTextProvider"/>; the toast
/// presenter is app-supplied (defaults to a no-op when none is registered).
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

    /// <summary>
    /// Logs the exception and shows a toast with a "Details" button that opens the
    /// full error report. Safe to call from any thread.
    /// </summary>
    public Task HandleAsync(Exception ex, string? context = null)
    {
        var report = Build(ex, context);
        LogReport(report);
        return ShowToastAsync(FriendlyMessage(ex), report);
    }

    /// <summary>Fire-and-forget convenience for event handlers and code-behind.</summary>
    public void Handle(Exception ex, string context) => _ = HandleAsync(ex, context);

    /// <summary>
    /// Shows a toast for a non-throwing API error (from a <see cref="Result{T}"/> failure).
    /// A "Details" button is available so the user can inspect the full report.
    /// </summary>
    public void Handle(IApiError error, string context)
    {
        _logger.LogApiError(error, context);
        var report = BuildFromApiError(error, context);
        _ = ShowToastAsync(error.Message, report);
    }

    /// <summary>Logs with full detail but shows no UI. For unrecoverable background failures.</summary>
    public void HandleSilent(Exception ex, string context)
        => LogReport(Build(ex, context));

    // ── Internals ──────────────────────────────────────────────────────────────

    private void LogReport(ErrorReport report)
        => _logger.Log("ERROR", $"{report.TraceId}\n{report.FullDetail}");

    private string FriendlyMessage(Exception ex) => ex switch
    {
        AppException app when !string.IsNullOrWhiteSpace(app.UserMessage) => app.UserMessage,
        ApiException api when !string.IsNullOrWhiteSpace(api.Message)    => api.Message,
        TaskCanceledException or OperationCanceledException               => _text.TimeoutError,
        HttpRequestException                                              => _text.NetworkError,
        _                                                                 => _text.UnexpectedError
    };

    private async Task ShowToastAsync(string message, ErrorReport report)
    {
        try
        {
            await _toast.ShowAsync(
                _text.ErrorToastTitle,
                message,
                onDetails: () => _ = ShowReportAsync(report),
                detailsLabel: _text.ErrorDetailsButton);
        }
        catch (Exception ex) { _logger.Log("ERROR", $"Failed to present error toast: {ex}"); }
    }

    private ErrorReport Build(Exception ex, string? context)
    {
        var traceId = (ex as ApiException)?.ServerTraceId
                      ?? Guid.NewGuid().ToString("N")[..12];

        var sb = new StringBuilder();
        sb.AppendLine($"Trace ID : {traceId}");
        sb.AppendLine($"Time     : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        if (!string.IsNullOrWhiteSpace(context))
            sb.AppendLine($"Context  : {context}");
        sb.AppendLine();
        AppendException(sb, ex, 0);

        var summary = (ex as AppException)?.UserMessage ?? ex.Message;
        if (string.IsNullOrWhiteSpace(summary))
            summary = _text.UnexpectedError;

        return new ErrorReport
        {
            TraceId          = traceId,
            Summary          = summary,
            TimestampLocal   = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            FullDetail       = sb.ToString()
        };
    }

    private ErrorReport BuildFromApiError(IApiError error, string context)
    {
        var api = error as ApiException;
        var traceId = api?.ServerTraceId ?? Guid.NewGuid().ToString("N")[..12];

        var sb = new StringBuilder();
        sb.AppendLine($"Trace ID : {traceId}");
        sb.AppendLine($"Time     : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Context  : {context}");
        sb.AppendLine($"Status   : {error.StatusCode}");
        if (!string.IsNullOrEmpty(error.ErrorCode)) sb.AppendLine($"Code     : {error.ErrorCode}");
        sb.AppendLine($"Message  : {error.Message}");
        if (api != null)
        {
            sb.AppendLine($"HTTP     : {api.Method} {api.Endpoint}");
            if (!string.IsNullOrEmpty(api.RawBody))          sb.AppendLine($"Body     : {api.RawBody}");
            if (!string.IsNullOrEmpty(api.ServerStackTrace)) sb.AppendLine($"\nServer stack trace:\n{api.ServerStackTrace}");
        }

        return new ErrorReport
        {
            TraceId        = traceId,
            Summary        = error.Message,
            TimestampLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            FullDetail     = sb.ToString()
        };
    }

    private static void AppendException(StringBuilder sb, Exception ex, int depth)
    {
        var indent = new string(' ', depth * 2);
        var label  = depth == 0 ? "Exception" : $"Inner exception (depth {depth})";
        sb.AppendLine($"{indent}── {label} ──");
        sb.AppendLine($"{indent}Type    : {ex.GetType().FullName}");
        sb.AppendLine($"{indent}Message : {ex.Message}");

        if (ex is ApiException api)
        {
            sb.AppendLine($"{indent}HTTP    : {api.Method} {api.Endpoint} → {(int)api.StatusCode} {api.StatusCode}");
            if (!string.IsNullOrEmpty(api.ServerCode))      sb.AppendLine($"{indent}Code    : {api.ServerCode}");
            if (!string.IsNullOrEmpty(api.ServerTraceId))   sb.AppendLine($"{indent}Server  : trace {api.ServerTraceId}");
            if (!string.IsNullOrEmpty(api.RawBody))         sb.AppendLine($"{indent}Body    : {api.RawBody}");
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
            foreach (var inner in agg.InnerExceptions) { sb.AppendLine(); AppendException(sb, inner, depth + 1); }
        else if (ex.InnerException != null)
        { sb.AppendLine(); AppendException(sb, ex.InnerException, depth + 1); }
    }

    private async Task ShowReportAsync(ErrorReport report)
    {
        if (_reportVisible) return;
        _reportVisible = true;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var nav = Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation
                          ?? Shell.Current?.Navigation;
                if (nav == null) { _reportVisible = false; return; }

                var theme    = ServiceHelper.GetSafe<IErrorPageTheme>() ?? new DefaultErrorPageTheme();
                var reporter = ServiceHelper.GetSafe<IErrorReporter>() ?? new EmailComposeReporter(_text);
                var vm       = new MyToolkit.ViewModels.ErrorReportViewModel(report, _text, theme, reporter, _toast);
                var page  = new ErrorReportPage(vm);
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
