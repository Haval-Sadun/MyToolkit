using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace MyToolkit.Controls.Sheets;

/// <summary>One row of the toggle sheet. Carries its own command so the template binds without reaching up to the sheet's view-model.</summary>
public sealed class ToggleRow : SheetRowModel
{
    public ToggleRow(ToggleOptionBase option, Func<ToggleRow, Task> toggle) : base(option.Title, option.Detail, option.Badge, option.BadgeIsIcon, option.IsSelected) =>
        ToggleCommand = new AsyncRelayCommand(() => toggle(this));

    public ICommand ToggleCommand { get; }
}

/// <summary>The toggle sheet: every tap flips a row at once through the caller's callback, which gets the row's index. "New" closes the sheet with <see cref="ToggleExit.NewRequested"/>, "Done" with <see cref="ToggleExit.Closed"/>.</summary>
public sealed class ToggleSheetViewModel
{
    private readonly Func<int, bool, Task<ToggleResult?>> _toggle;
    private bool _busy;

    public ToggleSheetViewModel(string? subtitle, IReadOnlyList<ToggleOptionBase> options, Func<int, bool, Task<ToggleResult?>> toggle, string? newText, string? doneText, Action<ToggleExit> close)
    {
        _toggle = toggle;
        Subtitle = subtitle ?? string.Empty;
        NewText = newText ?? string.Empty;
        DoneText = doneText ?? string.Empty;
        Rows = options.Select(option => new ToggleRow(option, ToggleAsync)).ToList();
        NewCommand = new RelayCommand(() => close(ToggleExit.NewRequested));
        DoneCommand = new RelayCommand(() => close(ToggleExit.Closed));
    }

    public string Subtitle { get; }

    public bool HasSubtitle => Subtitle.Length > 0;

    public bool HasHeader => HasSubtitle || HasNew;

    public string NewText { get; }

    public bool HasNew => NewText.Length > 0;

    public string DoneText { get; }

    public bool HasDone => DoneText.Length > 0;

    public IReadOnlyList<ToggleRow> Rows { get; }

    public ICommand NewCommand { get; }

    public ICommand DoneCommand { get; }

    private async Task ToggleAsync(ToggleRow row)
    {
        // One at a time: a second tap while the first saves would flip against stale state.
        if (_busy)
            return;

        _busy = true;
        try
        {
            var index = Rows.ToList().IndexOf(row);
            if (index < 0 || await _toggle(index, row.IsSelected) is not { } result)
                return;

            row.IsSelected = result.IsSelected;
            if (result.Detail is { } detail)
                row.Detail = detail;
        }
        finally
        {
            _busy = false;
        }
    }
}
