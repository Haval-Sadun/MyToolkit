using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Animation;
using Xaml = Microsoft.UI.Xaml;
using XamlControls = Microsoft.UI.Xaml.Controls;
using XamlMedia = Microsoft.UI.Xaml.Media;

namespace MyToolkit.Platforms.Windows;

/// <summary>In-app stand-in for the toolkit Snackbar, which needs package identity an unpackaged app lacks.</summary>
/// <remarks>A native WinUI Popup over the window's XamlRoot — never re-parents page content, which on Home holds the app's only MediaElement.</remarks>
public static class WindowsBanner
{
    private const double SideMargin = 16;
    private const double BottomMargin = 24;

    /// <summary>Share of the banner's width (or 60px down) a drag must cover to dismiss; shorter drags snap back.</summary>
    private const double DismissFraction = 0.3;
    private const double DismissDown = 60;

    /// <summary>A fast flick dismisses even when short — px per ms, roughly a deliberate swipe.</summary>
    private const double DismissVelocity = 0.8;

    // Static: one banner at a time for the whole app; a new one replaces the old.
    private static Popup? _current;
    private static CancellationTokenSource? _currentCts;

    /// <summary>Must run on the UI thread. Completes when the banner is dismissed or replaced.</summary>
    public static async Task ShowAsync(string message, TimeSpan duration, Action? action = null, string? actionText = null)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView is not Xaml.Window window || window.Content?.XamlRoot is not { } root)
            return;

        Close();

        var cts = new CancellationTokenSource();
        var width = Math.Max(0, root.Size.Width - 2 * SideMargin);
        var banner = BuildBanner(message, width, action is null ? null : () => { action(); cts.Cancel(); }, actionText);
        banner.Measure(new global::Windows.Foundation.Size(width, double.PositiveInfinity));
        EnableSwipeToDismiss(banner, width, cts);

        var popup = new Popup
        {
            XamlRoot = root,
            Child = banner,
            HorizontalOffset = SideMargin,
            VerticalOffset = Math.Max(0, root.Size.Height - banner.DesiredSize.Height - BottomMargin),
            ShouldConstrainToRootBounds = true,
            ChildTransitions = new TransitionCollection { new PopupThemeTransition() },
        };

        _current = popup;
        _currentCts = cts;
        popup.IsOpen = true;

        try
        {
            await Task.Delay(duration, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Replaced by a newer banner, or its action was tapped.
        }
        finally
        {
            popup.IsOpen = false;
            popup.Child = null;
            if (ReferenceEquals(_current, popup))
            {
                _current = null;
                _currentCts = null;
            }
            cts.Dispose();
        }
    }

    private static void Close()
    {
        // Only Cancel, never Dispose: the showing invocation's own finally owns disposing it.
        _currentCts?.Cancel();
        _current = null;
        _currentCts = null;
    }

    /// <summary>Drag sideways or down to dismiss — touch, pen and mouse alike (WinUI raises manipulations for all three once ManipulationMode is set).</summary>
    private static void EnableSwipeToDismiss(XamlControls.Border banner, double width, CancellationTokenSource cts)
    {
        var offset = new XamlMedia.TranslateTransform();
        banner.RenderTransform = offset;
        banner.ManipulationMode = Xaml.Input.ManipulationModes.TranslateX | Xaml.Input.ManipulationModes.TranslateY;

        // Owned by the banner and released with it, so no unsubscribe needed.
        banner.ManipulationDelta += (_, e) =>
        {
            offset.X += e.Delta.Translation.X;
            offset.Y = Math.Max(0, offset.Y + e.Delta.Translation.Y);
            var progress = Math.Max(Math.Abs(offset.X) / width, offset.Y / (DismissDown * 2));
            banner.Opacity = Math.Clamp(1 - progress, 0.2, 1);
        };

        banner.ManipulationCompleted += (_, e) =>
        {
            var flung = Math.Abs(e.Velocities.Linear.X) > DismissVelocity || e.Velocities.Linear.Y > DismissVelocity;
            if (flung || Math.Abs(offset.X) > width * DismissFraction || offset.Y > DismissDown)
            {
                // Same path as the timeout and the action button: ShowAsync's finally closes the popup.
                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The banner timed out mid-drag — it's already gone.
                }
                return;
            }

            offset.X = 0;
            offset.Y = 0;
            banner.Opacity = 1;
        };
    }

    private static XamlControls.Border BuildBanner(string message, double width, Action? onAction, string? actionText)
    {
        var white = new XamlMedia.SolidColorBrush(Microsoft.UI.Colors.White);
        var text = new XamlControls.TextBlock
        {
            Text = message,
            Foreground = white,
            FontSize = 14,
            TextWrapping = Xaml.TextWrapping.Wrap,
            VerticalAlignment = Xaml.VerticalAlignment.Center,
            HorizontalAlignment = onAction is null ? Xaml.HorizontalAlignment.Center : Xaml.HorizontalAlignment.Left,
            TextAlignment = onAction is null ? Xaml.TextAlignment.Center : Xaml.TextAlignment.Left,
        };

        var row = new XamlControls.Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new XamlControls.ColumnDefinition { Width = new Xaml.GridLength(1, Xaml.GridUnitType.Star) });
        row.ColumnDefinitions.Add(new XamlControls.ColumnDefinition { Width = Xaml.GridLength.Auto });
        row.Children.Add(text);

        if (onAction is not null)
        {
            var button = new XamlControls.Button
            {
                Content = actionText ?? "OK",
                Foreground = white,
                Background = new XamlMedia.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Xaml.Thickness(0),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            };
            // Owned by the banner and released with it, so no unsubscribe needed.
            button.Click += (_, _) => onAction();
            XamlControls.Grid.SetColumn(button, 1);
            row.Children.Add(button);
        }

        return new XamlControls.Border
        {
            Width = width,
            CornerRadius = new Xaml.CornerRadius(8),
            Background = new XamlMedia.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(220, 50, 50, 50)),
            Padding = new Xaml.Thickness(16, 10, 16, 10),
            Child = row,
        };
    }
}
