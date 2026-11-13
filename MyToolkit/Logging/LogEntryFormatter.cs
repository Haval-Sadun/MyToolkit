using System.Globalization;
using System.Text;

namespace MyToolkit.Logging;

/// <summary>Pure text formatting for log lines and entry blocks.</summary>
public static class LogEntryFormatter
{
    /// <summary>Block terminator line.</summary>
    public const string EndMarker = "=== end ===";

    /// <summary>One rolling-log line in local time.</summary>
    public static string FormatLine(DateTimeOffset time, string level, string message) => $"{Stamp(time)} [{level}] {message}";

    /// <summary>One-per-launch header line.</summary>
    public static string FormatSessionHeader(DateTimeOffset time, string sessionId, string appVersion, string os, string device) => $"=== SESSION {Stamp(time)} session={sessionId} app={appVersion} os={os} device={device} ===";

    /// <summary>Full error block with stack, inner exceptions and recent breadcrumbs.</summary>
    public static string FormatErrorBlock(DateTimeOffset time, string sessionId, string appVersion, string context, Exception ex, IReadOnlyList<string> breadcrumbs, int suppressedBefore)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== ERROR {Stamp(time)} session={sessionId} app={appVersion} ===");
        sb.AppendLine($"context: {context}");
        sb.AppendLine($"exception: {ex.GetType().FullName}: {ex.Message}");
        sb.AppendLine(ex.ToString());
        if (suppressedBefore > 0)
            sb.AppendLine($"suppressed_identical_before: {suppressedBefore}");
        sb.AppendLine($"recent log (last {breadcrumbs.Count} lines, oldest first):");
        foreach (var line in breadcrumbs)
            sb.AppendLine($"  {line}");
        sb.AppendLine(EndMarker);
        return sb.ToString();
    }

    /// <summary>Short block for a high-value WARN line.</summary>
    public static string FormatWarnBlock(DateTimeOffset time, string sessionId, string appVersion, string line)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== WARN {Stamp(time)} session={sessionId} app={appVersion} ===");
        sb.AppendLine(line);
        sb.AppendLine(EndMarker);
        return sb.ToString();
    }

    private static string Stamp(DateTimeOffset time) => time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
}
