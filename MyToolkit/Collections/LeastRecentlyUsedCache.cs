namespace MyToolkit.Collections;

/// <summary>A size-capped dictionary that evicts the least recently used entry. Not thread-safe — confine it to one thread or actor, or lock around it.</summary>
public sealed class LeastRecentlyUsedCache<TValue>(int capacity)
{
    private readonly Dictionary<string, TValue> _entries = [];
    private readonly LeastRecentlyUsedTracker _recency = new();
    private readonly int _capacity = Math.Max(1, capacity);
    private long _evictions;

    public int Count => _entries.Count;

    /// <summary>Entries pushed out by the cap since creation — thread-safe to read.</summary>
    public long Evictions => Interlocked.Read(ref _evictions);

    /// <summary>A hit counts as a use, so a just-read entry isn't next in line for eviction.</summary>
    public bool TryGet(string key, out TValue value)
    {
        if (!_entries.TryGetValue(key, out value!))
            return false;

        _recency.Touch(key);
        return true;
    }

    public void Set(string key, TValue value)
    {
        _entries[key] = value;
        _recency.Touch(key);
        _recency.EvictTo(_capacity + 1, evicted =>
        {
            _entries.Remove(evicted);
            Interlocked.Increment(ref _evictions);
        });
    }

    public void Remove(string key)
    {
        _entries.Remove(key);
        _recency.Remove(key);
    }

    /// <summary>Drops every entry matching <paramref name="isDead"/> — not counted as evictions.</summary>
    public void RemoveWhere(Func<TValue, bool> isDead)
    {
        foreach (var key in _entries.Where(entry => isDead(entry.Value)).Select(entry => entry.Key).ToList())
            Remove(key);
    }
}
