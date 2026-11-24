# Bottom sheets

One door: extension methods on the `IPopupPresenter` an app already injects (`SheetPresenterExtensions`). The views stay hidden.

| You want to | Call | You get back |
|---|---|---|
| Pick one of a few choices (language, a limit) | `popups.ChooseAsync(title, options, current)` | the picked `ChoiceOption<T>`, or `null` when dismissed |
| Pick a short value (speed, duration) as chips | `popups.ChooseAsync(..., style: ChoiceStyle.Chips)` | same |
| Flip items in or out of a set (a song in playlists) | `popups.ToggleAsync(title, options, toggle, subtitle, newText, doneText)` | `ToggleExit.NewRequested` or `Closed` |
| Show an item's menu (song, book, folder) | `popups.RunActionAsync(title, subtitle, imageUrl, placeholderGlyph, main, quick, groups)` | runs the tapped action after the sheet closes |
| Same, but run it yourself | `popups.PickActionAsync(...)` | the tapped `Func<Task>` or `null` |

Build menu actions with `SheetAction.Async(...)` and `SheetAction.Sync(...)`.

## Examples

```csharp
// Pick one.
var picked = await popups.ChooseAsync("Fade", [new ChoiceOption<int>(0, "Off"), new ChoiceOption<int>(4, "4 s")], current: 4);
if (picked is { } choice) Save(choice.Value);

// Toggle: every tap saves at once; the callback returns the new state (null = failed, row stays).
var exit = await popups.ToggleAsync(
    "Add to playlist",
    playlists.Select(p => new ToggleOption<Playlist>(p, p.Name, $"{p.Count} songs", inside.Contains(p.Id))).ToList(),
    async (playlist, isIn) => await SaveAsync(playlist, !isIn) ? new ToggleResult(!isIn, $"{playlist.Count + (isIn ? -1 : 1)} songs") : null,
    subtitle: song.Title, newText: "New", doneText: "Done");

// Item menu.
await popups.RunActionAsync(book.Title, book.Author, book.CoverUrl, glyph,
    main: SheetAction.Async("Open", OpenAsync, openGlyph),
    quick: [SheetAction.Async("Share", ShareAsync, shareGlyph)],
    groups: [[SheetAction.Sync("Remove", Remove, deleteGlyph, isDestructive: true)]]);
```

## What a sheet assumes from the app

Colors and fonts come from the popup palette (`PopupTheme.Apply`): `PrimaryColor`, `TextColor`, `TextSecondaryColor`, `BackgroundSecondaryColor`, `PopupBorderColor`, `AppFontFamily`, `AppSemiBoldFamily`. A debug build writes a line when one is missing (`SheetTheme`). Glyphs use the `MaterialIcons` font. An item menu's cover is sized by `ThumbnailSizeConverter`.

## Inside

- `SheetContentView` is the XAML root of every sheet body (sets `OnPrimaryColor`, checks the palette).
- `SheetRowModel` is the row every pick and toggle sheet shares; `SheetBadge` and `SheetCheck` are its two drawn parts.
- `OptionsLayout` holds the item menu's layout rules (columns, when plain rows fold into tiles): pure, tested.
- Older sheets that are not here (sleep timer, bookmarks, ...) can adopt `SheetContentView` when they are next touched.

On Android an app that uses `SegmentedControl` swipes must call `SegmentSwipeRouter.Handle` from its activity's `DispatchTouchEvent` (see that class).
