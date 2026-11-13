namespace MyToolkit.Logging;

/// <summary>Verdict of <see cref="ErrorRepeatGuard.Check"/>.</summary>
/// <param name="Write">Whether the error should be written in full.</param>
/// <param name="SuppressedBefore">Identical errors suppressed since the last write (running count when not written).</param>
public readonly record struct ErrorRepeatDecision(bool Write, int SuppressedBefore);

/// <summary>Flood guard: lets one identical error through per window and counts the rest.</summary>
public sealed class ErrorRepeatGuard
{
    private sealed class Entry
    {
        public DateTimeOffset LastWritten;
        public int Suppressed;
    }

    private readonly TimeSpan _window;
    private readonly Func<DateTimeOffset> _now;
    private readonly int _maxKeys;
    private readonly Dictionary<string, Entry> _entries = new();
    private readonly Queue<string> _order = new();
    private readonly object _gate = new();

    /// <summary>Creates a guard; the oldest keys are evicted beyond <paramref name="maxKeys"/>.</summary>
    public ErrorRepeatGuard(TimeSpan window, Func<DateTimeOffset> now, int maxKeys = 256)
    {
        _window = window;
        _now = now;
        _maxKeys = maxKeys;
    }

    /// <summary>Decides whether an error with this key should be written now.</summary>
    public ErrorRepeatDecision Check(string key)
    {
        var now = _now();
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                _entries[key] = new Entry { LastWritten = now };
                _order.Enqueue(key);
                while (_entries.Count > _maxKeys && _order.TryDequeue(out var oldest))
                    _entries.Remove(oldest);
                return new ErrorRepeatDecision(true, 0);
            }

            if (now - entry.LastWritten >= _window)
            {
                var suppressed = entry.Suppressed;
                entry.Suppressed = 0;
                entry.LastWritten = now;
                return new ErrorRepeatDecision(true, suppressed);
            }

            entry.Suppressed++;
            return new ErrorRepeatDecision(false, entry.Suppressed);
        }
    }
}
