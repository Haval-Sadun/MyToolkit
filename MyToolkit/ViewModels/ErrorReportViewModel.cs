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
    private readonly IErrorReporter _reporter;

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

    /// <summary>True when the reporter is capable of sending; controls "Send to Admin" button visibility.</summary>
    public bool CanSendToAdmin { get; }

    // ── Mutable ──────────────────────────────────────────────────────────────
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _copyLabel;

    /// <summary>User-typed description of what they were doing; included in the admin e-mail body.</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _userDescription = string.Empty;

    public ErrorReportViewModel(
        ErrorReport report,
        IErrorTextProvider text,
        IErrorPageTheme theme,
        IErrorReporter reporter,
        IErrorToastPresenter? toast = null)
    {
        _report = report;
        _text = text;
        _toast = toast ?? new NoOpErrorToastPresenter();
        _reporter = reporter;
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
        CanSendToAdmin = reporter.CanReport;
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
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var outcome = await _reporter.ReportAsync(_report, UserDescription);
            if (outcome.Succeeded)
            {
                if (!string.IsNullOrEmpty(outcome.Message))
                    await _toast.ShowAsync(_text.ErrorToastTitle, outcome.Message);
                RequestClose?.Invoke();
            }
            else
            {
                await _toast.ShowAsync(_text.ErrorToastTitle, outcome.Message ?? _text.EmailSendFailed);
            }
        }
        catch
        {
            await _toast.ShowAsync(_text.ErrorToastTitle, _text.EmailSendFailed);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
