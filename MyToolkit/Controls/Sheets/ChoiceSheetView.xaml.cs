namespace MyToolkit.Controls.Sheets;

/// <summary>Body of a pick-one sheet: cards with a radio; see <see cref="ChoiceSheetViewModel"/>.</summary>
public partial class ChoiceSheetView : SheetContentView
{
    public ChoiceSheetView(ChoiceSheetViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
