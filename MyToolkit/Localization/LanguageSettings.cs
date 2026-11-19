using System.Globalization;
using Microsoft.Maui.Storage;
using MyToolkit.Theme;

namespace MyToolkit.Localization;

/// <summary>The language the user picked, kept in Preferences and applied once at startup (pages read their text when they load, so a change needs a restart).</summary>
/// <param name="key">The Preferences key the choice is saved under.</param>
public sealed class LanguageSettings(string key, LanguageCatalog catalog)
{
    public string Code
    {
        get => Preferences.Default.Get(key, LanguageCatalog.System);
        set => Preferences.Default.Set(key, value);
    }

    /// <summary>First thing at launch, before any page reads its text. With the system choice nothing changes. Also tells <see cref="Direction"/> which way arrows and chevrons point.</summary>
    public void ApplyAtStartup()
    {
        Direction.ForcedRightToLeft = ForcedFlowDirection is { } forced ? forced == FlowDirection.RightToLeft : null;

        if (catalog.Resolve(Code) is not { } culture)
            return;

        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>The layout direction to force on the app, or null to follow the device as before. Only a language the user chose forces it, so a device that is already right to left behaves as it always did.</summary>
    public FlowDirection? ForcedFlowDirection => catalog.Resolve(Code) is not null
        ? catalog.IsRightToLeft(Code) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight
        : null;
}
