using System.Collections;

using UXDivers.Popups;
using UXDivers.Popups.Maui;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;

namespace MyToolkit.Services.Popups;

/// <summary>
/// Default <see cref="IPopupPresenter"/> implementation. Each method constructs the relevant
/// UXDivers popup, wires its buttons to a <see cref="TaskCompletionSource{TResult}"/> and shows it
/// through <see cref="IPopupService.Current"/>. Stateless — registered as a singleton.
/// </summary>
public sealed class PopupPresenter : IPopupPresenter
{
    private static IPopupService Service => IPopupService.Current;

    // ── Transient notifications ─────────────────────────────────────────────

    public Task ToastAsync(string title, string? icon = null, Color? iconColor = null,
        VerticalPosition position = VerticalPosition.Bottom)
    {
        var popup = new Toast { Title = title, VerticalPosition = position };
        if (icon is not null) popup.IconText = icon;
        if (iconColor is not null) popup.IconColor = iconColor;
        ApplySafeArea(popup);
        return Service.PushAsync(popup);
    }

    public Task FloaterAsync(string title, string text, string? icon = null, Color? iconColor = null,
        VerticalPosition position = VerticalPosition.Top)
    {
        var popup = new FloaterPopup { Title = title, Text = text, VerticalPosition = position };
        if (icon is not null) popup.IconText = icon;
        if (iconColor is not null) popup.IconColor = iconColor;
        ApplySafeArea(popup);
        return Service.PushAsync(popup);
    }

    // ── Informational (single dismiss) ──────────────────────────────────────

    public Task AlertAsync(string text, string? title = null, string? closeIcon = null, Color? closeIconColor = null, string? actionText = null)
        => ShowAlertAsync(PopupKit.DefaultInfoIcon(), title, text, actionText);

    public Task IconAlertAsync(string icon, string title, string text, string? actionText = null, Color? iconColor = null)
        => ShowAlertAsync(icon, title, text, actionText);

    private static Task ShowAlertAsync(string? icon, string? title, string text, string? actionText)
    {
        var tcs = new TaskCompletionSource<bool>();
        DialogPopup? popup = null;
        popup = new DialogPopup(icon, title, text, actionText ?? "OK", PopupKit.ButtonKind.Primary, () => Resolve(popup!, tcs, true));
        ApplySafeArea(popup);
        return ShowForResultAsync(popup, tcs, dismissed: true);
    }

    // ── Decisions ───────────────────────────────────────────────────────────

    public Task<bool> ConfirmAsync(string title, string text, string acceptText, string? cancelText = null, bool isDestructive = false)
    {
        var tcs = new TaskCompletionSource<bool>();
        DialogPopup? popup = null;
        popup = new DialogPopup(null, title, text, acceptText, isDestructive ? PopupKit.ButtonKind.Danger : PopupKit.ButtonKind.Primary, () => Resolve(popup!, tcs, true), cancelText, () => Resolve(popup!, tcs, false));
        ApplySafeArea(popup);
        return ShowForResultAsync(popup, tcs, dismissed: false);
    }

