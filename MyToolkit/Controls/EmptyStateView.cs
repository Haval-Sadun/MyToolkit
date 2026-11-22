using System.Windows.Input;

using MyToolkit.Theme;

namespace MyToolkit.Controls;

/// <summary>What a screen shows when there is nothing to list: a large glyph, a title, an optional sentence of explanation and an optional action. One calm block, centred, so every empty screen looks like the same app.</summary>
/// <remarks>Generic, no app types. Uses the MaterialIcons and OpenSansSemibold fonts the other toolkit controls already assume.</remarks>
public sealed class EmptyStateView : ContentView
{
    public static readonly BindableProperty GlyphProperty = BindableProperty.Create(nameof(Glyph), typeof(string), typeof(EmptyStateView), string.Empty, propertyChanged: (b, _, v) => ((EmptyStateView)b).Apply());

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(EmptyStateView), string.Empty, propertyChanged: (b, _, v) => ((EmptyStateView)b).Apply());

    public static readonly BindableProperty MessageProperty = BindableProperty.Create(nameof(Message), typeof(string), typeof(EmptyStateView), string.Empty, propertyChanged: (b, _, v) => ((EmptyStateView)b).Apply());

    public static readonly BindableProperty ActionTextProperty = BindableProperty.Create(nameof(ActionText), typeof(string), typeof(EmptyStateView), string.Empty, propertyChanged: (b, _, v) => ((EmptyStateView)b).Apply());

    public static readonly BindableProperty ActionCommandProperty = BindableProperty.Create(nameof(ActionCommand), typeof(ICommand), typeof(EmptyStateView), propertyChanged: (b, _, v) => ((EmptyStateView)b)._action.Command = (ICommand?)v);

    private readonly Label _glyph;
    private readonly Label _title;
    private readonly Label _message;
    private readonly PillButton _action;

    public EmptyStateView()
    {
        _glyph = new Label { FontFamily = "MaterialIcons", FontSize = 48, HorizontalOptions = LayoutOptions.Center, IsVisible = false };
        _title = new Label { FontFamily = "OpenSansSemibold", FontSize = 18, HorizontalTextAlignment = TextAlignment.Center, IsVisible = false };
        _message = new Label { FontSize = 14, HorizontalTextAlignment = TextAlignment.Center, MaximumWidthRequest = 320, IsVisible = false };
        _action = new PillButton { HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 6, 0, 0), IsVisible = false };

        ThemeColors.Set(_glyph, Label.TextColorProperty, "OnSurfaceVariant");
        ThemeColors.Set(_title, Label.TextColorProperty, "OnSurface");
        ThemeColors.Set(_message, Label.TextColorProperty, "OnSurfaceVariant");

        // The glyph is decoration; the title says what the screen is saying.
        AutomationProperties.SetIsInAccessibleTree(_glyph, false);

        Content = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(32),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children = { _glyph, _title, _message, _action },
        };
    }

    public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    public string ActionText { get => (string)GetValue(ActionTextProperty); set => SetValue(ActionTextProperty, value); }

    public ICommand? ActionCommand { get => (ICommand?)GetValue(ActionCommandProperty); set => SetValue(ActionCommandProperty, value); }

    /// <summary>Each line shows only when it has text, so a screen sets just what it has to say.</summary>
    private void Apply()
    {
        _glyph.Text = Glyph;
        _glyph.IsVisible = !string.IsNullOrEmpty(Glyph);
        _title.Text = Title;
        _title.IsVisible = !string.IsNullOrEmpty(Title);
        _message.Text = Message;
        _message.IsVisible = !string.IsNullOrEmpty(Message);
        _action.Text = ActionText;
        _action.IsVisible = !string.IsNullOrEmpty(ActionText);
    }
}
