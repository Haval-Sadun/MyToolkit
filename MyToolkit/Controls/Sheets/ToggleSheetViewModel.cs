using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MyToolkit.Controls.Sheets;

/// <summary>One row of the toggle sheet. Carries its own command so the template binds without reaching up to the sheet's view-model.</summary>
public sealed class ToggleRow : ObservableObject
{
    private string _detail;
    private bool _isSelected;

    public ToggleRow(ToggleOption option, Func<ToggleRow, Task> toggle)
    {
        Id = option.Id;
        Title = option.Title;
        _detail = option.Detail ?? string.Empty;
        _isSelected = option.IsSelected;
        Badge = option.Badge ?? string.Empty;
        BadgeIsIcon = option.BadgeIsIcon;
        ToggleCommand = new AsyncRelayCommand(() => toggle(this));
    }

    public string Id { get; }

    public string Title { get; }

    public string Badge { get; }

    public bool HasBadge => Badge.Length > 0;

    public bool BadgeIsIcon { get; }

    public bool BadgeIsText => !BadgeIsIcon;

    public string Detail
    {
        get => _detail;
        set
        {
            if (SetProperty(ref _detail, value))
                OnPropertyChanged(nameof(HasDetail));
        }
    }

    public bool HasDetail => _detail.Length > 0;

    /// <summary>Whether the item is in: the check on the right.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public ICommand ToggleCommand { get; }
}

/// <summary>The toggle sheet: every tap flips a row at once through the caller's callback. "New" closes the sheet with true, "Done" with false.</summary>
public sealed class ToggleSheetViewModel
{
    private readonly Func<string, bool, Task<ToggleResult?>> _toggle;
    private readonly Action<bool> _close;
    private bool _busy;

    public ToggleSheetViewModel(string? subtitle, IReadOnlyList<ToggleOption> options, Func<string, bool, Task<ToggleResult?>> toggle, string? newText, string? doneText, Action<bool> close)
    {
        _toggle = toggle;
        _close = close;
        Subtitle = subtitle ?? string.Empty;
        NewText = newText ?? string.Empty;
        DoneText = doneText ?? string.Empty;
        Rows = options.Select(option => new ToggleRow(option, ToggleAsync)).ToList();
        NewCommand = new RelayCommand(() => _close(true));
        DoneCommand = new RelayCommand(() => _close(false));
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
            if (await _toggle(row.Id, row.IsSelected) is not { } result)
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
