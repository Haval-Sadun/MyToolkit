using System.Threading.Channels;

namespace MyToolkit.Logging;

/// <summary>Queues log entries and writes them on one background consumer; producers never block and never throw.</summary>
public sealed class LogWriter : IDisposable
{
    private readonly record struct Item(string? RollingLine, string? ErrorBlock);

    private readonly SizeRotatingFile _rolling;
    private readonly ErrorLogFile _errors;
    private readonly string _sessionId;
    private readonly string _appVersion;
    private readonly Func<DateTimeOffset> _now;
    private readonly Channel<Item> _channel;
    private readonly object _flushGate = new();
    private readonly object _startGate = new();
    private Task? _consumer;
    private long _enqueued;
    private long _processed;
    private long _dropped;
    private long _dropsReported;
    private int _waiters;
    private int _disposed;

    /// <summary>Creates the writer; pass <paramref name="startConsumer"/> false to defer the consumer until <see cref="Start"/>.</summary>
    public LogWriter(SizeRotatingFile rolling, ErrorLogFile errors, BreadcrumbBuffer breadcrumbs, string sessionId, string appVersion, Func<DateTimeOffset> now, int capacity = 10_000, bool startConsumer = true)
    {
        _rolling = rolling;
        _errors = errors;
        Breadcrumbs = breadcrumbs;
        _sessionId = sessionId;
        _appVersion = appVersion;
        _now = now;
        _channel = Channel.CreateBounded<Item>(new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true }, _ => OnDropped());
        if (startConsumer)
            Start();
    }

    /// <summary>Recent INFO/WARN lines, fed by <see cref="WriteLine"/>.</summary>
    public BreadcrumbBuffer Breadcrumbs { get; }

    /// <summary>WARN messages starting with one of these are also persisted to the error file.</summary>
    public string[] PersistentWarnPrefixes { get; set; } = ["db.", "catalogue.parse.suspect"];

    /// <summary>Lines dropped because the queue was full.</summary>
    public long DroppedCount => Interlocked.Read(ref _dropped);

    /// <summary>Starts the consumer; safe to call more than once.</summary>
    public void Start()
    {
        lock (_startGate)
            _consumer ??= Task.Run(ConsumeAsync);
    }

    /// <summary>Formats and queues a line; returns it so callers can mirror it. INFO/WARN also feed the breadcrumbs.</summary>
    public string WriteLine(string level, string message)
    {
        var time = _now();
        var line = LogEntryFormatter.FormatLine(time, level, message);
        try
        {
            string? block = null;
            if (level == "WARN")
            {
                Breadcrumbs.Add(line);
                if (IsPersistentWarn(message))
                    block = LogEntryFormatter.FormatWarnBlock(time, _sessionId, _appVersion, line);
            }
            else if (level == "INFO")
            {
                Breadcrumbs.Add(line);
            }
            Enqueue(new Item(line, block));
        }
        catch
        {
            // Logging must never throw.
        }
        return line;
    }

    /// <summary>Queues a full error block plus its one-line copy for the rolling file.</summary>
    public void WriteError(string block, string summary)
    {
        try
        {
            Enqueue(new Item(LogEntryFormatter.FormatLine(_now(), "ERROR", summary), block));
        }
        catch
        {
            // Logging must never throw.
        }
    }

    /// <summary>Queues a pre-formatted header (session start) for both files.</summary>
    public void WriteHeader(string header) => Enqueue(new Item(header, header));

    /// <summary>Blocks until everything queued before the call is written, or the timeout elapses.</summary>
    public bool Flush(TimeSpan timeout)
    {
        var target = Interlocked.Read(ref _enqueued);
        if (Interlocked.Read(ref _processed) >= target)
            return true;

        var deadline = DateTime.UtcNow + timeout;
        Interlocked.Increment(ref _waiters);
        try
        {
            lock (_flushGate)
            {
                while (Interlocked.Read(ref _processed) < target)
                {
                    var remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                        return false;
                    Monitor.Wait(_flushGate, remaining < TimeSpan.FromMilliseconds(50) ? remaining : TimeSpan.FromMilliseconds(50));
                }
                return true;
            }
        }
        finally
        {
            Interlocked.Decrement(ref _waiters);
        }
    }

    /// <summary>Stops accepting entries and flushes with a 2 s cap.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        try
        {
            Start();
            _channel.Writer.TryComplete();
            Flush(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Logging must never throw.
        }
    }

    private bool IsPersistentWarn(string message)
    {
        foreach (var prefix in PersistentWarnPrefixes)
        {
            if (message.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private void Enqueue(Item item)
    {
        Interlocked.Increment(ref _enqueued);
        // A completed channel rejects the write: count it as handled so Flush can't wait on it.
        if (!_channel.Writer.TryWrite(item))
            MarkProcessed();
    }

    private void OnDropped()
    {
        Interlocked.Increment(ref _dropped);
        MarkProcessed();
    }

    private void MarkProcessed()
    {
        Interlocked.Increment(ref _processed);
        if (Volatile.Read(ref _waiters) == 0)
            return;

        lock (_flushGate)
            Monitor.PulseAll(_flushGate);
    }

    private async Task ConsumeAsync()
    {
        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    if (item.RollingLine is not null)
                        _rolling.Append(item.RollingLine);
                    if (item.ErrorBlock is not null)
                        _errors.Append(item.ErrorBlock);
                    if (_channel.Reader.Count == 0)
                        ReportDrops();
                }
                catch
                {
                    // One bad item must not kill the loop.
                }
                MarkProcessed();
            }
        }
        catch
        {
            // Consumer ends only on shutdown.
        }
    }

    private void ReportDrops()
    {
        var dropped = Interlocked.Read(ref _dropped);
        var fresh = dropped - _dropsReported;
        if (fresh <= 0)
            return;

        _dropsReported = dropped;
        _rolling.Append(LogEntryFormatter.FormatLine(_now(), "INFO", $"log.dropped count={fresh}"));
    }
}
