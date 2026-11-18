namespace MyToolkit.Localization;

/// <summary>XAML access to <see cref="Localizer"/>: <c>Text="{loc:Localize Settings_Title}"</c>. The text is read once when the page loads, so a language change applies the next time the app starts.</summary>
[ContentProperty(nameof(Key))]
public sealed class LocalizeExtension : IMarkupExtension<string>
{
    public string Key { get; set; } = string.Empty;

    public string ProvideValue(IServiceProvider serviceProvider) => Localizer.Get(Key);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
