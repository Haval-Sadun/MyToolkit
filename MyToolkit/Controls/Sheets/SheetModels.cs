namespace MyToolkit.Controls.Sheets;

/// <summary>One choice of a pick-one sheet (<see cref="SheetPresenterExtensions.ChooseAsync{T}"/>).</summary>
/// <param name="Value">What the caller gets back when this one is picked.</param>
/// <param name="Badge">A short mark in a tile at the start of the row: letters, or a glyph when <paramref name="BadgeIsIcon"/>.</param>
public sealed record ChoiceOption<T>(T Value, string Title, string? Detail = null, string? Badge = null, bool BadgeIsIcon = false);

/// <summary>How a pick-one sheet shows its choices.</summary>
public enum ChoiceStyle
{
    /// <summary>A card per choice, with a radio: for choices that have a name and a detail.</summary>
    Cards,

    /// <summary>Chips that wrap: for short choices (a speed, a duration) that are all visible at once.</summary>
    Chips,
}

/// <summary>What every row of a toggle sheet carries, whatever its value type. Use <see cref="ToggleOption{T}"/>.</summary>
public abstract record ToggleOptionBase(string Title, string? Detail, bool IsSelected, string? Badge, bool BadgeIsIcon);

/// <summary>One row of a toggle sheet (<see cref="SheetPresenterExtensions.ToggleAsync{T}"/>): an item that is in or out, such as a playlist the song is in.</summary>
/// <param name="Value">What the toggle callback receives for this row.</param>
public sealed record ToggleOption<T>(T Value, string Title, string? Detail, bool IsSelected, string? Badge = null, bool BadgeIsIcon = false) : ToggleOptionBase(Title, Detail, IsSelected, Badge, BadgeIsIcon);

/// <summary>What a toggle did: the row's new state, and its new detail line (null keeps the old one).</summary>
public sealed record ToggleResult(bool IsSelected, string? Detail = null);

/// <summary>How a toggle sheet was left.</summary>
public enum ToggleExit
{
    /// <summary>Closed with Done, or dismissed.</summary>
    Closed,

    /// <summary>The "New" pill was tapped: the caller asks for a name and makes the item.</summary>
    NewRequested,
}
