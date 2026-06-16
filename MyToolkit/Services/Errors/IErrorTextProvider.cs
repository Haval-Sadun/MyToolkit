namespace MyToolkit.Services.Errors;

/// <summary>
/// Supplies the user-facing strings the toolkit error stack needs (report screen
/// title, buttons, trace label). Each app implements this in its own language so
/// the shared <c>ErrorReportPage</c> / <c>ErrorHandler</c> carry no hardcoded,
/// app-specific copy. Registered as a DI singleton.
/// </summary>
public interface IErrorTextProvider
{
    /// <summary>Headline on the error report screen (e.g. "⚠ An app error occurred").</summary>
    string ErrorReportTitle { get; }

    /// <summary>Label for the "copy details" action.</summary>
    string CopyDetails { get; }

    /// <summary>Confirmation shown after details are copied.</summary>
    string Copied { get; }

    /// <summary>Label for the "close" action.</summary>
    string Close { get; }

    /// <summary>Generic summary used when an exception carries no user message.</summary>
    string UnexpectedError { get; }

    /// <summary>Inline message for connectivity failures (e.g. <c>HttpRequestException</c>).</summary>
    string NetworkError { get; }

    /// <summary>Inline message for request timeouts (e.g. <c>TaskCanceledException</c>).</summary>
    string TimeoutError { get; }

    /// <summary>Title shown on the brief error toast/floater (e.g. "Error").</summary>
    string ErrorToastTitle { get; }

    /// <summary>Label for the action button on the error toast that opens the full report (e.g. "Details").</summary>
    string ErrorDetailsButton => "Details";

    /// <summary>Renders the trace/timestamp sub-label (e.g. "Trace ID: {id} • {time}").</summary>
    string TraceLabel(string traceId, string timestampLocal);
}
