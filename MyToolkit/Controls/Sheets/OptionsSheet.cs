using MyToolkit.Services.Popups;

namespace MyToolkit.Controls.Sheets;

/// <summary>Shows the item options sheet (song, station, book, folder, playlist): the item, a main action, a row of quick tiles and grouped rows. One place, so every item menu looks and behaves alike.</summary>
public static class OptionsSheet
{
    /// <summary>An action that is async.</summary>
    public static PopupOption<Func<Task>> Item(string text, Func<Task> run, string? icon, bool isDestructive = false) => new(text, run, icon, IsDestructive: isDestructive);

    /// <summary>An action that finishes at once (a store call).</summary>
    public static PopupOption<Func<Task>> ItemAction(string text, Action run, string? icon, bool isDestructive = false) => new(text, () =>
    {
        run();
        return Task.CompletedTask;
    }, icon, IsDestructive: isDestructive);

    /// <summary>Empty groups are left out; the tapped action runs once the sheet has closed. Dismissing it does nothing.</summary>
    public static async Task ShowAsync(IPopupPresenter popups, string title, string subtitle, string? thumbnailUrl, string placeholderGlyph, PopupOption<Func<Task>>? play, IReadOnlyList<PopupOption<Func<Task>>> quick, IReadOnlyList<IReadOnlyList<PopupOption<Func<Task>>>> groups)
    {
        var chosen = await popups.CustomSheetAsync<Func<Task>>(null, close =>
        {
            OptionsEntry Entry(PopupOption<Func<Task>> option, bool showDivider = false) => new(option.Text, option.Icon, option.Value, option.IsDestructive, showDivider, close);

            var cards = groups
                .Where(group => group.Count > 0)
                .Select(group => new OptionsGroup(group.Select((option, index) => Entry(option, showDivider: index > 0)).ToList()))
                .ToList();
            return new OptionsSheetView(new OptionsSheetViewModel(title, subtitle, thumbnailUrl, placeholderGlyph, play is null ? null : Entry(play), quick.Select(option => Entry(option)).ToList(), cards));
        });

        if (chosen is not null)
            await chosen();
    }
}
