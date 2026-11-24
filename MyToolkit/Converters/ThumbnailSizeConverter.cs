using System.Globalization;
using System.Text.RegularExpressions;
using MyToolkit.Services;
using MyToolkit.Storage;

namespace MyToolkit.Converters;

/// <summary>Rewrites a YouTube or Google image URL to the size it is drawn at (ConverterParameter, in dp), so small rows don't download full-size artwork. An image already on disk (see <see cref="ImageDiskCache"/>) comes from there.</summary>
public sealed partial class ThumbnailSizeConverter : IValueConverter
{
    private static readonly double Density = Math.Max(1, DeviceDisplay.MainDisplayInfo.Density);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string url || string.IsNullOrEmpty(url))
            return null;

        var sized = double.TryParse(parameter?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dp) ? SizedFor(url, dp) : url;

        // A cover fetched ahead of time (the next song's) comes from disk, so it shows on the first frame.
        return (ImageSource)(ServiceHelper.GetSafe<ImageDiskCache>()?.TryGetPath(sized) ?? sized);
    }

    /// <summary>The exact URL an image drawn <paramref name="dp"/> wide requests: what must be prefetched for the cache to hit.</summary>
    public static string SizedFor(string url, double dp) => Resize(url, (int)Math.Ceiling(dp * Density));

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();

    /// <summary>googleusercontent/ggpht URLs take a size suffix (=w544-h544 or =s1200); i.ytimg.com has fixed variants. Pure, so it is unit-tested.</summary>
    public static string Resize(string url, int px)
    {
        if (url.Contains("googleusercontent.com", StringComparison.Ordinal) || url.Contains("ggpht.com", StringComparison.Ordinal))
            return SizeSuffix().Replace(url, m => m.Groups["w"].Success ? $"=w{px}-h{px}" : $"=s{px}", 1);

        if (px <= 320 && url.Contains("i.ytimg.com/vi/", StringComparison.Ordinal))
            return YtimgVariant().Replace(url, "/mqdefault.jpg");

        return url;
    }

    [GeneratedRegex(@"=(?:(?<w>w\d+-h\d+)|s\d+)")]
    private static partial Regex SizeSuffix();

    [GeneratedRegex(@"/(?:maxresdefault|sddefault|hq720|hqdefault)\.jpg(?:\?.*)?$")]
    private static partial Regex YtimgVariant();
}
