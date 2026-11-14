namespace MyToolkit.Logging;

/// <summary>One ERROR block of errors.log, reduced to what the Diagnostics list shows.</summary>
public sealed record ErrorLogEntry(string Time, string Context, string Summary);

/// <summary>Pure parser for errors.log: blocks start with <c>=== ERROR {time} session=... ===</c>, then <c>context:</c> and <c>exception:</c> lines.</summary>
public static class ErrorLogParser
{
    private const string ErrorHeader = "=== ERROR ";

    /// <summary>The last <paramref name="count"/> ERROR entries, newest first. Anything that isn't an ERROR block (WARN blocks, a cut-off start) is skipped.</summary>
    public static IReadOnlyList<ErrorLogEntry> ParseRecent(IEnumerable<string> lines, int count)
    {
        var entries = new List<ErrorLogEntry>();
        string? time = null;
        string? context = null;
        string? summary = null;

        void Flush()
        {
            if (time is not null)
                entries.Add(new ErrorLogEntry(time, context ?? "-", summary ?? "-"));

            time = null;
            context = null;
            summary = null;
        }

        foreach (var line in lines)
        {
            if (line.StartsWith(ErrorHeader, StringComparison.Ordinal))
            {
                Flush();
                var rest = line[ErrorHeader.Length..];
                var end = rest.IndexOf(" session=", StringComparison.Ordinal);
                time = (end < 0 ? rest.TrimEnd(' ', '=') : rest[..end]).Trim();
            }
            else if (line.StartsWith("=== ", StringComparison.Ordinal))
                Flush();
            else if (time is null)
                continue;
            else if (context is null && line.StartsWith("context: ", StringComparison.Ordinal))
                context = line["context: ".Length..].Trim();
            else if (summary is null && line.StartsWith("exception: ", StringComparison.Ordinal))
                summary = line["exception: ".Length..].Trim();
        }

        Flush();
        entries.Reverse();
        return entries.Count > count ? entries.GetRange(0, count) : entries;
    }
}
