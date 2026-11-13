using System.Text;

namespace MyToolkit.Logging;

/// <summary>Append-only text file that rotates by size: <c>base.log</c>, <c>base.1.log</c>, <c>base.2.log</c>... Never throws.</summary>
public sealed class SizeRotatingFile
{
    private readonly string _directory;
    private readonly string _baseName;
    private readonly long _maxBytes;
    private readonly int _maxFiles;
    private static readonly Encoding NoBom = new UTF8Encoding(false);
    private readonly object _gate = new();
    private long _size;

    /// <summary>Opens (or starts) the log; <paramref name="maxFiles"/> counts the current file too.</summary>
    public SizeRotatingFile(string directory, string baseName, long maxBytes, int maxFiles)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFiles, 1);
        _directory = directory;
        _baseName = baseName;
        _maxBytes = maxBytes;
        _maxFiles = maxFiles;
        try
        {
            Directory.CreateDirectory(directory);
            _size = CurrentSize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _size = 0;
        }
    }

    /// <summary>Path of the current (newest) file.</summary>
    public string CurrentPath => PathFor(0);

    /// <summary>Existing files, oldest first.</summary>
    public IReadOnlyList<string> Files
    {
        get
        {
            lock (_gate)
            {
                var result = new List<string>();
                for (var i = _maxFiles - 1; i >= 0; i--)
                {
                    var path = PathFor(i);
                    if (File.Exists(path))
                        result.Add(path);
                }
                return result;
            }
        }
    }

    /// <summary>Appends a line (newline added); returns false when the write failed.</summary>
    public bool Append(string line)
    {
        var text = line + Environment.NewLine;
        var bytes = Encoding.UTF8.GetByteCount(text);
        lock (_gate)
        {
            try
            {
                if (_size > 0 && _size + bytes > _maxBytes)
                    Rotate();
                Write(text);
                _size += bytes;
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    private void Write(string text)
    {
        try
        {
            File.AppendAllText(CurrentPath, text, NoBom);
        }
        catch (DirectoryNotFoundException)
        {
            // Folder removed mid-run: recreate once.
            Directory.CreateDirectory(_directory);
            File.AppendAllText(CurrentPath, text, NoBom);
        }
    }

    private void Rotate()
    {
        try
        {
            File.Delete(PathFor(_maxFiles - 1));
            for (var i = _maxFiles - 2; i >= 0; i--)
            {
                var from = PathFor(i);
                if (File.Exists(from))
                    File.Move(from, PathFor(i + 1), true);
            }
        }
        finally
        {
            // Re-sync so a half-failed rotation can't leave a stale size.
            _size = CurrentSize();
        }
    }

    private long CurrentSize()
    {
        var info = new FileInfo(CurrentPath);
        return info.Exists ? info.Length : 0;
    }

    private string PathFor(int index) => Path.Combine(_directory, index == 0 ? $"{_baseName}.log" : $"{_baseName}.{index}.log");
}
