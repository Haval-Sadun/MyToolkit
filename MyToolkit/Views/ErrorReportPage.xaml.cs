using MyToolkit.Services.Errors;

namespace MyToolkit.Views;

/// <summary>
/// Shared modal that shows the full, copyable error dump for an <see cref="ErrorReport"/>.
/// All user-facing copy comes from the app's <see cref="IErrorTextProvider"/> so the page
/// carries no app-specific text. (Colours/RTL are currently dark-theme defaults — make them
/// themeable when a second app with different chrome adopts this page.)
/// </summary>
public partial class ErrorReportPage : ContentPage
{
    private readonly ErrorReport _report;
    private readonly IErrorTextProvider _text;

    public ErrorReportPage(ErrorReport report, IErrorTextProvider text)
    {
        InitializeComponent();
        _report = report;
        _text = text;

        TitleLabel.Text = text.ErrorReportTitle;
        SummaryLabel.Text = report.Summary;
        TraceLabel.Text = text.TraceLabel(report.TraceId, report.TimestampLocal);
        DetailLabel.Text = report.FullDetail;
        CopyButton.Text = text.CopyDetails;
        CloseButton.Text = text.Close;
    }

    private async void OnCopyClicked(object? sender, EventArgs e)
    {
        try
        {
            await Clipboard.SetTextAsync(_report.FullDetail);
            CopyButton.Text = _text.Copied;
        }
        catch { /* clipboard unavailable — ignore */ }
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
