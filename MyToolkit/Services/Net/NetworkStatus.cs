namespace MyToolkit.Services.Net;

/// <summary>MAUI <see cref="Connectivity"/> behind <see cref="INetworkStatus"/>; never throws.</summary>
public sealed class NetworkStatus(AppLogger logger) : INetworkStatus
{
    private int _loggedFailure;

    public bool IsOnline
    {
        get
        {
            try
            {
                return Connectivity.Current.NetworkAccess is not (NetworkAccess.None or NetworkAccess.Local);
            }
            catch (Exception ex)
            {
                LogOnce(ex);
                return true;
            }
        }
    }

    public bool IsUnmetered
    {
        get
        {
            try
            {
                return Connectivity.Current.ConnectionProfiles.Any(profile => profile is ConnectionProfile.WiFi or ConnectionProfile.Ethernet);
            }
            catch (Exception ex)
            {
                LogOnce(ex);
                return false;
            }
        }
    }

    // Once per launch: a platform that can't report connectivity would otherwise log on every check.
    private void LogOnce(Exception ex)
    {
        if (Interlocked.Exchange(ref _loggedFailure, 1) == 0)
            logger.LogException(ex, "network status unavailable");
    }
}
