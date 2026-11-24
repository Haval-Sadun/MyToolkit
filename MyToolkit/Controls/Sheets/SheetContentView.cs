namespace MyToolkit.Controls.Sheets;

/// <summary>Base of a sheet's body: puts <c>OnPrimaryColor</c> (the text on the accent) in the view's own resources before its XAML loads, so every sheet reads it the same way, and checks the palette once in debug builds.</summary>
/// <remarks>Use it as the XAML root. Colors, fonts and the border come from the popup palette (see <see cref="SheetTheme"/>), so a sheet follows the app's theme without knowing it.</remarks>
public abstract class SheetContentView : ContentView
{
    protected SheetContentView()
    {
        SheetTheme.CheckOnce();
        Resources["OnPrimaryColor"] = SheetColors.OnPrimary();
    }
}
