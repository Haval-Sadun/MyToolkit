using MyToolkit.ViewModels;

namespace MyToolkit.Views;

public partial class ErrorReportPage : ToolkitPage
{
    protected override bool DisposeViewModelOnPop => true;

    public ErrorReportPage(ErrorReportViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
        => await Navigation.PopModalAsync();
}
