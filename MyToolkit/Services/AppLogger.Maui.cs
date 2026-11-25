#if MAUI_PLATFORM
namespace MyToolkit.Services;

public partial class AppLogger
{
    /// <summary>Logs under <c>{AppDataDirectory}/logs</c>, with this device in the session header.</summary>
    public AppLogger() : this(Path.Combine(FileSystem.AppDataDirectory, "logs"), $"{Safe(() => DeviceInfo.Current.Platform.ToString())} {Safe(() => DeviceInfo.Current.VersionString)}", $"{Safe(() => DeviceInfo.Current.Manufacturer)} {Safe(() => DeviceInfo.Current.Model)}")
    {
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
}
#endif
