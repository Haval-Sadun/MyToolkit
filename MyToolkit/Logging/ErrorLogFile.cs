using System.Globalization;
using System.Text;

namespace MyToolkit.Logging;

/// <summary>Persistent file of entry blocks, trimmed by size (oldest blocks first) and age. Never throws.</summary>
public sealed class ErrorLogFile
{
    private static readonly Encoding NoBom = new UTF8Encoding(false);
    private const string BlockPrefix = "=== ";
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss.fff";
    private static readonly TimeSpan PruneInterval = TimeSpan.FromHours(1);

    private readonly long _maxBytes;
    private readonly TimeSpan _maxAge;
    private readonly Func<DateTimeOffset> _now;
    private readonly object _gate = new();
    private DateTimeOffset? _lastPrune;

    /// <summary>Creates the file wrapper; the file itself is created on first append.</summary>
    public ErrorLogFile(string path, long maxBytes, TimeSpan maxAge, Func<DateTimeOffset> now)
    {
        FilePath = path;
        _maxBytes = maxBytes;
        _maxAge = maxAge;
        _now = now;
    }

    /// <summary>Full path of the file.</summary>
    public string FilePath { get; }

    /// <summary>Appends a block, then trims when oversized or when the hourly age check is due; false on IO failure.</summary>
    public bool Append(string block)
    {
        var text = block.EndsWith('\n') ? block : block + Environment.NewLine;
        lock (_gate)
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.AppendAllText(FilePath, text, NoBom);
                Trim();
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    private void Trim()
    {
        var now = _now();
        var oversized = new FileInfo(FilePath).Length > _maxBytes;
        var ageDue = _lastPrune is null || now - _lastPrune >= PruneInterval;
        if (!oversized && !ageDue)
            return;

        _lastPrune = now;
        var blocks = ReadBlocks();
        var original = blocks.Count;

        blocks.RemoveAll(b => TryParseTime(b, out var t) && now - t > _maxAge);

        var total = blocks.Sum(b => (long)Encoding.UTF8.GetByteCount(b));
        if (total > _maxBytes)
        {
            var target = _maxBytes * 3 / 4;
            while (blocks.Count > 1 && total > target)
            {
                total -= Encoding.UTF8.GetByteCount(blocks[0]);
                blocks.RemoveAt(0);
            }
        }

        if (blocks.Count != original)
            File.WriteAllText(FilePath, string.Concat(blocks), NoBom);
    }

    // A block starts at any "=== " line except the end marker and runs to the next such line.
    private List<string> ReadBlocks()
    {
        var blocks = new List<string>();
        var current = new StringBuilder();
        foreach (var raw in File.ReadLines(FilePath))
        {
            var isStart = raw.StartsWith(BlockPrefix, StringComparison.Ordinal) && raw != LogEntryFormatter.EndMarker;
            if (isStart && current.Length > 0)
            {
                blocks.Add(current.ToString());
                current.Clear();
            }
            current.Append(raw).Append(Environment.NewLine);
        }
        if (current.Length > 0)
            blocks.Add(current.ToString());
        return blocks;
    }

    private static bool TryParseTime(string block, out DateTimeOffset time)
    {
        time = default;
        var firstSpace = block.IndexOf(' ', BlockPrefix.Length);
        if (firstSpace < 0 || block.Length < firstSpace + 1 + TimeFormat.Length)
            return false;
        var stamp = block.Substring(firstSpace + 1, TimeFormat.Length);
        if (!DateTime.TryParseExact(stamp, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            return false;
        time = new DateTimeOffset(parsed);
        return true;
    }
}
