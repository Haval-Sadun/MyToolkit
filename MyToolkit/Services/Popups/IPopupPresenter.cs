using System.Collections;
using UXDivers.Popups;
using UXDivers.Popups.Maui.Controls;

namespace MyToolkit.Services.Popups;

/// <summary>
/// A thin, one-call-per-popup facade over every popup type exposed by the
/// <c>UXDivers.Popups.Maui</c> library. Inject <see cref="IPopupPresenter"/> and show any
/// popup with a single line — the implementation builds the control, wires its buttons to a
/// result, and awaits dismissal for you.
///
/// <para>All display text is taken verbatim from the caller; localize strings before passing them in.</para>
/// </summary>
public interface IPopupPresenter
{
    // ── Transient notifications ─────────────────────────────────────────────

    /// <summary>Shows a lightweight <see cref="Toast"/> (icon + title) that the user dismisses. Fire-and-forget.</summary>
    Task ToastAsync(string title, string? icon = null, Color? iconColor = null,
        VerticalPosition position = VerticalPosition.Top);

    /// <summary>Shows a <see cref="FloaterPopup"/> (icon + title + text) anchored top or bottom. Fire-and-forget.</summary>
    Task FloaterAsync(string title, string text, string? icon = null, Color? iconColor = null,
        VerticalPosition position = VerticalPosition.Top);

    // ── Informational (single dismiss) ──────────────────────────────────────

    /// <summary>
    /// Shows a <see cref="SimpleTextPopup"/> (optional title + text + close button). Completes when dismissed.
    /// Text-first because the common case is a bare message: <c>AlertAsync(message)</c>.
    /// </summary>
    Task AlertAsync(string text, string? title = null, string? closeIcon = null, Color? closeIconColor = null);

    /// <summary>
    /// Shows an <see cref="IconTextPopup"/> (prominent icon + title + text). When
    /// <paramref name="actionText"/> is supplied an action button is shown that simply closes the popup.
    /// Completes when dismissed.
    /// </summary>
    Task IconAlertAsync(string icon, string title, string text, string? actionText = null, Color? iconColor = null);

    // ── Decisions ───────────────────────────────────────────────────────────

    /// <summary>
    /// Shows a <see cref="SimpleActionPopup"/> and returns the user's choice:
    /// <c>true</c> for the primary (accept) button, <c>false</c> for the secondary (cancel) button
    /// or any other dismissal. The cancel button is hidden when <paramref name="cancelText"/> is null.
    /// </summary>
    Task<bool> ConfirmAsync(string title, string text, string acceptText, string? cancelText = null);

    /// <summary>
    /// Shows an <see cref="ActionModalPopup"/> hosting arbitrary <paramref name="content"/> with a title,
    /// close button and a single action button. Returns <c>true</c> if the action button was tapped,
    /// <c>false</c> if closed/dismissed.
    /// </summary>
    Task<bool> ActionModalAsync(string title, View content, string actionText, string? closeIcon = null);

    // ── Collection input ────────────────────────────────────────────────────

    /// <summary>
    /// Shows a <see cref="FormPopup"/> built from <paramref name="fields"/> and returns the entered
    /// values (in field order) when the action button is tapped, or <c>null</c> if the form was dismissed.
    /// The same <paramref name="fields"/> instances also carry the values via <see cref="FormField.Value"/>.
    /// </summary>
    Task<IReadOnlyList<string>?> FormAsync(string title, string actionText, IEnumerable<FormField> fields,
        string? text = null);

    /// <summary>
    /// Shows a <see cref="ListActionPopup"/> bound to <paramref name="items"/> with the given
    /// <paramref name="itemTemplate"/>. Returns <c>true</c> if the action button was tapped,
    /// <c>false</c> if closed/dismissed.
    /// </summary>
    Task<bool> ListActionAsync(string title, IEnumerable items, DataTemplate itemTemplate, string? actionText = null);

    /// <summary>
    /// Shows an <see cref="OptionSheetPopup"/> bottom sheet of <paramref name="options"/> and returns the
    /// <see cref="PopupOption{T}.Value"/> of the tapped option, or <c>default</c> if dismissed without a choice.
    /// </summary>
    Task<T?> OptionSheetAsync<T>(string title, IEnumerable<PopupOption<T>> options, string? closeIcon = null);
}
