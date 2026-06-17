namespace MyToolkit.Services.Notifications;

/// <summary>
/// Renders unread-count badges on the Shell bottom tab bar. Holds NO business logic —
/// it only renders counts it is given. The caller decides which tabs get badges and when.
/// Native rendering is best-effort (per-platform partials); failures are swallowed and the
/// in-page fallback badge remains the on-screen source of truth.
/// </summary>
public interface ITabBadgeService : IAsyncDisposable
{
    /// <summary>
    /// Call once from AppShell constructor to mark the service as ready.
    /// After this returns, calls to <see cref="SetBadge"/> will attempt to render.
    /// </summary>
    void Attach();

    /// <summary>
    /// Set the badge on the tab at <paramref name="tabIndex"/> (zero-based).
    /// Pass 0 to clear the badge. Safe to call before <see cref="Attach"/> — the call
    /// is a no-op until the Shell is ready.
    /// </summary>
    void SetBadge(int tabIndex, int count);
}
