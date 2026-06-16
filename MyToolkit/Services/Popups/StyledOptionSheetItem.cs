using UXDivers.Popups.Maui.Controls;

namespace MyToolkit.Services.Popups;

/// <summary>
/// An <see cref="OptionSheetItem"/> that also carries an <see cref="IsDestructive"/> flag, so a
/// custom row template (see <c>OptionSheetRowTemplate</c> in App.xaml) can render destructive
/// actions (delete, kick, …) with a danger accent. The flag is bound to from the item template;
/// OptionSheetPopup still treats it as a plain <see cref="OptionSheetItem"/> for tap handling.
/// </summary>
public sealed class StyledOptionSheetItem : OptionSheetItem
{
    public bool IsDestructive { get; set; }
}
