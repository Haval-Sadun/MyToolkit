namespace MyToolkit.Services.Errors;

/// <summary>
/// App-supplied seam for showing a brief, non-blocking error notification. The toolkit
/// owns the <em>when</em> and the <em>text</em>; each app supplies the <em>how</em> by
/// delegating to whatever presenter it already uses (e.g. UXDivers Floater, Snackbar).
///
/// When <paramref name="onDetails"/> is non-null the presenter should show a tappable
/// action button (labelled <paramref name="detailsLabel"/>) that opens the full error
/// report. When null, a plain non-interactive notification is shown.
/// </summary>
public interface IErrorToastPresenter
{
    /// <summary>Shows a transient, non-blocking error notification. Must not throw.</summary>
    Task ShowAsync(string title, string message, Action? onDetails = null, string detailsLabel = "Details");
}

/// <summary>Fallback that shows nothing — used when an app registers no presenter.</summary>
public sealed class NoOpErrorToastPresenter : IErrorToastPresenter
{
    public Task ShowAsync(string title, string message, Action? onDetails = null, string detailsLabel = "Details")
        => Task.CompletedTask;
}