    public Task<bool> ActionModalAsync(string title, View content, string actionText, string? closeIcon = null, string cancelText = "Cancel")
    {
        var tcs = new TaskCompletionSource<bool>();
        SheetPopup? popup = null;
        var footer = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                PopupKit.PillButton(actionText, PopupKit.ButtonKind.Primary, () => Resolve(popup!, tcs, true)),
                PopupKit.PillButton(cancelText, PopupKit.ButtonKind.Ghost, () => Resolve(popup!, tcs, false)),
            },
        };
        popup = new SheetPopup(title, content, footer, scrollBody: content is not (ScrollView or ItemsView));
        return ShowForResultAsync(popup, tcs, dismissed: false);
    }

    // ── Collection input ────────────────────────────────────────────────────

    public Task<IReadOnlyList<string>?> FormAsync(string title, string actionText, IEnumerable<FormField> fields, string? text = null)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<string>?>();
        var list = fields.ToList();
        SheetPopup? popup = null;
        void Submit() => Resolve<IReadOnlyList<string>?>(popup!, tcs, list.Select(field => field.Value ?? string.Empty).ToList());

        var body = new VerticalStackLayout { Spacing = 12 };
        if (text is not null)
            body.Add(PopupKit.Text(text, 15, false, "TextSecondaryColor", TextAlignment.Start));

        Entry? first = null;
        for (var i = 0; i < list.Count; i++)
        {
            body.Add(PopupKit.FormEntry(list[i], i == list.Count - 1, Submit, out var input));
            first ??= input;
        }

        var footer = PopupKit.PillButton(actionText, PopupKit.ButtonKind.Primary, Submit);
        popup = new SheetPopup(title, body, footer, scrollBody: true, avoidKeyboard: true, focusTarget: first);
        return ShowForResultAsync<IReadOnlyList<string>?>(popup, tcs, dismissed: null);
    }

    public Task<bool> ListActionAsync(string title, IEnumerable items, DataTemplate itemTemplate, string? actionText = null)
    {
        var tcs = new TaskCompletionSource<bool>();
        SheetPopup? popup = null;
        var list = new VerticalStackLayout { Spacing = 4 };
        BindableLayout.SetItemsSource(list, items);
        BindableLayout.SetItemTemplate(list, itemTemplate);
        var footer = actionText is null ? null : PopupKit.PillButton(actionText, PopupKit.ButtonKind.Primary, () => Resolve(popup!, tcs, true));
        popup = new SheetPopup(title, list, footer, scrollBody: true);
        return ShowForResultAsync(popup, tcs, dismissed: false);
    }

    public Task<T?> OptionSheetAsync<T>(string title, IEnumerable<PopupOption<T>> options, string? closeIcon = null)
    {
        var tcs = new TaskCompletionSource<T?>();
        SheetPopup? popup = null;
        var body = new VerticalStackLayout { Spacing = 4 };
        string? group = null;
        var list = options.ToList();
        var reserveIconSlot = list.Any(option => PopupKit.HasLeadingIcon(option.Icon));
        foreach (var option in list)
        {
            if (option.GroupName is not null && option.GroupName != group)
                body.Add(PopupKit.GroupHeader(option.GroupName));

            group = option.GroupName;
            var value = option.Value;
            body.Add(PopupKit.OptionRow(option.Text, option.Icon, option.IconColor, option.IsDestructive, reserveIconSlot, () => Resolve<T?>(popup!, tcs, value)));
        }

        popup = new SheetPopup(title, body, scrollBody: true);
        return ShowForResultAsync<T?>(popup, tcs, dismissed: default);
    }

    public Task<T?> CustomSheetAsync<T>(string? title, Func<Action<T?>, View> buildContent)
    {
        var tcs = new TaskCompletionSource<T?>();
        SheetPopup? popup = null;
        var content = buildContent(result => Resolve<T?>(popup!, tcs, result));
        popup = new SheetPopup(title, content, scrollBody: content is not (ScrollView or ItemsView));
        return ShowForResultAsync<T?>(popup, tcs, dismissed: default);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Pads the popup by the system safe-area insets on every edge so its content (especially
    /// bottom-anchored sheets) clears the Android gesture / 3-button navigation bar under the
    /// enforced edge-to-edge layout of Android 15+.
    /// </summary>
    private static void ApplySafeArea(PopupPage popup)
    {
#if ANDROID
        // SafeAreaAsPadding.All reads insets forwarded through the MAUI page layer. Pages
        // that deliberately opt out of safe-area handling (e.g. full-screen image viewers)
        // zero those forwarded insets, leaving bottom-anchored popups behind the nav bar.
        // Reading RootWindowInsets from the DecorView is reliable regardless of what the
        // current page does with insets.
        var insets = PopupKit.SystemBarsInsetsDp();
        if (insets != default)
        {
            popup.Padding = new Thickness(insets.Left, insets.Top, insets.Right, insets.Bottom);
            return;
        }
#endif
        popup.SafeAreaAsPadding = SafeAreaAsPadding.All;
    }

    /// <summary>Completes <paramref name="tcs"/> once and closes <paramref name="popup"/>.</summary>
    private static void Resolve<T>(PopupPage popup, TaskCompletionSource<T> tcs, T value)
    {
        tcs.TrySetResult(value);
        _ = Service.PopAsync(popup);
    }

    /// <summary>
    /// Shows <paramref name="popup"/> and awaits its result. If the popup is dismissed without a
    /// button resolving the result (e.g. background tap), <paramref name="dismissed"/> is returned.
    /// </summary>
    private static async Task<T> ShowForResultAsync<T>(PopupPage popup, TaskCompletionSource<T> tcs, T dismissed)
    {
        void OnPopped(object? sender, PopupEventArgs e)
        {
            if (!ReferenceEquals(e.PopupPage, popup)) return;
            Service.PopupPopped -= OnPopped;
            tcs.TrySetResult(dismissed);
        }

        Service.PopupPopped += OnPopped;
        try
        {
            PopupTheme.Refresh();
            await Service.PushAsync(popup);
            return await tcs.Task;
        }
        finally
        {
            Service.PopupPopped -= OnPopped;
        }
    }
}
