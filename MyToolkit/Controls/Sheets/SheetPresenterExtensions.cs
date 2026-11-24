using System.Globalization;
using MyToolkit.Services.Popups;

namespace MyToolkit.Controls.Sheets;

/// <summary>The one door to the bottom sheets: extension methods on the <see cref="IPopupPresenter"/> an app already injects, so every sheet is found in IntelliSense on it and faked with it in tests.
/// Every method that asks the user for something returns what was chosen, or null/<see cref="ToggleExit.Closed"/> when the sheet was dismissed; nothing runs behind the caller's back except <see cref="RunActionAsync"/>, which says so.</summary>
public static class SheetPresenterExtensions
{
    /// <summary>Pick one of a few choices (language, a limit, a speed): the current one is marked. Returns the tapped choice, or null when dismissed.</summary>
    public static async Task<ChoiceOption<T>?> ChooseAsync<T>(this IPopupPresenter popups, string title, IReadOnlyList<ChoiceOption<T>> options, T current, string? subtitle = null, string? note = null, ChoiceStyle style = ChoiceStyle.Cards)
    {
        var picked = await popups.CustomSheetAsync<string>(title, close =>
        {
            var rows = options
                .Select((option, index) => new ChoiceRow(index.ToString(CultureInfo.InvariantCulture), option.Title, option.Detail, option.Badge, option.BadgeIsIcon, EqualityComparer<T>.Default.Equals(option.Value, current), close))
                .ToList();
            var viewModel = new ChoiceSheetViewModel(subtitle, rows, note);
            return style == ChoiceStyle.Chips ? (View)new ChipSheetView(viewModel) : new ChoiceSheetView(viewModel);
        });

        return int.TryParse(picked, NumberStyles.None, CultureInfo.InvariantCulture, out var chosen) && chosen < options.Count ? options[chosen] : null;
    }

    /// <summary>Flip items in or out of a set (a song in playlists): every tap saves at once through <paramref name="toggle"/> and the row follows. The sheet decides nothing itself.</summary>
    /// <param name="toggle">Saves the flip of the row with this value (its state before the tap is the second argument); returns the new state, or null when it failed and the row must stay as it was.</param>
    /// <param name="newText">Shows a "New" pill with this text that closes the sheet with <see cref="ToggleExit.NewRequested"/>, so the caller can ask for a name; null hides it.</param>
    /// <param name="doneText">Shows a pinned button with this text that closes the sheet; null hides it (the sheet still closes by tapping outside).</param>
    public static async Task<ToggleExit> ToggleAsync<T>(this IPopupPresenter popups, string title, IReadOnlyList<ToggleOption<T>> options, Func<T, bool, Task<ToggleResult?>> toggle, string? subtitle = null, string? newText = null, string? doneText = null) =>
        await popups.CustomSheetAsync<ToggleExit>(title, close => new ToggleSheetView(new ToggleSheetViewModel(subtitle, options, (index, isSelected) => toggle(options[index].Value, isSelected), newText, doneText, close)));

    /// <summary>An item's menu (a song, a book, a folder): the item, a main action, a row of quick tiles and grouped rows; empty groups are left out. Returns the tapped action, not yet run, or null when dismissed.</summary>
    public static async Task<Func<Task>?> PickActionAsync(this IPopupPresenter popups, string title, string subtitle, string? thumbnailUrl, string placeholderGlyph, PopupOption<Func<Task>>? main, IReadOnlyList<PopupOption<Func<Task>>> quick, IReadOnlyList<IReadOnlyList<PopupOption<Func<Task>>>> groups) =>
        await popups.CustomSheetAsync<Func<Task>>(null, close =>
        {
            OptionsEntry Entry(PopupOption<Func<Task>> option, bool showDivider = false) => new(option.Text, option.Icon, option.Value, option.IsDestructive, showDivider, close);

            var cards = groups
                .Where(group => group.Count > 0)
                .Select(group => new OptionsGroup(group.Select((option, index) => Entry(option, showDivider: index > 0)).ToList()))
                .ToList();
            return new OptionsSheetView(new OptionsSheetViewModel(title, subtitle, thumbnailUrl, placeholderGlyph, main is null ? null : Entry(main), quick.Select(option => Entry(option)).ToList(), cards));
        });

    /// <summary><see cref="PickActionAsync"/>, then runs the tapped action once the sheet has closed: the usual way to show an item menu. Dismissing it does nothing.</summary>
    public static async Task RunActionAsync(this IPopupPresenter popups, string title, string subtitle, string? thumbnailUrl, string placeholderGlyph, PopupOption<Func<Task>>? main, IReadOnlyList<PopupOption<Func<Task>>> quick, IReadOnlyList<IReadOnlyList<PopupOption<Func<Task>>>> groups)
    {
        if (await popups.PickActionAsync(title, subtitle, thumbnailUrl, placeholderGlyph, main, quick, groups) is { } chosen)
            await chosen();
    }
}
