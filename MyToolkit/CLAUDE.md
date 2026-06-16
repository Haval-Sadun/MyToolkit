# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

**MyToolkit** is a shared MAUI class library consumed by sibling apps (`KurdishConnect`, `SyriaBet.Mobile`, `SyriaBet.Admin`) via `<ProjectReference>`. It contains reusable base classes, services, and controls — no app-specific business logic, no authentication/current-user coupling, no hardcoded UI strings.

Target frameworks: `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`, `net10.0-windows10.0.19041.0`.

## Build commands

Build the library (from the repo root or any consuming app):
```
dotnet build MyToolkit.csproj
```

There is no solution file in this directory. The library is built as part of each consuming app's solution. To verify changes compile cleanly, build a consuming app:
```
dotnet build <consuming-app>.sln
```

There are no tests in this library — validation is done by building and running consuming apps.

## Architecture

### Layer map

| Folder | Purpose |
|--------|---------|
| `ViewModels/` | MVVM base classes (`ToolkitViewModel`, `PaginatedListViewModel<T>`) |
| `Views/` | Page base (`ToolkitPage`) and shared screens (`ErrorReportPage`) |
| `Services/Errors/` | Exception types + `ErrorHandler` (central exception sink) |
| `Services/Navigation/` | `Navigator` — re-entrancy-guarded Shell nav wrapper |
| `Services/Net/` | `ApiService` — JWT-authenticated HTTP client wrapper |
| `Services/Popups/` | `IPopupPresenter` / `PopupPresenter` — all modal dialogs |
| `Services/Time/` | `TimeAgo` — relative-time formatting |
| `Services/` (root) | `ServiceHelper` (locator), `AppLogger` (rolling file log) |
| `Controls/` | Reusable XAML controls (`CardView`) |

### Key base classes

**`ToolkitViewModel`** (`ViewModels/ToolkitViewModel.cs`)
- `ObservableObject` base with `IsBusy`, `ErrorMessage`, `HasError`, `IsNotBusy`
- `RunSafeAsync()` — guarded async execution; routes failures to `ErrorHandler`
- Intentionally carries **no** auth or current-user state — apps subclass and add their own

**`PaginatedListViewModel<TItem>`** (`ViewModels/PaginatedListViewModel.cs`)
- Cursor-based pagination; override `FetchPageAsync()` to adapt app-specific DTOs
- `Items` ObservableCollection, `LoadAsync()` / `LoadMoreAsync()` lifecycle

**`ToolkitPage`** (`Views/ToolkitPage.cs`)
- `ContentPage` base that wires navigation lifecycle to bound `ILifecycleAware` VM
- Handles: iOS safe-area, Android edge-to-edge keyboard insets (Android 15/16 blank-space fix), bottom nav-bar inset
- `DisposeViewModelOnPop` (default `true`) — transient detail pages dispose VM on back; tab-root pages set it to `false`

### Error pipeline

`AppException` / `ApiException` → `ErrorHandler.HandleAsync()` → **Minor** (log only) or **Important** (log + open `ErrorReportPage` modal).

`ApiException` captures the full backend error envelope (`StatusCode`, `ServerCode`, `ServerTraceId`, `ServerStackTrace`). `IErrorTextProvider` supplies all localized strings for the error UI — the toolkit renders nothing hardcoded.

### Services design rules

- **`Navigator`** — always use this, never call `Shell.Current.GoToAsync()` directly. It is re-entrancy-guarded, UI-thread-safe, and never throws.
- **`ApiService`** — configured per-app via `ApiClientOptions` singleton (token keys, refresh endpoint, auth-skip prefixes, JSON options). Handles 401 → refresh → retry transparently.
- **`IPopupPresenter`** — single entry point for every dialog type (toast, alert, confirm, form, option sheet). Registered as a singleton; no manual UXDivers wiring.
- **`AppLogger`** — rolling daily file at `{AppDataDirectory}/logs/app-yyyyMMdd.log`; thread-safe; never throws.
- **`ServiceHelper.Get<T>()`** / **`ServiceHelper.GetSafe<T>()`** — last-resort locator when constructor DI is unavailable (e.g. inside static helpers).

### What belongs here vs. in apps

**Belongs here:** generic MVVM scaffolding, cross-platform UI plumbing (safe areas, keyboard insets), HTTP/error/navigation/popup infrastructure with no brand or auth coupling.

**Stays in consuming apps:** authentication, current-user model, business services, feature ViewModels/Pages, localized string resources, brand colors/styles.

### Extending the toolkit

When adding a new component:
1. It must be **generic** — zero references to app-specific types, strings, or services.
2. Any UI copy must go through an injected provider interface (see `IErrorTextProvider` as the pattern).
3. Register any new services in the consuming app's `MauiProgram.cs`, not here.
4. `ILifecycleAware` is the contract for VM navigation hooks; `ToolkitPage` calls it automatically.
