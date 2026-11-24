namespace MyToolkit.Controls.Sheets;

/// <summary>The options sheet of a song, a station, a book, a folder or a playlist: the item, a main action, quick actions and grouped rows. Which actions appear is decided by the caller.</summary>
public partial class OptionsSheetView : SheetContentView
{
    public OptionsSheetView(OptionsSheetViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // As many equal columns and as many rows as the tiles need; a Grid cannot take these from a binding.
        for (var column = 0; column < viewModel.Columns; column++)
            QuickGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        for (var row = 0; row < viewModel.TileRows; row++)
            QuickGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        PlayButton.TextColor = (Color)Resources["OnPrimaryColor"];
    }
}
