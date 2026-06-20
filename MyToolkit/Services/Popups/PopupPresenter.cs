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

    public Task AlertAsync(string text, string? title = null, string? closeIcon = null, Color? closeIconColor = null)
    {
        var popup = new SimpleTextPopup { Text = text };
        if (title is not null) popup.Title = title;
        if (closeIcon is not null) popup.CloseButtonIconText = closeIcon;
        if (closeIconColor is not null) popup.CloseButtonIconColor = closeIconColor;
        ApplySafeArea(popup);
        return Service.PushAsync(popup);
    }

    public Task IconAlertAsync(string icon, string title, string text, string? actionText = null,
        Color? iconColor = null)
    {
        var popup = new IconTextPopup
        {
            IconText = icon,
            Title = title,
            Text = text,
            ShowActionButton = actionText is not null,
        };
        if (actionText is not null) popup.ActionButtonText = actionText;
        if (iconColor is not null) popup.IconColor = iconColor;
        ApplySafeArea(popup);
        return Service.PushAsync(popup);
    }

    // ── Decisions ───────────────────────────────────────────────────────────

    public Task<bool> ConfirmAsync(string title, string text, string acceptText, string? cancelText = null)
    {
        var popup = new SimpleActionPopup
        {
            Title = title,
            Text = text,
            ActionButtonText = acceptText,
            ShowActionButton = true,
            ShowSecondaryActionButton = cancelText is not null,
        };
        var tcs = new TaskCompletionSource<bool>();
        popup.ActionButtonCommand = ResolveCommand(popup, tcs, true);
        if (cancelText is not null)
        {
            popup.SecondaryActionButtonText = cancelText;
            popup.SecondaryActionButtonCommand = ResolveCommand(popup, tcs, false);
        }
        ApplySafeArea(popup);
        return ShowForResultAsync(popup, tcs, dismissed: false);
    }

    public Task<bool> ActionModalAsync(string title, View content, string actionText, string? closeIcon = null)
    {
        var popup = new ActionModalPopup
        {
            Title = title,
            PopupContent = content,
            ActionButtonText = actionText,
            ShowActionButton = true,
        };
        if (closeIcon is not null) popup.CloseButtonIconText = closeIcon;
        ApplySafeArea(popup);
        var tcs = new TaskCompletionSource<bool>();
        popup.ActionButtonCommand = ResolveCommand(popup, tcs, true);
        popup.CloseButtonCommand = ResolveCommand(popup, tcs, false);
        return ShowForResultAsync(popup, tcs, dismissed: false);
    }

    // ── Collection input ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<string>?> FormAsync(string title, string actionText,
        IEnumerable<FormField> fields, string? text = null)
    {
        var popup = new FormPopup
        {
            Title = title,
            ActionButtonText = actionText,
            ShowActionButton = true,
            Items = fields.ToList(),
        };
        if (text is not null) popup.Text = text;
        ApplySafeArea(popup);
        // FormPopup is a PopupResultPage<List<string>>; its default action button collects the
        // field values and sets the result. A dismissal leaves the result null.
        return await Service.PushAsync(popup);
    }

    public Task<bool> ListActionAsync(string title, IEnumerable items, DataTemplate itemTemplate,
        string? actionText = null)
    {
        var popup = new ListActionPopup
        {
            Title = title,
            ItemsSource = items,
            ItemDataTemplate = itemTemplate,
            ShowActionButton = actionText is not null,
        };
        if (actionText is not null) popup.ActionButtonText = actionText;
        var tcs = new TaskCompletionSource<bool>();
        popup.ActionButtonCommand = ResolveCommand(popup, tcs, true);
        popup.CloseButtonCommand = ResolveCommand(popup, tcs, false);
        ApplySafeArea(popup);
        return ShowForResultAsync(popup, tcs, dismissed: false);
    }

    public Task<T?> OptionSheetAsync<T>(string title, IEnumerable<PopupOption<T>> options, string? closeIcon = null)
    {
        var popup = new OptionSheetPopup
        {
            Title = title,
            //ItemDataTemplate = (DataTemplate)Application.Current!.Resources["OptionSheetRowTemplate"],
        };
        if (closeIcon is not null) popup.CloseButtonIconText = closeIcon;

        var tcs = new TaskCompletionSource<T?>();
        // Guards against a double pop if the option's Command fires more than once
        // (custom template tap + popup's own tap handling).
        var closed = false;
        void Resolve(T? value)
        {
            if (closed) return;
            closed = true;
            tcs.TrySetResult(value);
            _ = Service.PopAsync(popup);
        }

        var items = new List<OptionSheetItem>();
        foreach (var option in options)
        {
            var item = new OptionSheetItem
            {
                Text = option.Text,
                //IsDestructive = option.IsDestructive,
                Command = new Command(() => Resolve(option.Value)),
            };
            if (option.Icon is not null)
                item.Icon = option.Icon;
            if (option.IconColor is not null)
                item.IconColor = option.IconColor;
            if (option.GroupName is not null)
                item.GroupName = option.GroupName;
            items.Add(item);
        }
        popup.Items = items;
        popup.CloseButtonCommand = new Command(() => Resolve(default));
        ApplySafeArea(popup);
        return ShowForResultAsync(popup, tcs, dismissed: default);
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
        var insets = WindowSystemBarsInsetsDp();
        if (insets != default)
        {
            popup.Padding = new Thickness(insets.Left, insets.Top, insets.Right, insets.Bottom);
            return;
        }
#endif
        popup.SafeAreaAsPadding = SafeAreaAsPadding.All;
    }

#if ANDROID
    private static Thickness WindowSystemBarsInsetsDp()
    {
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            var decorView = activity?.Window?.DecorView;
            var rootInsets = decorView?.RootWindowInsets;
            if (rootInsets is null) return default;
            var compat = AndroidX.Core.View.WindowInsetsCompat.ToWindowInsetsCompat(rootInsets);
            var sys = compat.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars());
            var density = decorView.Resources?.DisplayMetrics?.Density ?? 1f;
            if (density <= 0) density = 1f;
            return new Thickness(sys.Left / density, sys.Top / density, sys.Right / density, sys.Bottom / density);
        }
        catch { return default; }
    }
#endif

    /// <summary>Builds a button command that resolves <paramref name="tcs"/> then closes <paramref name="popup"/>.</summary>
    private static Command ResolveCommand<T>(PopupPage popup, TaskCompletionSource<T> tcs, T value) =>
        new(() =>
        {
            tcs.TrySetResult(value);
            _ = Service.PopAsync(popup);
        });

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
            ApplySafeArea(popup);
            await Service.PushAsync(popup);
            return await tcs.Task;
        }
        finally
        {
            Service.PopupPopped -= OnPopped;
        }
    }
}
