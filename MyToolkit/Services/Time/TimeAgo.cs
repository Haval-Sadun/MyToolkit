namespace MyToolkit.Services.Time;

/// <summary>
/// Centralized relative-time formatting. Single source of truth for the verbose
/// "x minutes ago" strings used across an app (notifications, comments, etc.).
/// </summary>
public static class TimeAgo
{
    public static string Relative(DateTime utc)
    {
        var diff = DateTime.UtcNow - utc.ToUniversalTime();

        if (diff.TotalSeconds < 60) return "just now";
        if (diff.TotalMinutes < 60)
        {
            var m = (int)diff.TotalMinutes;
            return $"{m} minute{(m == 1 ? "" : "s")} ago";
        }
        if (diff.TotalHours < 24)
        {
            var h = (int)diff.TotalHours;
            return $"{h} hour{(h == 1 ? "" : "s")} ago";
        }
        if (diff.TotalDays < 2) return "Yesterday";
        if (diff.TotalDays < 7)
        {
            var d = (int)diff.TotalDays;
            return $"{d} days ago";
        }
        return utc.ToLocalTime().ToString("MMM d");
    }
}
