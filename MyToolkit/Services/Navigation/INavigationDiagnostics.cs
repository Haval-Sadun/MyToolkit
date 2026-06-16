namespace MyToolkit.Services.Navigation;

/// <summary>
/// Optional hooks the toolkit <see cref="Navigator"/> uses to report navigation
/// activity and surface failures. Each app adapts these to its own logging and
/// error-presentation UX (Snackbar/Floater, modal report screen, etc.). If no
/// implementation is registered in DI, navigation still works — silently.
/// </summary>
public interface INavigationDiagnostics
{
    /// <summary>Informational trace (e.g. "Navigation 'PushAsync'").</summary>
    void Log(string message);

    /// <summary>A navigation attempt threw; present/record it however the app prefers.</summary>
    void HandleError(Exception ex, string context);
}
