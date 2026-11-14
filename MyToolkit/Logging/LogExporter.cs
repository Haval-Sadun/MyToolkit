using System.Text;
using MyToolkit.Services;

namespace MyToolkit.Logging;

/// <summary>What differs per app: the name in the export, which log lines count as events, and extra files in the log folder to attach.</summary>
/// <param name="AppName">Shown in the export header and file name.</param>
/// <param name="EventTags">A rolling-log line containing any of these is an event for <see cref="LogExporter.ReadRecentEventLines"/>.</param>
/// <param name="ExtraFilePatterns">File-name patterns in the log folder (e.g. "search-empty-*.json") appended whole to the export.</param>
public sealed record LogExporterOptions(string AppName, IReadOnlyList<string> EventTags, IReadOnlyList<string>? ExtraFilePatterns = null);

/// <summary>Packs errors.log and the rolling logs into one shareable .txt and reads recent log lines, for a diagnostics screen.</summary>
public sealed class LogExporter(AppLogger logger, LogExporterOptions options)
{
    /// <summary>Rolling-log share of the export: the newest bytes win.</summary>
    private const long RollingExportBytes = 2 * 1024 * 1024;

    /// <summary>Only the end of each log is scanned for the Diagnostics lists.</summary>
    private const long TailBytes = 512 * 1024;

    /// <summary>Shares errors, recent logs and the options' extra files. False when there's nothing to send yet.</summary>
    public async Task<bool> ShareAsync()
    {
        var path = await Task.Run(BuildLogFile);
        if (path is null)
            return false;

        await Share.Default.RequestAsync(new ShareFileRequest { Title = $"{options.AppName} {AppInfo.Current.VersionString} logs", File = new ShareFile(path, "text/plain") });
        return true;
    }

    /// <summary>The newest <paramref name="count"/> lines matching <see cref="LogExporterOptions.EventTags"/> from the rolling logs, newest last. Never throws. Does disk IO: call off the UI thread.</summary>
    public IReadOnlyList<string> ReadRecentEventLines(int count)
    {
        var recent = new Queue<string>();
        try
        {
            foreach (var path in logger.RollingLogPaths)
            {
                foreach (var line in ReadTailLines(path, TailBytes))
                {
                    if (!options.EventTags.Any(tag => line.Contains(tag, StringComparison.Ordinal)))
                        continue;

                    recent.Enqueue(line);
                    if (recent.Count > count)
                        recent.Dequeue();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Diagnostics are optional — show what was read so far.
        }

        return recent.ToArray();
    }

    /// <summary>The last <paramref name="count"/> ERROR entries of errors.log, newest first. Never throws. Does disk IO: call off the UI thread.</summary>
    public IReadOnlyList<ErrorLogEntry> ReadRecentErrors(int count)
    {
        try
        {
            return ErrorLogParser.ParseRecent(ReadTailLines(logger.ErrorLogPath, TailBytes), count);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    // Shared read: the logger may be appending right now.
    private static IEnumerable<string> ReadTailLines(string path, long maxBytes)
    {
        if (!File.Exists(path))
            yield break;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var start = Math.Max(0, stream.Length - maxBytes);
        stream.Seek(start, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);

        // Started mid-line: drop the fragment.
        if (start > 0)
            reader.ReadLine();

        while (reader.ReadLine() is { } line)
            yield return line;
    }

    /// <summary>Header, errors.log, rolling-log tails and extra files in one file in the cache folder. Null when there is nothing to send.</summary>
    private string? BuildLogFile()
    {
        try
        {
            logger.Flush();
            if (!Directory.Exists(logger.LogDirectory))
                return null;

            var errorsPath = logger.ErrorLogPath;
            var rolling = logger.RollingLogPaths.Where(File.Exists).ToList();
            var searches = (options.ExtraFilePatterns ?? []).SelectMany(pattern => Directory.GetFiles(logger.LogDirectory, pattern)).Order().ToList();
            var hasErrors = File.Exists(errorsPath);
            if (!hasErrors && rolling.Count == 0 && searches.Count == 0)
                return null;

            var path = Path.Combine(FileSystem.CacheDirectory, $"{options.AppName}-logs-{DateTime.Now:yyyyMMdd-HHmm}.txt");
            foreach (var old in Directory.GetFiles(FileSystem.CacheDirectory, $"{options.AppName}-logs-*.txt"))
                File.Delete(old);

            using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine($"{options.AppName} {AppInfo.Current.VersionString}");
            writer.WriteLine($"OS: {DeviceInfo.Platform} {DeviceInfo.VersionString}");
            writer.WriteLine($"Device: {DeviceInfo.Manufacturer} {DeviceInfo.Model}");
            writer.WriteLine($"Session: {logger.SessionId}");
            writer.WriteLine($"Exported: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");

            if (hasErrors)
                AppendWhole(writer, "errors.log", errorsPath);

            AppendRollingTails(writer, rolling);
            foreach (var search in searches)
                AppendWhole(writer, Path.GetFileName(search), search);

            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void AppendWhole(StreamWriter writer, string title, string source)
    {
        writer.WriteLine();
        writer.WriteLine($"===== {title} =====");
        using var reader = new StreamReader(new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
        writer.Write(reader.ReadToEnd());
    }

    // Newest files fill the budget first; written back oldest to newest.
    private static void AppendRollingTails(StreamWriter writer, List<string> oldestFirst)
    {
        var remaining = RollingExportBytes;
        var picked = new List<(string Path, long Take)>();
        for (var i = oldestFirst.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var take = Math.Min(new FileInfo(oldestFirst[i]).Length, remaining);
            picked.Add((oldestFirst[i], take));
            remaining -= take;
        }

        for (var i = picked.Count - 1; i >= 0; i--)
        {
            var (source, take) = picked[i];
            writer.WriteLine();
            writer.WriteLine($"===== {Path.GetFileName(source)} =====");
            foreach (var line in ReadTailLines(source, take))
                writer.WriteLine(line);
        }
    }
}
