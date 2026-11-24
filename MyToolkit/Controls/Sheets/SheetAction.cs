using MyToolkit.Services.Popups;

namespace MyToolkit.Controls.Sheets;

/// <summary>Builds the actions an item menu offers (<see cref="SheetPresenterExtensions.RunActionAsync"/>): a label, a glyph, and what to run.</summary>
public static class SheetAction
{
    /// <summary>An action that is async.</summary>
    public static PopupOption<Func<Task>> Async(string text, Func<Task> run, string? icon, bool isDestructive = false) => new(text, run, icon, IsDestructive: isDestructive);

    /// <summary>An action that finishes at once (a store call).</summary>
    public static PopupOption<Func<Task>> Sync(string text, Action run, string? icon, bool isDestructive = false) => new(text, () =>
    {
        run();
        return Task.CompletedTask;
    }, icon, IsDestructive: isDestructive);
}
