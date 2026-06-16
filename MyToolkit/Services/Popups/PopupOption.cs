namespace MyToolkit.Services.Popups;

/// <summary>
/// A single selectable option for <see cref="IPopupPresenter.OptionSheetAsync{T}"/>.
/// Carries a strongly-typed <see cref="Value"/> that is returned when the option is tapped,
/// so callers never have to wire <c>ICommand</c>s by hand.
/// </summary>
/// <typeparam name="T">The type of the value returned when this option is selected.</typeparam>
/// <param name="IsDestructive">When <c>true</c> the row renders with a danger (red) accent.</param>
public sealed record PopupOption<T>(
    string Text,
    T Value,
    string? Icon = null,
    Color? IconColor = null,
    string? GroupName = null,
    bool IsDestructive = false);
