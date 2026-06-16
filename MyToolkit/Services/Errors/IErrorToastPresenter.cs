namespace MyToolkit.Services.Errors;

/// <summary>
/// App-supplied seam for showing a brief, non-blocking notification (a toast/floater)
/// when the central <see cref="ErrorHandler"/> handles a <see cref="ErrorSeverity.Minor"/>
/// error. The toolkit owns the <em>when</em> and the <em>text</em> (via
/// <see cref="IErrorTextProvider"/>); each app supplies the <em>how</em> by delegating to
/// whatever presenter it already has (e.g. <c>IPopupPresenter.FloaterAsync</c> or a
/// CommunityToolkit <c>Toast</c>). This keeps the shared error pipeline decoupled from any
/// one app's popup type. Registered as a DI singleton; defaults to
/// <see cref="NoOpErrorToastPresenter"/> when an app supplies none.
/// </summary>
public interface IErrorToastPresenter
{
    /// <summary>Shows a transient, non-blocking notification. Must not throw.</summary>
    Task ShowAsync(string title, string message);
}

/// <summary>Fallback that shows nothing — used when an app registers no presenter.</summary>
public sealed class NoOpErrorToastPresenter : IErrorToastPresenter
{
    public Task ShowAsync(string title, string message) => Task.CompletedTask;
}
