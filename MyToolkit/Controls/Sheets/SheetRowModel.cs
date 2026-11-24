using CommunityToolkit.Mvvm.ComponentModel;

namespace MyToolkit.Controls.Sheets;

/// <summary>What every row of a pick or toggle sheet shows: a title, an optional detail line and badge, and whether it is chosen. The sheets differ only in what a tap does.</summary>
public abstract class SheetRowModel : ObservableObject
{
    private string _detail;
    private bool _isSelected;

    protected SheetRowModel(string title, string? detail, string? badge, bool badgeIsIcon, bool isSelected)
    {
        Title = title;
        _detail = detail ?? string.Empty;
        Badge = badge ?? string.Empty;
        BadgeIsIcon = badgeIsIcon;
        _isSelected = isSelected;
    }

    public string Title { get; }

    /// <summary>A short mark (letters, or a glyph when <see cref="BadgeIsIcon"/>) in a tile at the start of the row.</summary>
    public string Badge { get; }

    public bool HasBadge => Badge.Length > 0;

    public bool BadgeIsIcon { get; }

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

    /// <summary>The tick on the right (a radio in a pick sheet, a check in a toggle sheet).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
