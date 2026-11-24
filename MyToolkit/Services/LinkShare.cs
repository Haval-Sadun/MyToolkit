using MyToolkit.Services.Errors;

namespace MyToolkit.Services;

/// <summary>Opens the OS share sheet (WhatsApp, Messenger, ...) with a link: the target app decides how to render it.</summary>
public static class LinkShare
{
    /// <summary>A failure goes to <paramref name="errors"/>, named by the title that was being shared.</summary>
    public static async Task ShareAsync(string title, string url, ErrorHandler errors)
    {
        try
        {
            await Share.Default.RequestAsync(new ShareTextRequest { Title = title, Text = title, Uri = url });
        }
        catch (Exception ex)
        {
            await errors.HandleAsync(ex, $"share.failed title=\"{title}\"");
        }
    }
}
