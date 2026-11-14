using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace MyToolkit.Platforms.Windows;

/// <summary>Lifts MAUI's Windows modal focus trap while a popup is open: the trap sends every focus request outside the top modal page (the Player) back to where focus was, so a text box in a popup over a modal could never be focused.</summary>
/// <remarks>Uses the trap's own private members by reflection, and does nothing if they are missing (another MAUI version). Holds the root container only between <see cref="Suspend"/> and <see cref="Resume"/>.</remarks>
internal sealed class ModalFocusTrapGuard
{
    private const string ContainerTypeName = "WindowRootViewContainer";
    private const int MaxSearchNodes = 4000;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private object? _container;

    /// <summary>Call once the popup is in the visual tree; <paramref name="start"/> is its native element, <paramref name="windowContent"/> the fallback search root.</summary>
    public void Suspend(DependencyObject? start, DependencyObject? windowContent)
    {
        if (_container is not null)
            return;

        var container = FindAbove(start) ?? FindBelow(windowContent);
        if (container is not null)
            Invoke(container, "DisableModalFocusTrap", onlyIfActive: true);
    }

    public void Resume()
    {
        if (_container is not { } container)
            return;

        _container = null;
        Invoke(container, "EnableModalFocusTrap", onlyIfActive: false);
    }

    // A popup's content hangs off a Popup, so follow the logical parent as well as the visual one.
    private static object? FindAbove(DependencyObject? start)
    {
        for (var node = start; node is not null; node = (node as FrameworkElement)?.Parent ?? VisualTreeHelper.GetParent(node))
        {
            if (node.GetType().Name == ContainerTypeName)
                return node;
        }

        return null;
    }

    private static object? FindBelow(DependencyObject? root)
    {
        if (root is null)
            return null;

        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);
        for (var seen = 0; queue.Count > 0 && seen < MaxSearchNodes; seen++)
        {
            var node = queue.Dequeue();
            if (node.GetType().Name == ContainerTypeName)
                return node;

            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
                queue.Enqueue(VisualTreeHelper.GetChild(node, i));
        }

        return null;
    }

    private void Invoke(object container, string method, bool onlyIfActive)
    {
        try
        {
            var type = container.GetType();
            // Re-enabling a trap that was never active would trap focus for no modal.
            if (onlyIfActive && type.GetField("_modalFocusTrapActive", Private)?.GetValue(container) is not true)
                return;

            if (type.GetMethod(method, Private, null, Type.EmptyTypes, null) is not { } target)
                return;

            target.Invoke(container, null);
            if (onlyIfActive)
                _container = container;
        }
        catch (Exception)
        {
            // Best effort: a MAUI version without these members just keeps its default focus behaviour.
        }
    }
}
