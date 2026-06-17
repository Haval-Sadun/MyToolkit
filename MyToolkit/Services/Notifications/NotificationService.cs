using MyToolkit.Models;
using MyToolkit.Services.Auth;
using MyToolkit.Services.Net;

namespace MyToolkit.Services.Notifications;

/// <summary>
/// Owns the notification feed API and the app-wide unread notification count.
/// This is a DISTINCT concern from app-level chat message badge services — do not merge the two.
///
/// All count mutations and <see cref="UnreadCountChanged"/> are marshalled to the UI thread
/// (the count drives a native tab badge + in-page bindings). Tie into your app's realtime bus
/// by calling <see cref="RefreshUnreadCountAsync"/> from the appropriate message handlers.
/// </summary>
public class NotificationService
{
    private readonly ApiService _api;
    private readonly INotificationEndpoints _endpoints;

    public int UnreadCount { get; private set; }
    public event Action? UnreadCountChanged;

    public NotificationService(ApiService api, IAuthStateProvider auth, INotificationEndpoints endpoints)
    {
        _api = api;
        _endpoints = endpoints;
        auth.LoggedIn  += (_, _) => _ = RefreshUnreadCountAsync();
        auth.LoggedOut += (_, _) => ResetUnread();
    }

    // ----- API -----

    public Task<Result<PagedResponse<Notification>>> GetNotifications(string? cursor = null)
        => _api.TryGetAsync<PagedResponse<Notification>>(_endpoints.GetNotifications(cursor));

    public Task<Result<UnreadCountResponse>> GetUnreadCount()
        => _api.TryGetAsync<UnreadCountResponse>(_endpoints.GetUnreadCount());

    public Task<Result<StatusResponse>> MarkRead(long id)
        => _api.TryPostAsync<StatusResponse>(_endpoints.MarkNotificationRead(id), new { });

    public Task<Result<StatusResponse>> MarkAllRead()
        => _api.TryPostAsync<StatusResponse>(_endpoints.MarkAllNotificationsRead(), new { });

    public Task<Result<bool>> Delete(long id)
        => _api.TryDeleteAsync(_endpoints.DeleteNotification(id));

    // ----- Unread-count ownership -----

    private Task? _ongoingRefresh;

    public Task RefreshUnreadCountAsync()
    {
        if (_ongoingRefresh is { IsCompleted: false })
            return _ongoingRefresh;
        return _ongoingRefresh = DoRefreshAsync();
    }

    private async Task DoRefreshAsync()
    {
        var res = await GetUnreadCount();
        if (res.IsSuccess && res.Value is not null)
            SetUnread(res.Value.Count);
    }

    public void SetUnread(int count) => Apply(Math.Max(0, count));

    public void DecrementUnread(int by = 1) => Apply(Math.Max(0, UnreadCount - by));

    public void ResetUnread() => Apply(0);

    private void Apply(int value)
    {
        if (MainThread.IsMainThread) Set(value);
        else MainThread.BeginInvokeOnMainThread(() => Set(value));
    }

    private void Set(int value)
    {
        if (UnreadCount == value)
        {
            // Raise even on no-change so a fresh subscriber (e.g. tab badge after login)
            // can render the current value without waiting for the next mutation.
            UnreadCountChanged?.Invoke();
            return;
        }
        UnreadCount = value;
        UnreadCountChanged?.Invoke();
    }
}
