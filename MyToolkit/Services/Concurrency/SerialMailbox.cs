using System.Threading.Channels;

namespace MyToolkit.Services.Concurrency;

/// <summary>Actor mailbox: messages run one at a time, in posting order, each to completion (including its awaits) before the next starts.</summary>
/// <remarks>Platform-free; callers pass the dispatcher (e.g. one that runs on the UI thread). Posting never runs a message inline, so a handler can't re-enter itself.</remarks>
public sealed class SerialMailbox(Func<Func<Task>, Task> dispatch, Action<Exception> onError)
{
    private readonly Channel<Func<Task>> _channel = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions { SingleReader = true });
    private int _started;

    /// <summary>Completes once <see cref="Complete"/> was called and every queued message has run. Null until <see cref="Start"/>.</summary>
    public Task? Completion { get; private set; }

    /// <summary>Starts the consumer. Messages posted earlier wait in the queue until then. Idempotent.</summary>
    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
            Completion = Task.Run(RunAsync);
    }

    /// <summary>False once <see cref="Complete"/> was called — the message is dropped.</summary>
    public bool Post(Func<Task> work) => _channel.Writer.TryWrite(work);

    public bool Post(Action work) => Post(() =>
    {
        work();
        return Task.CompletedTask;
    });

    /// <summary>Posts and returns a task that completes once the message has run. Never faults — handler errors go to the error callback.</summary>
    public Task Send(Func<Task> work)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var posted = Post(async () =>
        {
            try
            {
                await work();
            }
            finally
            {
                done.TrySetResult();
            }
        });

        if (!posted)
            done.TrySetResult();

        return done.Task;
    }

    public Task Send(Action work) => Send(() =>
    {
        work();
        return Task.CompletedTask;
    });

    /// <summary>Stops accepting messages; already-queued ones still run.</summary>
    public void Complete() => _channel.Writer.TryComplete();

    private async Task RunAsync()
    {
        await foreach (var work in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                await dispatch(work).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is control flow, not a fault.
            }
            catch (Exception ex)
            {
                onError(ex);
            }
        }
    }
}
