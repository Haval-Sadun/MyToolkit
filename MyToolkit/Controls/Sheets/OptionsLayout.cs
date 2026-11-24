namespace MyToolkit.Controls.Sheets;

/// <summary>How the options sheet lays its actions out. Pure and platform-free, so it is unit-tested.</summary>
public static class OptionsLayout
{
    /// <summary>The most quick tiles in one row.</summary>
    public const int MaxColumns = 4;

    /// <summary>A menu with this few plain rows left turns them into tiles too, so a short menu is not a few lonely boxes.</summary>
    public const int FoldAtMostRows = 2;

    /// <summary>Whether the grouped rows fold into the tile row: only a couple of them, and none destructive (a red action keeps its own row, away from the easy-to-hit tiles).</summary>
    public static bool ShouldFold(int rows, bool anyDestructive) => !anyDestructive && rows is > 0 and <= FoldAtMostRows;

    /// <summary>Evenly filled rows: 2 tiles are halves, 3 are thirds, 4 are quarters, 5-6 wrap into two rows of 3, more into rows of 4.</summary>
    public static int Columns(int tiles) => tiles switch { <= 0 => 0, <= MaxColumns => tiles, <= 6 => 3, _ => MaxColumns };

    /// <summary>Where tile <paramref name="index"/> sits in a grid of <paramref name="columns"/> columns.</summary>
    public static (int Row, int Column) Cell(int index, int columns) => (index / columns, index % columns);

    /// <summary>How many grid rows <paramref name="tiles"/> tiles take.</summary>
    public static int Rows(int tiles) => tiles <= 0 ? 0 : (tiles + Columns(tiles) - 1) / Columns(tiles);
}
