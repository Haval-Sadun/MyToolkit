using System.Collections.Concurrent;

namespace MyToolkit.Services.Concurrency;

/// <summary>Coalesces concurrent requests for the same key into one shared in-flight <see cref="Task{T}"/>,
/// preventing duplicate network work when the same resource is requested simultaneously by multiple callers.</summary>
public sealed class SingleFlight<T>
{
    // Lazy<Task<T>>: GetOrAdd's own factory can run more than once under contention; Lazy guarantees the work itself starts exactly once.
    private readonly ConcurrentDictionary<string, Lazy<Task<T>>> _inflight = new();

    /// <summary>Returns the shared in-flight Task for <paramref name="key"/>, starting
    /// <paramref name="work"/> if none is running yet. <paramref name="work"/> receives
    /// <see cref="CancellationToken.None"/> — the shared fetch must not be cancellable by any
    /// individual caller. Callers should apply <c>.WaitAsync(cancellationToken)</c> on the returned
    /// Task to honour their own cancellation independently without affecting other waiters.</summary>
    public Task<T> GetOrRunAsync(string key, Func<CancellationToken, Task<T>> work)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        // Wrap so a sync throw becomes a faulted Task, never a poisoned Lazy.
        var lazy = _inflight.GetOrAdd(key, _ => new Lazy<Task<T>>(() =>
        {
            try { return work(CancellationToken.None); }
            catch (Exception ex) { return Task.FromException<T>(ex); }
        }));
        var task = lazy.Value;

        // Every caller (winner and losers alike) races to clean up once the shared task finishes —
        // harmless: TryRemove's value-match makes only the first one actually succeed.
        _ = RemoveWhenDoneAsync(key, lazy, task);
        return task;
    }

    private async Task RemoveWhenDoneAsync(string key, Lazy<Task<T>> lazy, Task<T> task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // The real exception already reaches whoever awaits the Task this method returned to
            // GetOrRunAsync's own caller — this catch exists only so cleanup still runs on failure.
        }

        _inflight.TryRemove(new KeyValuePair<string, Lazy<Task<T>>>(key, lazy));
    }
}
