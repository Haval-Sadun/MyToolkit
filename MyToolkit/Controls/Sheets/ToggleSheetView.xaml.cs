using MyToolkit.Services.Popups;

namespace MyToolkit.Controls.Sheets;

/// <summary>Body of the toggle sheet: a header, one tappable row per item, and "Done" as the pinned footer; see <see cref="ToggleSheetViewModel"/>.</summary>
public partial class ToggleSheetView : SheetContentView, ISheetFooterProvider
{
    public ToggleSheetView(ToggleSheetViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Hosted outside this view, so it gets the binding context and its text color here; no button when there is no text for it.
        var footer = (Button)Resources["Footer"];
        footer.TextColor = (Color)Resources["OnPrimaryColor"];
        footer.BindingContext = viewModel;
        Footer = viewModel.HasDone ? footer : null;
    }

    public View? Footer { get; }
}
