using System.Collections;
using UXDivers.Popups;
using UXDivers.Popups.Maui.Controls;

namespace MyToolkit.Services.Popups;

/// <summary>
/// A thin, one-call-per-popup facade over the toolkit's popups: centered dialogs, bottom sheets and
/// transient toasts, all styled from the app palette (see <see cref="PopupTheme"/>). Inject
/// <see cref="IPopupPresenter"/> and show any popup with a single line — the implementation builds
/// the control, wires its buttons to a result, and awaits dismissal for you.
///
/// <para>All display text is taken verbatim from the caller; localize strings before passing them in.</para>
/// </summary>
public interface IPopupPresenter
{
    // ── Transient notifications ─────────────────────────────────────────────

    /// <summary>Shows a lightweight toast (icon + title) that the user dismisses. Fire-and-forget.</summary>
    Task ToastAsync(string title, string? icon = null, Color? iconColor = null,
        VerticalPosition position = VerticalPosition.Top);

    /// <summary>Shows a floater (icon + title + text) anchored top or bottom. Fire-and-forget.</summary>
    Task FloaterAsync(string title, string text, string? icon = null, Color? iconColor = null,
        VerticalPosition position = VerticalPosition.Top);

    // ── Informational (single dismiss) ──────────────────────────────────────

    /// <summary>
    /// Shows a dialog (optional title + text + one button reading <paramref name="actionText"/>, "OK" when null). Completes when dismissed.
    /// Text-first because the common case is a bare message: <c>AlertAsync(message)</c>.
    /// </summary>
    Task AlertAsync(string text, string? title = null, string? closeIcon = null, Color? closeIconColor = null, string? actionText = null);

    /// <summary>
    /// Shows a dialog with a prominent icon, title and text. The button reads
    /// <paramref name="actionText"/> ("OK" when null) and simply closes the popup. Completes when dismissed.
    /// </summary>
    Task IconAlertAsync(string icon, string title, string text, string? actionText = null, Color? iconColor = null);

    // ── Decisions ───────────────────────────────────────────────────────────

    /// <summary>
    /// Shows a dialog and returns the user's choice: <c>true</c> for the primary
    /// (accept) button, <c>false</c> for the secondary (cancel) button or any other dismissal. The cancel
    /// button is hidden when <paramref name="cancelText"/> is null. With <paramref name="isDestructive"/> the
    /// accept button uses the danger colour.
    /// </summary>
    Task<bool> ConfirmAsync(string title, string text, string acceptText, string? cancelText = null, bool isDestructive = false);

    /// <summary>
    /// Shows a bottom sheet hosting arbitrary <paramref name="content"/> with a title, an action button and
    /// a cancel button reading <paramref name="cancelText"/>. Returns <c>true</c> if the action button was tapped,
    /// <c>false</c> if cancelled/dismissed.
    /// </summary>
    Task<bool> ActionModalAsync(string title, View content, string actionText, string? closeIcon = null, string cancelText = "Cancel");

    // ── Collection input ────────────────────────────────────────────────────

    /// <summary>
    /// Shows a bottom sheet with a text entry per field, built from <paramref name="fields"/> and returns the entered
    /// values (in field order) when the action button is tapped, or <c>null</c> if the form was dismissed.
    /// The same <paramref name="fields"/> instances also carry the values via <see cref="FormField.Value"/>.
    /// </summary>
    Task<IReadOnlyList<string>?> FormAsync(string title, string actionText, IEnumerable<FormField> fields,
        string? text = null);

    /// <summary>
    /// Shows a bottom sheet listing <paramref name="items"/> with the given
    /// <paramref name="itemTemplate"/>. Returns <c>true</c> if the action button was tapped,
    /// <c>false</c> if closed/dismissed.
    /// </summary>
    Task<bool> ListActionAsync(string title, IEnumerable items, DataTemplate itemTemplate, string? actionText = null);

    /// <summary>
    /// Shows a bottom sheet of <paramref name="options"/> and returns the
    /// <see cref="PopupOption{T}.Value"/> of the tapped option, or <c>default</c> if dismissed without a choice.
    /// Row text left-aligns with the title unless some option has a leading icon.
    /// </summary>
    Task<T?> OptionSheetAsync<T>(string title, IEnumerable<PopupOption<T>> options, string? closeIcon = null);

    /// <summary>
    /// Shows a bottom sheet hosting the view returned by <paramref name="buildContent"/>, which receives a
    /// <c>close(result)</c> callback that completes the task and closes the sheet. Dismissal by scrim tap or
    /// back completes with <c>default</c>.
    /// </summary>
    Task<T?> CustomSheetAsync<T>(string? title, Func<Action<T?>, View> buildContent);
}
