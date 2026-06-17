using MyToolkit.Models;

namespace MyToolkit.Services.Notifications;

/// <summary>
/// Turns a <see cref="Notification"/> into a screen navigation.
/// Used by both the in-app notification list and push taps so deep-link behaviour
/// can never diverge between the two entry points.
/// Implement in the consuming app (it knows the pages and ViewModels to push).
/// </summary>
public interface INotificationNavigator
{
    Task NavigateAsync(Notification n);
    Task NavigateToProfileAsync(string? userId);
}
