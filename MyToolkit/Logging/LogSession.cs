namespace MyToolkit.Logging;

/// <summary>Per-launch session identity.</summary>
public static class LogSession
{
    /// <summary>Returns a new 8-hex-char id.</summary>
    public static string NewId() => Guid.NewGuid().ToString("N")[..8];
}
