namespace MyToolkit.Controls;

/// <summary>Takes a picture of a view on screen and hands it to the system share sheet — used for share cards and posters.</summary>
public static class ViewCapture
{
    /// <summary>False when the view could not be photographed (not on screen, or the platform refused). Throws what the share sheet throws.</summary>
    public static async Task<bool> ShareAsync(VisualElement view, string title, string fileName)
    {
        var shot = await view.CaptureAsync();
        if (shot is null)
            return false;

        var path = Path.Combine(FileSystem.CacheDirectory, fileName);
        await using (var png = await shot.OpenReadAsync(ScreenshotFormat.Png))
        await using (var file = File.Create(path))
            await png.CopyToAsync(file);

        await Share.Default.RequestAsync(new ShareFileRequest { Title = title, File = new ShareFile(path) });
        return true;
    }
}
