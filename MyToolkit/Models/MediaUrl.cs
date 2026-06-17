namespace MyToolkit.Models;

/// <summary>
/// Prepends a configured base URL to relative image paths. Absolute URLs (starting with "http")
/// are returned unchanged. Configure once at app startup via <see cref="Configure"/>.
/// </summary>
public static class MediaUrl
{
    private static string _baseUrl = string.Empty;

    /// <summary>
    /// Call once in MauiProgram / App startup with the server root (e.g. "http://10.0.2.2:8000"
    /// for the Android emulator, or "https://myapp.com" in production).
    /// </summary>
    public static void Configure(string baseUrl) => _baseUrl = baseUrl.TrimEnd('/');

    /// <summary>Prepends <see cref="_baseUrl"/> when <paramref name="value"/> is a relative path.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{_baseUrl}{value}";
    }

    /// <summary>Normalizes a URL, returning <paramref name="fallback"/> when <paramref name="value"/> is null/empty.</summary>
    public static string NormalizeOrFallback(string? value, string fallback)
        => Normalize(value) ?? fallback;
}
