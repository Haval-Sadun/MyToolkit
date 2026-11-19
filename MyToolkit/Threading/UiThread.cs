namespace MyToolkit.Threading;

/// <summary>Runs work on the UI thread through the app's own dispatcher. MainThread asks the active window, which on Windows is missing while the window is inactive, minimized or closing ("Unable to find main thread").
/// Like MainThread, work already on the UI thread runs inline: the dispatcher always queues, which would put every call behind pending layout and render work.</summary>
public static class UiThread
{
    private static IDispatcher? Dispatcher => Application.Current?.Dispatcher;

    public static bool IsOnUiThread => Dispatcher is { } dispatcher ? !dispatcher.IsDispatchRequired : IsMainThreadSafe();

    public static void Post(Action action)
    {
        if (IsOnUiThread)
            action();
        else if (Dispatcher is { } dispatcher)
            dispatcher.Dispatch(action);
        else
            PostBeforeWindow(action);
    }

    // Before the first window exists MainThread throws on Windows.
    private static bool IsMainThreadSafe()
    {
        try
        {
            return MainThread.IsMainThread;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    // Startup work (the audio-cache scan) can announce changes before any window exists; there is no screen to update yet.
    private static void PostBeforeWindow(Action action)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(action);
        }
        catch (InvalidOperationException)
        {
            // No window yet: rows built later read the current state themselves.
        }
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
