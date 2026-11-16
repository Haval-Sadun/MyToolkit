namespace MyToolkit.Services;

/// <summary>The running app's version for logs and exports. AppInfo can report a stale number (on Windows it reads a generated manifest that an incremental build may leave behind), so an app can supply a better source.</summary>
public static class AppVersionSource
{
    /// <summary>Set once at startup, before the logger is created.</summary>
    public static Func<string?>? Override { get; set; }

    public static string Current
    {
        get
        {
            try
            {
                return Override?.Invoke() ?? AppInfo.Current.VersionString;
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
