namespace MyToolkit.Services.Errors;

/// <summary>
/// Delivers an error report by opening the device's native email client via
/// <see cref="Email.Default.ComposeAsync"/>. This is the built-in fallback used
/// when no custom <see cref="IErrorReporter"/> is registered in DI.
/// Register it explicitly if you want to opt-in to the email-compose path without
/// providing a custom reporter.
/// </summary>
public sealed class EmailComposeReporter : IErrorReporter
{
    private readonly IErrorTextProvider _text;

    public EmailComposeReporter(IErrorTextProvider text) => _text = text;

    public bool CanReport =>
        !string.IsNullOrWhiteSpace(_text.AdminEmail) && Email.Default.IsComposeSupported;

    public async Task<ErrorReportOutcome> ReportAsync(
        ErrorReport report, string userDescription, CancellationToken ct = default)
    {
        if (!CanReport)
            return ErrorReportOutcome.Fail(_text.EmailSendFailed);

        try
        {
            // Delete stale attachments from previous sessions before writing the new one.
            // We can't delete immediately after ComposeAsync — the email app may still be
            // reading the file — so we clean up files older than one day instead.
            try
            {
                var cutoff = DateTime.Now.AddDays(-1);
                foreach (var f in Directory.GetFiles(FileSystem.Current.CacheDirectory, "error_*.txt"))
                    if (File.GetLastWriteTime(f) < cutoff)
                        File.Delete(f);
            }
            catch { /* best-effort */ }

            var path = Path.Combine(FileSystem.Current.CacheDirectory, $"error_{report.TraceId}.txt");
            await File.WriteAllTextAsync(path, report.FullDetail, ct);

            var body = string.IsNullOrWhiteSpace(userDescription)
                ? $"Trace ID: {report.TraceId}\n\n{report.Summary}"
                : $"Trace ID: {report.TraceId}\n\nWhat I was doing:\n{userDescription}\n\n{report.Summary}";

            var recipients = _text.AdminEmail
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            await Email.Default.ComposeAsync(new EmailMessage
            {
                Subject     = $"{_text.AdminEmailSubject} [{report.TraceId}]",
                Body        = body,
                To          = recipients,
                Attachments = [new EmailAttachment(path)],
            });

            return ErrorReportOutcome.Ok();
        }
        catch
        {
            return ErrorReportOutcome.Fail(_text.EmailSendFailed);
        }
    }
}
