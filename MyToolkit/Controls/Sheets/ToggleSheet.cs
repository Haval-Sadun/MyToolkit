using MyToolkit.Services.Popups;

namespace MyToolkit.Controls.Sheets;

/// <summary>One row of <see cref="ToggleSheet"/>: an item that is in or out (a playlist the song is in, a tag that is set).</summary>
/// <param name="Id">What the sheet hands back to the toggle callback for this row.</param>
/// <param name="Badge">A short mark in a tile at the start of the row: letters, or a glyph when <paramref name="BadgeIsIcon"/>.</param>
public sealed record ToggleOption(string Id, string Title, string? Detail, bool IsSelected, string? Badge = null, bool BadgeIsIcon = false);

/// <summary>What a toggle did: the row's new state, and its new detail line (null keeps the old one).</summary>
public sealed record ToggleResult(bool IsSelected, string? Detail = null);

/// <summary>Shows a many-of-many sheet: every tap flips one row at once, saved by the caller's callback; the sheet itself decides nothing. An optional "New" pill and a "Done" button sit at the top and bottom.</summary>
public static class ToggleSheet
{
    /// <param name="toggle">Saves the flip of the row with this id (its state before the tap is the second argument); returns the new state, or null when it failed and the row must stay as it was.</param>
    /// <param name="newText">Shows a "New" pill (with this text) that closes the sheet and makes this return true, so the caller can ask for a name; null hides it.</param>
    /// <param name="doneText">Shows a pinned button (with this text) that closes the sheet; null hides it (the sheet still closes by tapping outside).</param>
    /// <returns>True when "New" was tapped.</returns>
    public static Task<bool> ShowAsync(IPopupPresenter popups, string title, IReadOnlyList<ToggleOption> options, Func<string, bool, Task<ToggleResult?>> toggle, string? subtitle = null, string? newText = null, string? doneText = null) =>
        popups.CustomSheetAsync<bool>(title, close => new ToggleSheetView(new ToggleSheetViewModel(subtitle, options, toggle, newText, doneText, close)));
}
