using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace MyToolkit.Storage;

/// <summary>Keeps downloaded images as files named after their URL, so an image about to be shown (the next song's cover) is already on disk when it is needed. The oldest files go first once the folder passes its size cap. Platform-free; <see cref="PrefetchAsync"/> never throws.</summary>
public sealed class ImageDiskCache(HttpClient http, string folder, long maxBytes, Action<string>? warn = null, Func<string, Task>? shrink = null)
{
    private const long MaxImageBytes = 8 * 1024 * 1024;
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TouchEvery = TimeSpan.FromHours(1);

    // A list switch asks for dozens of covers at once; fetching and shrinking a few at a time keeps the memory flat.
    private const int MaxParallel = 4;

    private readonly ConcurrentDictionary<string, Task> _downloads = new();
    private readonly SemaphoreSlim _gate = new(MaxParallel);
    private readonly object _trimLock = new();

    /// <summary>The local file for <paramref name="url"/> when it was downloaded before, else null.</summary>
    public string? TryGetPath(string url)
    {
        if (!IsHttp(url))
            return null;

        var path = PathFor(url);
        try
        {
            if (!File.Exists(path))
                return null;

            // A file in use stays young, so trimming drops what was not shown for longest; touching once an hour is enough for that.
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > TouchEvery)
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow);

            return path;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Downloads <paramref name="url"/> unless it is on disk already or being fetched. Anything wrong (offline, not an image, too big) leaves no file behind.</summary>
    public Task PrefetchAsync(string url)
    {
        if (!IsHttp(url))
            return Task.CompletedTask;

        var path = PathFor(url);
        if (File.Exists(path))
            return Task.CompletedTask;

        // Registered before the download starts: one that fails at once would otherwise remove its entry before it was added, and the finished task would stay for good.
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = _downloads.GetOrAdd(path, done.Task);
        if (!ReferenceEquals(running, done.Task))
            return running;

        _ = RunAsync();
        return done.Task;

        async Task RunAsync()
        {
            try
            {
                await _gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    await DownloadAsync(url, path).ConfigureAwait(false);
                }
                finally
                {
                    _gate.Release();
                }
            }
            catch (Exception ex)
            {
                warn?.Invoke($"image cache: could not fetch a cover ({ex.GetType().Name}).");
            }
            finally
            {
                done.TrySetResult();
            }
        }
    }

    private async Task DownloadAsync(string url, string path)
    {
        var temp = path + ".part";
        try
        {
            Directory.CreateDirectory(folder);
            using var timeout = new CancellationTokenSource(DownloadTimeout);
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            var isImage = response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
            if (!response.IsSuccessStatusCode || !isImage || response.Content.Headers.ContentLength > MaxImageBytes)
                return;

            await using var source = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            long total = 0;
            await using (var file = File.Create(temp))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await source.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) > 0)
                {
                    total += read;
                    if (total > MaxImageBytes)
                        return;

                    await file.WriteAsync(buffer.AsMemory(0, read), timeout.Token).ConfigureAwait(false);
                }
            }

            await ShrinkAsync(temp).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
            Trim();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException or UnauthorizedAccessException)
        {
            // Offline, a slow server or a full disk is no reason to disturb playback; the image just loads from the network when shown.
            warn?.Invoke($"image cache: could not fetch a cover ({ex.GetType().Name}).");
        }
        finally
        {
            TryDelete(temp);
            _downloads.TryRemove(path, out _);
        }
    }

    // A cover decodes at full size on some platforms: the hook shrinks the file in place; a failure keeps the original.
    private async Task ShrinkAsync(string file)
    {
        if (shrink is null)
            return;

        try
        {
            await shrink(file).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            warn?.Invoke($"image cache: could not shrink a cover ({ex.GetType().Name}).");
        }
    }

    private void Trim()
    {
        lock (_trimLock)
        {
            var files = new DirectoryInfo(folder).GetFiles("*.jpg").OrderBy(file => file.LastWriteTimeUtc).ToList();
            var total = files.Sum(file => file.Length);
            foreach (var file in files)
            {
                if (total <= maxBytes)
                    break;

                total -= file.Length;
                TryDelete(file.FullName);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A file still in use is left for the next trim.
        }
    }

    private static bool IsHttp(string url) => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

    private string PathFor(string url) => Path.Combine(folder, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))) + ".jpg");
}
