namespace MyToolkit.Services.Notifications;

/// <summary>
/// App-specific URL factory for every notification-related backend endpoint.
/// Implement once per consuming app and register as a singleton.
/// </summary>
public interface INotificationEndpoints
{
    string GetNotifications(string? cursor = null);
    string GetUnreadCount();
    string MarkNotificationRead(long id);
    string MarkAllNotificationsRead();
    string DeleteNotification(long id);
    string RegisterDevice();
    string UnregisterDevice();
}
