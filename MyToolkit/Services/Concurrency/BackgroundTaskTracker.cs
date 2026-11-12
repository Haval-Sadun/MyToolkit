namespace MyToolkit.Services.Concurrency;

/// <summary>
/// Owns fire-and-forget work instead of <c>_ = SomethingAsync()</c>: every task gets a token that is
/// cancelled when its caller's token is or when the tracker is disposed, faults are logged (never
/// unobserved), and <see cref="DisposeAsync"/> waits for whatever is still running.
/// </summary>
/// <remarks>Work starts inline on the caller's thread, like a bare call would — wrap it in <c>Task.Run</c> yourself if it must start off-thread.
/// Takes a log delegate, not AppLogger, so it stays platform-free and unit-tested.</remarks>
public sealed class BackgroundTaskTracker(Action<string, Exception?> log, TimeSpan? shutdownTimeout = null) : IAsyncDisposable
{
    private readonly TimeSpan _shutdownTimeout = shutdownTimeout ?? TimeSpan.FromSeconds(5);
    // Never disposed: linking to it must keep working right up to the disposed check, and it owns no timer.
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HashSet<Task> _running = [];
    private readonly object _gate = new();
    private int _disposed;

    public int RunningCount
    {
        get
        {
            lock (_gate)
                return _running.Count;
        }
    }

    public void Run(Func<CancellationToken, Task> work, string context, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            log($"background task '{context}' not started: tracker disposed", null);
            return;
        }

        var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
        var task = ObserveAsync(work, linked, context);

        lock (_gate)
            _running.Add(task);

        // Registered after Add, so a task that already completed synchronously is still removed.
        _ = task.ContinueWith(t =>
        {
            lock (_gate)
                _running.Remove(t);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private async Task ObserveAsync(Func<CancellationToken, Task> work, CancellationTokenSource linked, string context)
    {
        try
        {
            await work(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            // Cancelled by its owner or by shutdown — control flow, not a fault.
        }
        catch (Exception ex)
        {
            log($"background task '{context}'", ex);
        }
        finally
        {
            linked.Dispose();
        }
    }

    /// <summary>Cancels every tracked task and waits for them, up to the shutdown timeout. Never throws.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _lifetime.Cancel();

        Task[] pending;
        lock (_gate)
            pending = [.. _running];

        try
        {
            await Task.WhenAll(pending).WaitAsync(_shutdownTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            log($"background tasks: {pending.Count(t => !t.IsCompleted)} still running after {_shutdownTimeout.TotalSeconds:0.#}s shutdown wait", null);
        }
    }
}
