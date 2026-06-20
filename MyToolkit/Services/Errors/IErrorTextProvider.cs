namespace MyToolkit.Services.Errors;

/// <summary>
/// Supplies every user-facing string the toolkit error stack needs. Each consuming app
/// implements this in its own language and registers it as a DI singleton.
/// Members with default implementations are opt-in additions — existing implementors
/// compile without change.
/// </summary>
public interface IErrorTextProvider
{
    // ── Error report screen ─────────────────────────────────────────────────

    /// <summary>Headline shown at the top of the error report modal.</summary>
    string ErrorReportTitle { get; }

    /// <summary>Question shown above the user-description input.</summary>
    string WhatWereYouDoingLabel => "What were you doing when the error occurred?";

    /// <summary>Placeholder text inside the user-description Entry.</summary>
    string DescriptionPlaceholder => "Describe what happened… (optional)";

    /// <summary>Section header above the technical detail box.</summary>
    string TechnicalDetailsLabel => "Technical Error Details";

    /// <summary>Label for the inline copy button beside the detail box.</summary>
    string CopyDetails { get; }

    /// <summary>Confirmation text shown on the copy button after a successful copy.</summary>
    string Copied { get; }

    /// <summary>Label for the close button.</summary>
    string Close { get; }

    /// <summary>Small branding label rendered inside the detail section.</summary>
    string BrandingText => "ERROR CAPTURE ENGINE";

    // ── Inline / toast errors ────────────────────────────────────────────────

    /// <summary>Generic summary used when an exception carries no user message.</summary>
    string UnexpectedError { get; }

    /// <summary>Inline message for connectivity failures.</summary>
    string NetworkError { get; }

    /// <summary>Inline message for request timeouts.</summary>
    string TimeoutError { get; }

    /// <summary>Title shown on the brief error toast.</summary>
    string ErrorToastTitle { get; }

    /// <summary>Label for the toast action button that opens the full report.</summary>
    string ErrorDetailsButton => "Details";

    /// <summary>
    /// Admin e-mail address for the "Send to Admin" button on the error report page.
    /// Return an empty string (default) to hide the button entirely.
    /// </summary>
    string AdminEmail => string.Empty;

    /// <summary>Label for the "Send to Admin" button on the error report page.</summary>
    string SendToAdminLabel => "Send to Admin";

    /// <summary>Subject line used when composing the admin error report e-mail.</summary>
    string AdminEmailSubject => "Error Report";

    /// <summary>Toast shown when the email client fails or is unavailable.</summary>
    string EmailSendFailed => "Failed to send email";

    /// <summary>Renders the trace/timestamp sub-label.</summary>
    string TraceLabel(string traceId, string timestampLocal);
}
