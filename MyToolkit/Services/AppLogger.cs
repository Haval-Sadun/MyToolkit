using System.Text;
using MyToolkit.Services.Errors;

namespace MyToolkit.Services;

/// <summary>
/// Lightweight rolling file logger. Writes to
/// <c>{AppDataDirectory}/logs/app-yyyyMMdd.log</c> (one file per day) and mirrors
/// to the debug output. Thread-safe and never throws — logging must not be able
/// to crash the app.
/// </summary>
public class AppLogger
{
    private readonly string _logDir;
    private readonly object _gate = new();

    public AppLogger()
    {
        _logDir = Path.Combine(FileSystem.AppDataDirectory, "logs");
        try { Directory.CreateDirectory(_logDir); } catch { }
    }

    public string LogDirectory => _logDir;

    public void Log(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        System.Diagnostics.Debug.WriteLine(line);
        try
        {
            var file = Path.Combine(_logDir, $"app-{DateTime.Now:yyyyMMdd}.log");
            lock (_gate)
            {
                File.AppendAllText(file, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch { /* swallow: logging must never throw */ }
    }

    // ── Convenience overloads ───────────────────────────────────────────────
    // Generic, app-agnostic shorthands over Log(level, message) so callers don't
    // repeat the level string or hand-format common shapes.

    /// <summary>Logs an informational message (level <c>INFO</c>).</summary>
    public void LogInfo(string message) => Log("INFO", message);

    /// <summary>Logs a warning message (level <c>WARN</c>).</summary>
    public void LogWarning(string message) => Log("WARN", message);

    /// <summary>Logs an exception with its context, type, message and stack trace (level <c>ERROR</c>).</summary>
    public void LogException(Exception ex, string context)
        => Log("ERROR", $"{context}: {ex.GetType().Name}: {ex.Message}\n  {ex.StackTrace}");

    /// <summary>Logs a backend API error envelope (level <c>WARN</c>).</summary>
    public void LogApiError(IApiError error, string context)
        => Log("WARN", $"{context}: [{error.StatusCode}] {error.ErrorCode ?? "ERROR"}: {error.Message}");
}
