using CommunityToolkit.Mvvm.Input;

using MyToolkit.Services;
using MyToolkit.Services.Errors;

namespace MyToolkit.ViewModels;

/// <summary>
/// ViewModel for the shared <c>ErrorReportPage</c> modal.
/// Exposes all text and theme colors as bindable properties so the XAML
/// carries zero hard-coded strings or colors.
/// </summary>
public partial class ErrorReportViewModel : ToolkitViewModel
{
    private readonly ErrorReport _report;
    private readonly IErrorTextProvider _text;
    private readonly IErrorToastPresenter _toast;

    /// <summary>Fired on successful email compose — the page pops the modal.</summary>
    public event Action? RequestClose;

    // ── Theme (colors) ───────────────────────────────────────────────────────
    public IErrorPageTheme Theme { get; }

    // ── Static text (set once, never change) ────────────────────────────────
    public string Title { get; }
    public string Summary { get; }
    public string TraceInfo { get; }
    public string WhatWereYouDoingLabel { get; }
    public string DescriptionPlaceholder { get; }
    public string TechnicalDetailsLabel { get; }
    public string DetailText { get; }
    public string CloseLabel { get; }
    public string BrandingText { get; }
    public string SendToAdminLabel { get; }

    /// <summary>True when an admin e-mail is configured; controls button visibility.</summary>
    public bool CanSendToAdmin { get; }

    // ── Mutable ──────────────────────────────────────────────────────────────
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _copyLabel;

    /// <summary>User-typed description of what they were doing; included in the admin e-mail body.</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _userDescription = string.Empty;

    public ErrorReportViewModel(ErrorReport report, IErrorTextProvider text, IErrorPageTheme theme, IErrorToastPresenter? toast = null)
    {
        _report = report;
        _text = text;
        _toast = toast ?? new NoOpErrorToastPresenter();
        Theme = theme;

        Title = text.ErrorReportTitle;
        Summary = report.Summary;
        TraceInfo = text.TraceLabel(report.TraceId, report.TimestampLocal);
        WhatWereYouDoingLabel = text.WhatWereYouDoingLabel;
        DescriptionPlaceholder = text.DescriptionPlaceholder;
        TechnicalDetailsLabel = text.TechnicalDetailsLabel;
        DetailText = report.FullDetail;
        CloseLabel = text.Close;
        BrandingText = text.BrandingText;
        SendToAdminLabel = text.SendToAdminLabel;
        _copyLabel = text.CopyDetails;
        CanSendToAdmin = !string.IsNullOrWhiteSpace(text.AdminEmail);
    }

    [RelayCommand]
    private async Task Copy()
    {
        try
        {
            await Clipboard.SetTextAsync(_report.FullDetail);
            CopyLabel = _text.Copied;
        }
        catch { /* clipboard unavailable */ }
    }

    [RelayCommand]
    private async Task SendToAdmin()
    {
        try
        {
            var adminEmail = _text.AdminEmail;
            if (string.IsNullOrWhiteSpace(adminEmail)) return;
            if (!Email.Default.IsComposeSupported) return;

            // Use MAUI's cache directory — its email implementation registers a
            // FileProvider for this path on Android, so the attachment is accessible
            // to the email app. Path.GetTempPath() returns an internal path that
            // third-party apps cannot read.
            var fileName = $"error_{_report.TraceId}.txt";
            var path = Path.Combine(FileSystem.Current.CacheDirectory, fileName);
            await File.WriteAllTextAsync(path, _report.FullDetail);

            var body = string.IsNullOrWhiteSpace(UserDescription)
                ? $"Trace ID: {_report.TraceId}\n\n{Summary}"
                : $"Trace ID: {_report.TraceId}\n\nWhat I was doing:\n{UserDescription}\n\n{Summary}";

            var recipients = adminEmail
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var message = new EmailMessage
            {
                Subject = $"{_text.AdminEmailSubject} [{_report.TraceId}]",
                Body = body,
                To = recipients,
                Attachments = [new EmailAttachment(path)]
            };

            await Email.Default.ComposeAsync(message);
            RequestClose?.Invoke();
        }
        catch
        {
            await _toast.ShowAsync(_text.ErrorToastTitle, _text.EmailSendFailed);
        }
    }
}
