// Keeps Debug.WriteLine in Release builds — [Conditional("DEBUG")] strips it from the published package otherwise.
#define DEBUG
using MyToolkit.Logging;
using MyToolkit.Services.Errors;

namespace MyToolkit.Services;

/// <summary>
/// Rolling file logger: <c>{AppDataDirectory}/logs/app.log</c> (1 MB x 3, INFO/WARN/ERROR) plus a persistent
/// <c>errors.log</c> with full error blocks. Writes happen on a background consumer; never throws.
/// </summary>
public class AppLogger : IDisposable
{
    private const int BreadcrumbCapacity = 100;
    private const int ErrorBreadcrumbCount = 50;

    private readonly string _logDir;
    private readonly string _appVersion;
    private readonly SizeRotatingFile _rolling;
    private readonly ErrorLogFile _errors;
    private readonly ErrorRepeatGuard _guard;
    private readonly LogWriter _writer;

    public AppLogger()
    {
        _logDir = Path.Combine(FileSystem.AppDataDirectory, "logs");
        SessionId = LogSession.NewId();
        _appVersion = Safe(() => AppInfo.Current.VersionString);
        Func<DateTimeOffset> now = () => DateTimeOffset.Now;

        _rolling = new SizeRotatingFile(_logDir, "app", 1024 * 1024, 3);
        _errors = new ErrorLogFile(Path.Combine(_logDir, "errors.log"), 512 * 1024, TimeSpan.FromDays(30), now);
        _guard = new ErrorRepeatGuard(TimeSpan.FromMinutes(1), now);
        _writer = new LogWriter(_rolling, _errors, new BreadcrumbBuffer(BreadcrumbCapacity), SessionId, _appVersion, now);

        _writer.PersistentWarnPrefixes = ["db.", "catalogue.parse.suspect", "app.exit"];
        DeleteLegacyFiles();
        var os = $"{Safe(() => DeviceInfo.Current.Platform.ToString())} {Safe(() => DeviceInfo.Current.VersionString)}";
        var device = $"{Safe(() => DeviceInfo.Current.Manufacturer)} {Safe(() => DeviceInfo.Current.Model)}";
        _writer.WriteHeader(LogEntryFormatter.FormatSessionHeader(now(), SessionId, _appVersion, os, device));
    }

    public string LogDirectory => _logDir;

    /// <summary>Id shared by every entry of this launch.</summary>
    public string SessionId { get; }

    /// <summary>Path of the persistent error file.</summary>
    public string ErrorLogPath => _errors.FilePath;

    /// <summary>Existing rolling files, oldest first.</summary>
    public IReadOnlyList<string> RollingLogPaths => _rolling.Files;

    public void Log(string level, string message)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine(_writer.WriteLine(level, message));
        }
        catch
        {
            // Logging must never throw.
        }
    }

    /// <summary>Blocks until queued entries are on disk (default 2 s); use before a crash exit or app pause.</summary>
    public bool Flush(TimeSpan? timeout = null) => _writer.Flush(timeout ?? TimeSpan.FromSeconds(2));

    public void Dispose()
    {
        _writer.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Logs an informational message (level <c>INFO</c>).</summary>
    public void LogInfo(string message) => Log("INFO", message);

    /// <summary>Logs a warning message (level <c>WARN</c>).</summary>
    public void LogWarning(string message) => Log("WARN", message);

    /// <summary>Logs an exception: a full block (stack, inner exceptions, recent lines) to errors.log, deduplicated per minute.</summary>
    public void LogException(Exception ex, string context)
    {
        try
        {
            var summary = $"{context}: {ex.GetType().Name}: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[ERROR] {summary}");
            var key = $"{EventName(context)}|{ex.GetType().FullName}|{FirstLine(ex.Message)}";
            var decision = _guard.Check(key);
            if (!decision.Write)
            {
                _writer.WriteLine("ERROR", $"(repeat suppressed) {summary}");
                return;
            }

            var crumbs = _writer.Breadcrumbs.Snapshot();
            if (crumbs.Count > ErrorBreadcrumbCount)
                crumbs = crumbs.Skip(crumbs.Count - ErrorBreadcrumbCount).ToList();
            var block = LogEntryFormatter.FormatErrorBlock(DateTimeOffset.Now, SessionId, _appVersion, context, ex, crumbs, decision.SuppressedBefore);
            _writer.WriteError(block, summary);
        }
        catch
        {
            // Logging must never throw.
        }
    }

    /// <summary>Logs a backend API error envelope (level <c>WARN</c>).</summary>
    public void LogApiError(IApiError error, string context)
        => Log("WARN", $"{context}: [{error.StatusCode}] {error.ErrorCode ?? "ERROR"}: {error.Message}");

    // "name key=value ..." contexts differ per occurrence; only the name identifies the failing place.
    private static string EventName(string context)
    {
        var equals = context.IndexOf('=');
        if (equals < 0)
            return context;

        var cut = context.LastIndexOf(' ', equals);
        return cut < 0 ? context : context[..cut];
    }

    private static string FirstLine(string text)
    {
        var end = text.AsSpan().IndexOfAny('\r', '\n');
        return end < 0 ? text : text[..end];
    }

    private static string Safe(Func<string?> read)
    {
        try
        {
            return read() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    // Old per-day files (app-yyyyMMdd.log) are replaced by the size-rotated set.
    private void DeleteLegacyFiles()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_logDir, "app-????????.log"))
                File.Delete(file);
        }
        catch
        {
            // Best effort.
        }
    }
}
