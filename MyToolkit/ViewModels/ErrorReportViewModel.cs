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

    // ── Theme (colors) ───────────────────────────────────────────────────────
    public IErrorPageTheme Theme { get; }

    // ── Static text (set once, never change) ────────────────────────────────
    public string Title                 { get; }
    public string Summary               { get; }
    public string TraceInfo             { get; }
    public string WhatWereYouDoingLabel { get; }
    public string DescriptionPlaceholder { get; }
    public string TechnicalDetailsLabel { get; }
    public string DetailText            { get; }
    public string CloseLabel            { get; }
    public string BrandingText          { get; }

    // ── Mutable (Copy button text changes after a successful copy) ───────────
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _copyLabel;

    public ErrorReportViewModel(ErrorReport report, IErrorTextProvider text, IErrorPageTheme theme)
    {
        _report = report;
        _text   = text;
        Theme   = theme;

        Title                  = text.ErrorReportTitle;
        Summary                = report.Summary;
        TraceInfo              = text.TraceLabel(report.TraceId, report.TimestampLocal);
        WhatWereYouDoingLabel  = text.WhatWereYouDoingLabel;
        DescriptionPlaceholder = text.DescriptionPlaceholder;
        TechnicalDetailsLabel  = text.TechnicalDetailsLabel;
        DetailText             = report.FullDetail;
        CloseLabel             = text.Close;
        BrandingText           = text.BrandingText;
        _copyLabel             = text.CopyDetails;
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
}
