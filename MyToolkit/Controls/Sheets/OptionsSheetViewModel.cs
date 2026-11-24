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

/// <summary>What the menu shows: the item, an optional big Play, a grid of quick tiles and the rest as grouped rows. Built by <c>PickActionAsync</c> from the actions the caller offers.
/// The layout follows how many actions there are: a menu with only a couple of plain rows turns them into tiles too, so a short menu is not a few lonely boxes; a long one keeps the tiles on top and the grouped rows below.</summary>
public sealed class OptionsSheetViewModel
{
    private const string CoverDp = "52";

    public OptionsSheetViewModel(string title, string subtitle, string? thumbnailUrl, string placeholderGlyph, OptionsEntry? play, IReadOnlyList<OptionsEntry> quick, IReadOnlyList<OptionsGroup> groups)
    {
        Title = title;
        Subtitle = subtitle;
        ThumbnailUrl = thumbnailUrl;
        Thumbnail = new ThumbnailSizeConverter().Convert(thumbnailUrl, typeof(ImageSource), CoverDp, System.Globalization.CultureInfo.InvariantCulture) as ImageSource;
        PlaceholderGlyph = placeholderGlyph;
        Play = play;

        // The rules live in OptionsLayout, where they are tested.
        var rows = groups.Sum(group => group.Items.Count);
        var fold = OptionsLayout.ShouldFold(rows, groups.Any(group => group.Items.Any(item => item.IsDestructive)));
        Quick = fold ? quick.Concat(groups.SelectMany(group => group.Items)).ToList() : quick;
        Groups = fold ? [] : groups;

        Columns = OptionsLayout.Columns(Quick.Count);
        for (var i = 0; i < Quick.Count; i++)
            (Quick[i].GridRow, Quick[i].GridColumn) = OptionsLayout.Cell(i, Columns);
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

    public int TileRows => OptionsLayout.Rows(Quick.Count);

    public bool HasPlay => Play is not null;

    public bool HasQuick => Quick.Count > 0;

    public bool HasGroups => Groups.Count > 0;
}
