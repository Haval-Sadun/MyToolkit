namespace MyToolkit.Logging;

/// <summary>Thread-safe ring of the most recent formatted log lines.</summary>
public sealed class BreadcrumbBuffer
{
    private readonly string[] _ring;
    private readonly object _gate = new();
    private int _next;
    private int _count;

    /// <summary>Creates a ring holding at most <paramref name="capacity"/> lines.</summary>
    public BreadcrumbBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _ring = new string[capacity];
    }

    /// <summary>Appends a line, overwriting the oldest when full.</summary>
    public void Add(string line)
    {
        lock (_gate)
        {
            _ring[_next] = line;
            _next = (_next + 1) % _ring.Length;
            if (_count < _ring.Length)
                _count++;
        }
    }

    /// <summary>Returns the buffered lines, oldest first.</summary>
    public IReadOnlyList<string> Snapshot()
    {
        lock (_gate)
        {
            var result = new string[_count];
            var start = (_next - _count + _ring.Length) % _ring.Length;
            for (var i = 0; i < _count; i++)
                result[i] = _ring[(start + i) % _ring.Length];
            return result;
        }
    }
}
