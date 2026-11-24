namespace MyToolkit.Controls.Sheets;

/// <summary>Colors a sheet needs that the popup palette does not carry itself.</summary>
public static class SheetColors
{
    /// <summary>Black or white, whichever reads better on <paramref name="fill"/>.</summary>
    public static Color ContrastOn(Color? fill) => fill is not null && 0.2126 * fill.Red + 0.7152 * fill.Green + 0.0722 * fill.Blue > 0.6 ? Colors.Black : Colors.White;

    /// <summary>The text color for the popup palette's accent (<c>PrimaryColor</c>): what sits on a filled button or a selected chip.</summary>
    public static Color OnPrimary()
    {
        var primary = Application.Current?.Resources.TryGetValue("PrimaryColor", out var value) == true ? value as Color : null;
        return ContrastOn(primary);
    }
}
