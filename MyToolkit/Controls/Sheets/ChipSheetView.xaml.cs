namespace MyToolkit.Controls.Sheets;

/// <summary>Body of a pick-one sheet: short choices as chips; see <see cref="ChoiceSheetViewModel"/>.</summary>
public partial class ChipSheetView : SheetContentView
{
    public ChipSheetView(ChoiceSheetViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
