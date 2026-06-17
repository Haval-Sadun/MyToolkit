namespace MyToolkit.Services.Notifications;

public sealed partial class TabBadgeService : ITabBadgeService
{
    private bool _attached;

    public void Attach() => _attached = true;

    public void SetBadge(int tabIndex, int count)
    {
        if (!_attached) return;
        if (MainThread.IsMainThread) RenderBadge(tabIndex, count);
        else MainThread.BeginInvokeOnMainThread(() => RenderBadge(tabIndex, count));
    }

    // Implemented per-platform in Platforms/{Android,iOS}. No-op on other platforms.
    partial void RenderBadge(int tabIndex, int count);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
