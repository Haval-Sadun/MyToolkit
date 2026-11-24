using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace MyToolkit.Controls.Sheets;

/// <summary>One row of a choice sheet. Carries its own command so the template binds without reaching up to the sheet's view-model; tapping closes the sheet with <see cref="Value"/>.</summary>
public sealed class ChoiceRow : SheetRowModel
{
    public ChoiceRow(string value, string title, string? detail, string? badge, bool badgeIsIcon, bool isSelected, Action<string> close) : base(title, detail, badge, badgeIsIcon, isSelected)
    {
        Value = value;
        Command = new RelayCommand(() => close(value));
    }

    public string Value { get; }

    public ICommand Command { get; }
}

/// <summary>A pick-one sheet: cards (or chips) with a title, an optional detail line and badge, a radio on the right and an optional note at the bottom. Tapping one closes the sheet with its value.</summary>
public sealed class ChoiceSheetViewModel(string? subtitle, IReadOnlyList<ChoiceRow> rows, string? note)
{
    public string Subtitle { get; } = subtitle ?? string.Empty;

    public bool HasSubtitle => Subtitle.Length > 0;

    public IReadOnlyList<ChoiceRow> Rows { get; } = rows;

    public string Note { get; } = note ?? string.Empty;

    public bool HasNote => Note.Length > 0;
}
