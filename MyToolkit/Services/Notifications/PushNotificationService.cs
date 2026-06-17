using MyToolkit.Services.Auth;
using MyToolkit.Services.Net;
using Plugin.Firebase.CloudMessaging;

namespace MyToolkit.Services.Notifications;

/// <summary>
/// Bridges Firebase Cloud Messaging to the backend.
///
/// On sign-in fetches the FCM token and registers it via the endpoint supplied by
/// <see cref="INotificationEndpoints.RegisterDevice"/>; on sign-out unregisters so the
/// device stops receiving that user's pushes. Also re-registers when FCM rotates the token.
///
/// Displaying the notification in the system tray is handled by the OS — the backend sends
/// an FCM "notification" payload that Android/iOS renders automatically when the app is
/// backgrounded/closed. <see cref="IFirebaseCloudMessaging.NotificationReceived"/> fires
/// only while the app is in the <b>foreground</b>.
///
/// Subclass and override <see cref="OnForegroundPushReceived"/> to handle app-specific
/// payload keys (e.g. direct messages, custom notification types).
/// </summary>
public class PushNotificationService : IAsyncDisposable
{
    private readonly ApiService _api;
    private readonly IAuthStateProvider _auth;
    private readonly IFirebaseCloudMessaging _fcm;
    private readonly AppLogger _logger;
    private readonly NotificationService _notifications;
    private readonly INotificationEndpoints _endpoints;

    private string? _registeredToken;
    private bool _wired;
    private EventHandler<Plugin.Firebase.CloudMessaging.EventArgs.FCMTokenChangedEventArgs>? _tokenChangedHandler;
    private EventHandler<Plugin.Firebase.CloudMessaging.EventArgs.FCMNotificationReceivedEventArgs>? _notificationReceivedHandler;

    public PushNotificationService(
        ApiService api,
        IAuthStateProvider auth,
        IFirebaseCloudMessaging fcm,
        AppLogger logger,
        NotificationService notifications,
        INotificationEndpoints endpoints)
    {
        _api = api;
        _auth = auth;
        _fcm = fcm;
        _logger = logger;
        _notifications = notifications;
        _endpoints = endpoints;
        _auth.LoggedIn  += (_, _) => _ = RegisterAsync();
        _auth.LoggedOut += (_, _) => _ = UnregisterAsync();
    }

    /// <summary>Fetch the FCM token and register it with the backend.</summary>
    public async Task RegisterAsync()
    {
        try
        {
            WireEvents();
            await _fcm.CheckIfValidAsync();
            var token = await _fcm.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return;
            await SendRegistration(token);
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "PushNotificationService.RegisterAsync");
        }
    }

    private async Task SendRegistration(string token)
    {
        if (token == _registeredToken) return;

#if ANDROID
        const string platform = "android";
#elif IOS
        const string platform = "ios";
#else
        const string platform = "android";
#endif

        var res = await _api.TryPostAsync<StatusResponse>(
            _endpoints.RegisterDevice(), new { token, platform });
        if (res.IsSuccess)
            _registeredToken = token;
    }

    public async Task UnregisterAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_registeredToken)) return;
            await _api.TryPostAsync<StatusResponse>(
                _endpoints.UnregisterDevice(), new { token = _registeredToken });
            _registeredToken = null;
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "PushNotificationService.UnregisterAsync");
        }
    }

    private void WireEvents()
    {
        if (_wired) return;
        _wired = true;

        _tokenChangedHandler = async (_, e) =>
        {
            if (_auth.IsAuthenticated && !string.IsNullOrEmpty(e.Token))
            {
                try { await SendRegistration(e.Token); }
                catch (Exception ex) { _logger.LogException(ex, "PushNotificationService.TokenChanged"); }
            }
        };
        _fcm.TokenChanged += _tokenChangedHandler;

        // Fires only while the app is in the FOREGROUND.
        // Generic behaviour: always re-derive the notification count from the server (the DB
        // notification is the source of truth for the badge).
        // App-specific payload handling: override OnForegroundPushReceived in a subclass.
        _notificationReceivedHandler = (_, e) =>
        {
            if (!_auth.IsAuthenticated) return;
            try
            {
                _ = _notifications.RefreshUnreadCountAsync();
                if (e.Notification?.Data is { } data)
                    OnForegroundPushReceived(data);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "PushNotificationService.NotificationReceived");
            }
        };
        _fcm.NotificationReceived += _notificationReceivedHandler;
    }

    /// <summary>
    /// Called for every foreground FCM push after the notification count has been refreshed.
    /// Override in a subclass to handle app-specific payload keys.
    /// The base implementation is a no-op.
    /// </summary>
    protected virtual void OnForegroundPushReceived(IDictionary<string, string?> data) { }

    public virtual ValueTask DisposeAsync()
    {
        if (_tokenChangedHandler is not null)
        {
            _fcm.TokenChanged -= _tokenChangedHandler;
            _tokenChangedHandler = null;
        }
        if (_notificationReceivedHandler is not null)
        {
            _fcm.NotificationReceived -= _notificationReceivedHandler;
            _notificationReceivedHandler = null;
        }
        return ValueTask.CompletedTask;
    }
}
