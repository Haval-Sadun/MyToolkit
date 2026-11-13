namespace MyToolkit.Collections;

/// <summary>Non-thread-safe O(1) LRU eviction order — Touch on every hit and insert, TryEvictOldest for eviction.</summary>
/// <remarks>Not thread-safe by design; callers that need thread safety (see LocalStreamProxy) must wrap every call in their own lock.</remarks>
public sealed class LeastRecentlyUsedTracker
{
    private readonly LinkedList<string> _order = new();
    private readonly Dictionary<string, LinkedListNode<string>> _nodes = new();

    public int Count => _order.Count;

    /// <summary>Adds <paramref name="key"/> at the MRU end, or moves it there if already tracked.</summary>
    public void Touch(string key)
    {
        if (_nodes.TryGetValue(key, out var node))
        {
            _order.Remove(node);
            _order.AddLast(node);
        }
        else
        {
            _nodes[key] = _order.AddLast(key);
        }
    }

    /// <summary>Removes <paramref name="key"/> so the tracker stays in sync when the backing store drops an entry explicitly.</summary>
    public void Remove(string key)
    {
        if (!_nodes.TryGetValue(key, out var node))
            return;

        _order.Remove(node);
        _nodes.Remove(key);
    }

    /// <summary>Removes and returns the least-recently-used key; returns false when empty.</summary>
    public bool TryEvictOldest(out string key)
    {
        var oldest = _order.First;
        if (oldest is null)
        {
            key = string.Empty;
            return false;
        }

        key = oldest.Value;
        _order.RemoveFirst();
        _nodes.Remove(key);
        return true;
    }

    /// <summary>Removes all tracked entries, equivalent to calling Remove on every key.</summary>
    public void Clear()
    {
        _order.Clear();
        _nodes.Clear();
    }

    /// <summary>Evicts the oldest entries while Count is at or above <paramref name="maxCount"/>, invoking <paramref name="onEvict"/> per evicted key so the caller can mirror-remove from its own backing store.</summary>
    /// <remarks>Assumes the caller's own backing store count tracks 1:1 with this tracker's Count.</remarks>
    public void EvictTo(int maxCount, Action<string> onEvict)
    {
        while (Count >= maxCount && TryEvictOldest(out var evicted))
            onEvict(evicted);
    }
}
