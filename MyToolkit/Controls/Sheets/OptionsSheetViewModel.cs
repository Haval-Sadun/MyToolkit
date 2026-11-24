using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using MyToolkit.Converters;

namespace MyToolkit.Controls.Sheets;

/// <summary>One action of an options sheet. Carries its own command so the template binds without reaching up to the sheet's view-model; tapping closes the sheet with the action, which the caller then runs.</summary>
public sealed class OptionsEntry
{
    public OptionsEntry(string text, string? icon, Func<Task> run, bool isDestructive, bool showDivider, Action<Func<Task>> close)
    {
        Text = text;
        Icon = icon ?? string.Empty;
        IsDestructive = isDestructive;
        ShowDivider = showDivider;
        Command = new RelayCommand(() => close(run));
    }

    public string Text { get; }

    public string Icon { get; }

    public bool IsDestructive { get; }

    /// <summary>Where the quick tile sits in the tile grid; set by the sheet's view-model.</summary>
    public int GridRow { get; set; }

    public int GridColumn { get; set; }

    /// <summary>A hairline above the row, except for the first of its group.</summary>
    public bool ShowDivider { get; }

    public ICommand Command { get; }
}

/// <summary>A rounded card of rows: list actions are grouped by what they do.</summary>
public sealed class OptionsGroup(IReadOnlyList<OptionsEntry> items)
{
    public IReadOnlyList<OptionsEntry> Items { get; } = items;
}

/// <summary>What the menu shows: the item, an optional big Play, a grid of quick tiles and the rest as grouped rows. Built by <c>SongMenuAction</c>, which decides which actions apply (music, radio or book).
/// The layout follows how many actions there are: a menu with only a couple of plain rows turns them into tiles too, so a short menu is not a few lonely boxes; a long one keeps the tiles on top and the grouped rows below.</summary>
public sealed class OptionsSheetViewModel
{
    private const int MaxColumns = 4;
    private const string CoverDp = "52";
    private const int FoldAtMostRows = 2;

    public OptionsSheetViewModel(string title, string subtitle, string? thumbnailUrl, string placeholderGlyph, OptionsEntry? play, IReadOnlyList<OptionsEntry> quick, IReadOnlyList<OptionsGroup> groups)
    {
        Title = title;
        Subtitle = subtitle;
        ThumbnailUrl = thumbnailUrl;
        Thumbnail = new ThumbnailSizeConverter().Convert(thumbnailUrl, typeof(ImageSource), CoverDp, System.Globalization.CultureInfo.InvariantCulture) as ImageSource;
        PlaceholderGlyph = placeholderGlyph;
        Play = play;

        // Only a couple of plain rows left (and nothing red): they sit better as tiles than as lone cards.
        var rows = groups.Sum(group => group.Items.Count);
        var fold = rows is > 0 and <= FoldAtMostRows && groups.All(group => group.Items.All(item => !item.IsDestructive));
        Quick = fold ? quick.Concat(groups.SelectMany(group => group.Items)).ToList() : quick;
        Groups = fold ? [] : groups;

        // Evenly filled rows: 2 tiles are halves, 3 are thirds, 4 are quarters, 5-6 wrap into two rows of 3, more into rows of 4.
        Columns = Quick.Count switch { 0 => 0, <= MaxColumns => Quick.Count, <= 6 => 3, _ => MaxColumns };
        for (var i = 0; i < Quick.Count; i++)
        {
            Quick[i].GridRow = i / Columns;
            Quick[i].GridColumn = i % Columns;
        }
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string? ThumbnailUrl { get; }

    /// <summary>The cover at the size it is drawn (and from disk when it was fetched ahead of time); null without a URL.</summary>
    public ImageSource? Thumbnail { get; }

    /// <summary>Shown behind the cover: a note for a song, a radio for a station, a book for a book.</summary>
    public string PlaceholderGlyph { get; }

    public OptionsEntry? Play { get; }

    public IReadOnlyList<OptionsEntry> Quick { get; }

    public IReadOnlyList<OptionsGroup> Groups { get; }

    public int Columns { get; }

    public int TileRows => Columns == 0 ? 0 : (Quick.Count + Columns - 1) / Columns;

    public bool HasPlay => Play is not null;

    public bool HasQuick => Quick.Count > 0;

    public bool HasGroups => Groups.Count > 0;
}
