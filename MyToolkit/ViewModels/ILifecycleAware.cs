namespace MyToolkit.ViewModels;

/// <summary>
/// Navigation lifecycle hooks a toolkit page forwards to its view-model. The base
/// page (<c>ToolkitPage</c>) drives these so VMs can react to appear/disappear and
/// directional navigation without referencing MAUI page types directly.
/// </summary>
public interface ILifecycleAware
{
    void OnAppearing();
    void OnDisappearing();
    void OnNavigatedTo(NavigationDirection direction);
    void OnNavigatedFrom(NavigationDirection direction);
}

/// <summary>
/// Direction of a navigation transition relative to the current page, so VMs can
/// distinguish "went deeper" from "came back" (e.g. to skip a refetch on return).
/// </summary>
public enum NavigationDirection
{
    Unknown = 0,
    ToChild,
    FromChild
}
