using System.Globalization;
using MyToolkit.Services.Popups;

namespace MyToolkit.Controls.Sheets;

/// <summary>One choice of <see cref="ChoiceSheet"/>.</summary>
public sealed record ChoiceOption<T>(T Value, string Title, string? Detail = null, string? Badge = null, bool BadgeIsIcon = false);

/// <summary>Shows a pick-one sheet (language, fade, download limit...): a card per option, the current one marked. One place, so every such picker looks and behaves alike.</summary>
public static class ChoiceSheet
{
    /// <summary>The tapped option, or null when the sheet was dismissed without a choice. <paramref name="chips"/> shows short choices as chips instead of cards.</summary>
    public static async Task<ChoiceOption<T>?> ShowAsync<T>(IPopupPresenter popups, string title, IReadOnlyList<ChoiceOption<T>> options, T current, string? subtitle = null, string? note = null, bool chips = false)
    {
        var picked = await popups.CustomSheetAsync<string>(title, close =>
        {
            var rows = options
                .Select((option, index) => new ChoiceRow(index.ToString(CultureInfo.InvariantCulture), option.Title, option.Detail, option.Badge, option.BadgeIsIcon, EqualityComparer<T>.Default.Equals(option.Value, current), close))
                .ToList();
            var viewModel = new ChoiceSheetViewModel(subtitle, rows, note);
            return chips ? (View)new ChipSheetView(viewModel) : new ChoiceSheetView(viewModel);
        });

        return int.TryParse(picked, NumberStyles.None, CultureInfo.InvariantCulture, out var chosen) && chosen < options.Count ? options[chosen] : null;
    }
}
