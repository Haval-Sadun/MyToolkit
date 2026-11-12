namespace MyToolkit.Services.Net;

/// <summary>What the network allows right now — an interface so decisions based on it can be faked in tests.</summary>
public interface INetworkStatus
{
    /// <summary>False only when the OS is sure there's no internet. Unknown or captive-portal counts as online — a wrong "offline" blocks work that would have succeeded.</summary>
    bool IsOnline { get; }

    /// <summary>On Wi-Fi or Ethernet. False when unsure — meant to gate optional downloads, where guessing "metered" only costs skipping one.</summary>
    bool IsUnmetered { get; }
}
