namespace MyToolkit.Services.Errors;

/// <summary>
/// Pluggable seam for delivering error reports out of the app. The toolkit owns the when
/// and the what; each consuming app supplies the how by implementing this interface and
/// registering it as a DI singleton.
///
/// When no implementation is registered, <see cref="ErrorHandler"/> falls back to
/// <see cref="EmailComposeReporter"/>, which opens the device's native email client.
/// </summary>
public interface IErrorReporter
{
    /// <summary>
    /// Whether this reporter can currently deliver a report. Evaluated once when the error
    /// report page opens; controls the "Send to Admin" button visibility.
    /// </summary>
    bool CanReport { get; }

    /// <summary>
    /// Delivers <paramref name="report"/> to the administrator. Must not throw — implementations
    /// must catch all exceptions internally and return <see cref="ErrorReportOutcome.Fail"/>.
    /// </summary>
    Task<ErrorReportOutcome> ReportAsync(ErrorReport report, string userDescription, CancellationToken ct = default);
}

/// <summary>
/// Result of an <see cref="IErrorReporter.ReportAsync"/> call.
/// <list type="bullet">
/// <item><c>Succeeded = true, Message != null</c> — shows a success toast then closes the modal.</item>
/// <item><c>Succeeded = true, Message == null</c> — closes the modal silently.</item>
/// <item><c>Succeeded = false</c> — shows <c>Message</c> as an error toast; modal stays open.</item>
/// </list>
/// </summary>
public sealed record ErrorReportOutcome(bool Succeeded, string? Message = null)
{
    public static ErrorReportOutcome Ok(string? successMessage = null) => new(true, successMessage);
    public static ErrorReportOutcome Fail(string errorMessage)        => new(false, errorMessage);
}
