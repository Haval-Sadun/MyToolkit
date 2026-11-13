using Microsoft.Maui.Controls.Shapes;
using UXDivers.Popups.Maui;

namespace MyToolkit.Services.Popups;

/// <summary>Centered dialog card: optional icon badge, title, message and one or two pill buttons.</summary>
internal sealed class DialogPopup : PopupPage
{
    private const double MaxWidth = 400;

    public DialogPopup(string? icon, string? title, string message, string primaryText, PopupKit.ButtonKind primaryKind, Action onPrimary, string? secondaryText = null, Action? onSecondary = null)
    {
        var stack = new VerticalStackLayout { Spacing = 12 };
        if (PopupKit.IconBadge(icon) is { } badge)
        {
            badge.Margin = new Thickness(0, 0, 0, 4);
            stack.Add(badge);
        }

        if (!string.IsNullOrEmpty(title))
            stack.Add(PopupKit.Text(title, 20, true, "TextColor", TextAlignment.Center));

        stack.Add(PopupKit.Text(message, 15, false, "TextSecondaryColor", TextAlignment.Center));
        stack.Add(BuildButtons(primaryText, primaryKind, onPrimary, secondaryText, onSecondary));

        var card = new Border
        {
            Padding = 24,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = PopupKit.CardRadius },
            VerticalOptions = LayoutOptions.Center,
            Content = stack,
        };
        card.SetDynamicResource(VisualElement.BackgroundColorProperty, "BackgroundColor");
        card.SetDynamicResource(Border.StrokeProperty, "PopupBorderColor");
        if (PopupKit.IsPhone)
        {
            card.HorizontalOptions = LayoutOptions.Fill;
            card.Margin = new Thickness(24, 0);
        }
        else
        {
            card.HorizontalOptions = LayoutOptions.Center;
            card.WidthRequest = MaxWidth;
        }

        this.SetDynamicResource(BackgroundColorProperty, "PopupBackdropColor");
        CloseWhenBackgroundIsClicked = true;
        AppearingAnimation = new StoryboardAnimation { Strategy = StoryboardStrategy.RunAllAtStart, Animation1 = new FadeInPopupAnimation { Duration = 180 }, Animation2 = new ScaleInPopupAnimation { ScaleFrom = 0.92, Duration = 180 } };
        DisappearingAnimation = new FadeOutPopupAnimation { Duration = 120 };
        PopupContent = card;
    }

    private static View BuildButtons(string primaryText, PopupKit.ButtonKind primaryKind, Action onPrimary, string? secondaryText, Action? onSecondary)
    {
        var primary = PopupKit.PillButton(primaryText, primaryKind, onPrimary);
        if (secondaryText is null)
            return primary;

        var row = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) } };
        row.Add(PopupKit.PillButton(secondaryText, PopupKit.ButtonKind.Ghost, onSecondary ?? (() => { })), 0);
        row.Add(primary, 1);
        return row;
    }
}
