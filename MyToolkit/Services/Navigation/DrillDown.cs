using Microsoft.Extensions.DependencyInjection;

namespace MyToolkit.Services.Navigation;

/// <summary>Builds, pushes and disposes transient drill-down (detail) pages.</summary>
/// <remarks>Sole owner of these pages' view-model disposal, so pages leave ToolkitPage.DisposeViewModelOnPop off.</remarks>
public static class DrillDown
{
    // UI-thread only: pushes and Shell.Navigated both run there.
    private static readonly HashSet<Page> Pushed = [];

    /// <summary>Builds the page and its view-model outside the container: the root provider keeps every IDisposable transient it resolves alive until app exit.</summary>
    public static TPage Create<TPage, TViewModel>(IServiceProvider services) where TPage : Page where TViewModel : class
    {
        var viewModel = ActivatorUtilities.CreateInstance<TViewModel>(services);
        return ActivatorUtilities.CreateInstance<TPage>(services, viewModel);
    }

    /// <summary>Pushes <paramref name="page"/>; a push dropped by Navigator (e.g. a double-tap) is disposed straight away.</summary>
    public static async Task PushAsync(Page page)
    {
        await Navigator.PushAsync(page);

        if (Shell.Current?.Navigation.NavigationStack.Contains(page) == true)
            Pushed.Add(page);
        else
            Dispose(page);
    }

    /// <summary>Call from Shell.OnNavigated: disposes every pushed page no longer on any tab's stack — pop-to-root never raises the middle pages' OnNavigatedFrom.</summary>
    public static void ReleasePopped(Shell shell)
    {
        if (Pushed.Count == 0)
            return;

        var live = shell.Items.SelectMany(item => item.Items).SelectMany(section => section.Stack).ToHashSet();
        foreach (var page in Pushed.Where(page => !live.Contains(page)).ToList())
        {
            Pushed.Remove(page);
            Dispose(page);
        }
    }

    private static void Dispose(Page page) => (page.BindingContext as IDisposable)?.Dispose();
}
