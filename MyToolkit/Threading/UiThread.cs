namespace MyToolkit.Threading;

/// <summary>Runs work on the UI thread through the app's own dispatcher. MainThread asks the active window, which on Windows is missing while the window is inactive, minimized or closing ("Unable to find main thread").
/// Like MainThread, work already on the UI thread runs inline: the dispatcher always queues, which would put every call behind pending layout and render work.</summary>
public static class UiThread
{
    private static IDispatcher? Dispatcher => Application.Current?.Dispatcher;

    public static bool IsOnUiThread => Dispatcher is { } dispatcher ? !dispatcher.IsDispatchRequired : MainThread.IsMainThread;

    public static void Post(Action action)
    {
        if (IsOnUiThread)
            action();
        else if (Dispatcher is { } dispatcher)
            dispatcher.Dispatch(action);
        else
            MainThread.BeginInvokeOnMainThread(action);
    }

    public static Task InvokeAsync(Action action)
    {
        if (!IsOnUiThread)
            return Dispatcher is { } dispatcher ? dispatcher.DispatchAsync(action) : MainThread.InvokeOnMainThreadAsync(action);

        try
        {
            action();
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

    public static Task InvokeAsync(Func<Task> work)
    {
        if (!IsOnUiThread)
            return Dispatcher is { } dispatcher ? dispatcher.DispatchAsync(work) : MainThread.InvokeOnMainThreadAsync(work);

        try
        {
            return work();
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

    public static Task<T> InvokeAsync<T>(Func<T> work)
    {
        if (!IsOnUiThread)
            return Dispatcher is { } dispatcher ? dispatcher.DispatchAsync(work) : MainThread.InvokeOnMainThreadAsync(work);

        try
        {
            return Task.FromResult(work());
        }
        catch (Exception ex)
        {
            return Task.FromException<T>(ex);
        }
    }

    public static Task<T> InvokeAsync<T>(Func<Task<T>> work)
    {
        if (!IsOnUiThread)
            return Dispatcher is { } dispatcher ? dispatcher.DispatchAsync(work) : MainThread.InvokeOnMainThreadAsync(work);

        try
        {
            return work();
        }
        catch (Exception ex)
        {
            return Task.FromException<T>(ex);
        }
    }
}
