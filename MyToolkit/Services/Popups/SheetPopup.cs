using Microsoft.Maui.Controls.Shapes;
using UXDivers.Popups.Maui;

namespace MyToolkit.Services.Popups;

/// <summary>Bottom sheet: drag handle, optional title, body (optionally scrolling) and optional footer.</summary>
internal sealed class SheetPopup : PopupPage
{
    private const double MaxWidth = 560;
    private const double MaxHeightFraction = 0.85;
    private const double ContentPadding = 20;

    private readonly View? _focusTarget;

    public SheetPopup(string? title, View body, View? footer = null, bool scrollBody = false, bool avoidKeyboard = false, View? focusTarget = null)
    {
        _focusTarget = focusTarget;
        var insets = PopupKit.SystemBarsInsetsDp();
        var bottomInset = 0d;
        if (insets != default)
        {
            Padding = new Thickness(insets.Left, insets.Top, insets.Right, 0);
            bottomInset = insets.Bottom;
        }
        else
        {
            SafeAreaAsPadding = UXDivers.Popups.SafeAreaAsPadding.All;
        }

        var grid = new Grid { RowSpacing = 0, RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) } };
        grid.Add(PopupKit.DragHandle(), 0, 0);
        if (!string.IsNullOrEmpty(title))
        {
            var label = PopupKit.Text(title, 18, true, "TextColor", TextAlignment.Start);
            label.Margin = new Thickness(0, 16, 0, 0);
            grid.Add(label, 0, 1);
        }

        var maxHeight = DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density * MaxHeightFraction;
        body.Margin = new Thickness(0, 12, 0, 0);
        // Auto rows don't shrink the body, so the scroll area gets an explicit cap that leaves room for the chrome.
        var chrome = 150 + bottomInset + (footer is null ? 0 : 130);
        grid.Add(scrollBody ? new ScrollView { Content = body, MaximumHeightRequest = maxHeight - chrome } : body, 0, 2);
        if (footer is not null)
        {
            footer.Margin = new Thickness(0, 16, 0, 0);
            grid.Add(footer, 0, 3);
        }

        var card = new Border
        {
            Padding = new Thickness(ContentPadding, 12, ContentPadding, ContentPadding + bottomInset),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(PopupKit.CardRadius, PopupKit.CardRadius, 0, 0) },
            VerticalOptions = LayoutOptions.End,
            MaximumHeightRequest = maxHeight,
            Content = grid,
        };
        card.SetDynamicResource(VisualElement.BackgroundColorProperty, "BackgroundColor");
        if (PopupKit.IsPhone)
        {
            card.HorizontalOptions = LayoutOptions.Fill;
        }
        else
        {
            card.HorizontalOptions = LayoutOptions.Center;
            card.WidthRequest = MaxWidth;
        }

        this.SetDynamicResource(BackgroundColorProperty, "PopupBackdropColor");
        CloseWhenBackgroundIsClicked = true;
        AvoidKeyboard = avoidKeyboard;
        AppearingAnimation = new MoveInPopupAnimation { MoveDirection = MoveDirection.Bottom, Duration = 250 };
        DisappearingAnimation = new MoveOutPopupAnimation { MoveDirection = MoveDirection.Bottom, Duration = 200 };
        PopupContent = card;
    }

    public override void OnAppearing()
    {
        base.OnAppearing();
        if (_focusTarget is { } target)
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () => target.Focus());
    }
}
