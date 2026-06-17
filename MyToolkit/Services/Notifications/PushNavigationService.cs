using MyToolkit.Models;
using MyToolkit.Services.Auth;

namespace MyToolkit.Services.Notifications;

/// <summary>
/// Bridges a tapped push notification to in-app navigation.
///
/// When the OS launches/resumes the app from a system-tray notification, the platform layer
/// (MainActivity / AppDelegate) hands the FCM data payload to <see cref="HandlePushData"/>.
/// If the Shell and a signed-in user are ready, navigation happens immediately; on a cold start
/// the target is parked until <see cref="TryFlushAsync"/> is called from App.OnStart once auth
/// has initialised.
/// </summary>
public class PushNavigationService
{
    private readonly INotificationNavigator _navigator;
    private readonly IAuthStateProvider _auth;
    private Notification? _pending;

    public PushNavigationService(INotificationNavigator navigator, IAuthStateProvider auth)
    {
        _navigator = navigator;
        _auth = auth;
    }

    /// <summary>Called by platform code when a push notification is tapped.</summary>
    public void HandlePushData(IDictionary<string, string?> data)
    {
        if (data is null || data.Count == 0) return;
        _pending = CreateFromPushData(data);
        // Best-effort immediate navigation (warm start); harmless if Shell isn't ready yet.
        _ = TryFlushAsync();
    }

    /// <summary>
    /// Maps an FCM data payload to a <see cref="Notification"/> for navigation routing.
    /// Override in a subclass when your app uses different push payload keys or structure.
    /// The default implementation delegates to <see cref="Notification.FromPushData"/>.
    /// </summary>
    protected virtual Notification CreateFromPushData(IDictionary<string, string?> data)
        => Notification.FromPushData(data);

    /// <summary>Navigate to any parked target once the app is ready.</summary>
    public async Task TryFlushAsync()
    {
        var target = _pending;
        if (target is null) return;
        if (!_auth.IsAuthenticated) return;
        if (Shell.Current is null) return;

        _pending = null;
        await _navigator.NavigateAsync(target);
    }
}
