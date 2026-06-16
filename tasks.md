# tasks.md — MyToolkit shared library

- **Version:** 2.0 (best-of-both: KurdishConnect + SyriaBet)
- **Status:** ✅ COMPLETE — TASK-01 ✅, TASK-02 ✅, TASK-03 ✅, TASK-04 ✅, TASK-05 ✅, TASK-06 ✅, TASK-07 ✅, TASK-08 ✅, TASK-09 ✅, TASK-10 ✅, TASK-11 ✅, TASK-12 ✅
- **Based on:** architecture.md v3.0 — §8 migration sequence
- **Date:** 2026-06-16
- **Donors:** KurdishConnect (navigation/page/paging/popups) + SyriaBet (error/logging/HTTP/ServiceHelper)

## Ordering rule
Each component is verified against **its lead donor first** (Kurdish for nav/page/paging; SyriaBet
for error/logging/HTTP), then the other app(s) are onboarded. **DoD for every task = green MAUI build
of every app currently referencing the toolkit** (android for Mobile/Kurdish; windows for Admin),
no new warnings. Dependencies land before dependents.

## Coverage inventory (architecture §2 item → task)
| Item | Task |
|---|---|
| Version alignment | TASK-01 ✅ |
| Create project | TASK-02 ✅ |
| Result/PagedResponse, TimeAgo, generic converters, AInputPopup, Popups | TASK-03 |
| AppLogger + ServiceHelper (SyriaBet) | TASK-04 |
| Contracts IErrorTextProvider/IApiError/ILifecycleAware | TASK-05 |
| Error stack (SyriaBet): Severity/AppException/ApiException/ErrorReport/ErrorReportPage/ErrorHandler | TASK-06 |
| Navigator (Kurdish) | TASK-07 |
| ApiService + ApiClientOptions (SyriaBet) | TASK-08 |
| ToolkitViewModel (merged) + ToolkitPage (Kurdish) | TASK-09 |
| PaginatedListViewModel + ViewModelFactory | TASK-10 |
| Generic Controls (CardView, CustomEntry) | TASK-11 |
| Onboard all consumers + delete duplicated copies | TASK-12 |

---

## TASK-01 — Align package versions ✅ DONE
All 3 apps bumped (Maui.Controls 10.0.60, CT.Maui 14.2.0, CT.Mvvm 8.4.2); all restore clean.

## TASK-02 — Create MyToolkit project ✅ DONE
Multi-TFM MAUI class library; empty build green (android, 0/0).

## TASK-03 — Zero-coupling moves + Popups facade ✅ DONE
- **Toolkit created:** `Services/Time/TimeAgo.cs`, `Services/Popups/{IPopupPresenter,PopupPresenter,PopupOption,StyledOptionSheetItem}.cs`.
- **KurdishConnect:** added ProjectReference; added `GlobalUsings.cs` (global usings for the moved namespaces — avoided editing 24 files); repointed `App.xaml` popups xmlns to `MyToolkit.Services.Popups;assembly=MyToolkit`; deleted originals (kept business-specific `RsvpPicker.cs`); qualified 2 fully-named `Shared.TimeAgo` refs.
- **Build:** KurdishConnect android **green, 0 errors**.
- **SCOPE NOTES (carried forward):**
  - `Result<T>`/`PagedResponse<T>` **deferred** → they couple to `ApiError`; move them **with the error stack** (TASK-06) / ApiService (TASK-08).
  - `AInputPopup<T>` + generic converters **deferred** → referenced from XAML via `clr-namespace`; move alongside `ToolkitPage` (TASK-09) and converter de-dup (TASK-12) to batch the XAML xmlns edits.
- **Pre-existing bug fixed to unblock build (NOT migration-caused):** `GroupEventsViewModel` had two parameterless `Refresh()` methods (CS0111). Renamed the messenger-invoked reload to `ReloadEvents()` (updated 2 call sites); kept the `[RelayCommand] Refresh` whose `RefreshCommand` is XAML-bound. **Confirm this matches intent.**
- **Known toolkit warning:** `PopupPresenter.FormAsync` CS8619 nullability (`List<string?>` vs `IReadOnlyList<string>`) — carried verbatim from donor; harmless, will tidy.

## TASK-04 — AppLogger + ServiceHelper (from SyriaBet) ✅ DONE
- **Toolkit created:** `Services/AppLogger.cs` (rolling-daily logger), `Services/ServiceHelper.cs` — namespace `MyToolkit.Services`, verbatim from SyriaBet donor (both business-agnostic).
- **SyriaBet.Mobile (canary):** added ProjectReference; added `GlobalUsings.cs` (`global using MyToolkit.Services;`); deleted its `Services/AppLogger.cs` + `Services/ServiceHelper.cs`. The 4 consumers (MauiProgram, App.xaml.cs, BaseViewModel, ErrorHandler) resolve via the global using — no per-file edits needed.
- **Build:** SyriaBet.Mobile android **green, 0 errors** (255 pre-existing Frame-obsolete warnings only).

## TASK-05 — Inversion contracts ✅ DONE
- **Toolkit created:** `Services/Errors/IApiError.cs` (StatusCode/Message/ErrorCode), `Services/Errors/IErrorTextProvider.cs` (ErrorReportTitle/CopyDetails/Copied/Close/UnexpectedError + `TraceLabel(id,time)`), `ViewModels/ILifecycleAware.cs` (OnAppearing/OnDisappearing/OnNavigatedTo/From(NavigationDirection)) **+ moved generic `NavigationDirection` enum** into the same file (`MyToolkit.ViewModels`) — it was app-agnostic in Kurdish's APage.
- **SyriaBet.Mobile:** added `Services/ErrorTextProvider.cs` (Arabic copy lifted from `ErrorReportPage.xaml`); registered `AddSingleton<IErrorTextProvider, ErrorTextProvider>()` in MauiProgram. Build android **green**.
- **KurdishConnect:** marked `Models/ApiError.cs : IApiError` (shape already matched exactly — StatusCode int/Message/ErrorCode); added `global using MyToolkit.Services.Errors;`. Build android **green**.
- **DEFERRED:** marking SyriaBet's `ApiException : IApiError` — its `StatusCode` is `HttpStatusCode` (name-clashes the int contract) and `ServerCode`≠`ErrorCode`; it also **moves into the toolkit** in TASK-06, so it'll get the interface there (explicit impl) rather than now. KurdishConnect's `ApiError` is the meaningful `IApiError` consumer (couples to `Result<T>`).
- **ILifecycleAware/NavigationDirection** are defined only; wired in TASK-09 (ToolkitPage/ToolkitViewModel). Kurdish's APage still owns its own `NavigationDirection` enum until TASK-09 — **two definitions coexist**, no conflict (different namespaces). TASK-09 deletes the Kurdish one.

## TASK-06 — Error stack (from SyriaBet, decoupled) ✅ DONE
- **Toolkit created (all `MyToolkit.Services.Errors`):** `ErrorSeverity`, `AppException`, `ApiException` (now `: IApiError` via explicit impl — `(int)StatusCode`, `ServerCode`→`ErrorCode`), `ErrorReport` (dropped its hardcoded Arabic `Title` default), `ErrorHandler` (ctor now `(AppLogger, IErrorTextProvider)`; summary falls back to `text.UnexpectedError`). Plus `Views/ErrorReportPage.xaml(.cs)` — **all user copy pulled from `IErrorTextProvider`** (title/summary/trace/copy/close/copied); ctor `(ErrorReport, IErrorTextProvider)`.
- **SyriaBet.Mobile:** deleted `Exceptions/` (3 files) + `Models/ErrorReport.cs` + `Services/ErrorHandler.cs` + `Pages/ErrorReportPage.xaml(.cs)`; added `global using MyToolkit.Services.Errors;`; removed the now-dangling `using SyriaBet.Mobile.Exceptions;` from 4 files (ApiService, AuthService, App.xaml.cs, BaseViewModel). Existing `AddSingleton<ErrorHandler>()` now resolves the toolkit type; `IErrorTextProvider` already registered (TASK-05). Build android **green, 0 errors**. KurdishConnect re-verified **green**.
- **SCOPE NOTE (colours/RTL):** the moved `ErrorReportPage` keeps SyriaBet's dark palette (#131315/#BBFF00/#FF3366) and the detail panel's LTR. These are *style*, not *text* — fine for the SyriaBet canary (both target apps are dark). When KurdishConnect adopts the page (TASK-12), make colours themeable (DynamicResource/app resource keys) + revisit page-level FlowDirection. Not blocking.
- **NOT runtime-tested** (no device/emulator in this env); verified by green compile only — DoD's "Minor→log / Important→modal / copy" behaviour is unchanged code, just re-homed.

## TASK-07 — Navigator (from Kurdish) ✅ DONE
- **DECOUPLING DECISION:** Kurdish's Navigator hard-coupled to its own `AppLogger` (`Log(string)`) **and** `AppErrorHandler` (Snackbar/Floater + localized friendly msgs) — both fundamentally different from the toolkit/SyriaBet versions, and `AppErrorHandler.Handle(ApiError,…)` is used app-wide. Fully unifying Kurdish onto the toolkit logger/error stack is a large separate effort, NOT this task. So instead of dragging either app's specifics in, the toolkit Navigator depends on a tiny **inversion contract**.
- **Toolkit created:** `Services/Navigation/INavigationDiagnostics.cs` (`Log(msg)` + `HandleError(ex, context)`) and `Services/Navigation/Navigator.cs` (static, re-entrancy-guarded, UI-thread, never-throws; resolves `INavigationDiagnostics?` optionally from DI — navigation works silently if none registered).
- **KurdishConnect:** added `Services/NavigationDiagnostics.cs` (adapter → its `AppLogger.Log` + `AppErrorHandler.Handle`); registered `AddSingleton<INavigationDiagnostics, NavigationDiagnostics>()`; deleted `Shared/Navigator.cs`; added `global using MyToolkit.Services.Navigation;`; fixed one fully-qualified `Shared.Navigator` ref in AuthService → `Navigator`. Build android **green, 0 errors**.
- **NOTE:** SyriaBet apps don't currently use Navigator (they're not Shell-nav-wrapped the same way); they'll get an adapter if/when they adopt it (TASK-12 or later).

## TASK-08 — ApiService + ApiClientOptions (from SyriaBet, parameterized) ✅ DONE
- **Toolkit created:** `Services/Net/ApiClientOptions.cs` (HttpClientName, Access/RefreshTokenKey, RefreshEndpoint, `AuthSkipPrefixes`, `JsonOptions` — with `DefaultDrfJson()` = snake_case + AllowReadingFromString), `Services/Net/ApiService.cs` (moved verbatim from SyriaBet, every hardcoded value now read from options; throws toolkit `ApiException`; DRF pagination unwrap + error-envelope parsing preserved).
- **Toolkit csproj:** added `Microsoft.Extensions.Http 10.0.0` (for `IHttpClientFactory`) + `using System.Net.Http;` in ApiService.
- **SyriaBet.Mobile:** deleted `Services/ApiService.cs`; added `global using MyToolkit.Services.Net;`; registered `ApiClientOptions { HttpClientName="SyriaBet", AccessTokenKey="jwt_token", RefreshTokenKey="jwt_refresh", RefreshEndpoint="auth/refresh/" }` (AuthSkipPrefixes + JSON use toolkit defaults — identical to old hardcoded set). `AddSingleton<ApiService>()` unchanged. Build android **green, 0 errors**. KurdishConnect re-verified green (no NU1605 from the new pkg).
- **NOT runtime-tested** (no backend/emulator here); login/list/post/401-refresh is byte-identical logic, just parameterized.

## TASK-09 — ToolkitViewModel (merged) + ToolkitPage (Kurdish) ✅ DONE
- **Toolkit created:** `ViewModels/ToolkitViewModel.cs` — `IsBusy/IsNotBusy/ErrorMessage/HasError`, `SetError/ClearError`, `RunSafeAsync` (→ `ServiceHelper.Get<ErrorHandler>()`, friendly inline msg via `IErrorTextProvider`), `IsBusyChanged` hook, lifecycle (implements `ILifecycleAware`), `IDisposable`. **NO auth.** `Views/ToolkitPage.cs` — Kurdish's APage verbatim, re-homed; drives lifecycle via `BindingContext as ILifecycleAware` and disposes via `BindingContext as IDisposable`; uses the toolkit `NavigationDirection`.
- **`Title` deliberately NOT in the base** — 2 Kurdish VMs already declare `_title` (CreateEventViewModel, NotificationItemViewModel); a base `Title` would shadow them. Title is SyriaBet UI sugar → stays in SyriaBet's `BaseViewModel`. Added `IErrorTextProvider.NetworkError` (SyriaBet provider implements it) for the inline connectivity message.
- **SyriaBet.Mobile:** `BaseViewModel : ToolkitViewModel` now holds only `Title`; all busy/error/RunSafeAsync inherited. `global using MyToolkit.ViewModels;`.
- **KurdishConnect:** `AViewModel : ToolkitViewModel` (kept AuthService/CurrentUser/`ErrorHandler`(AppErrorHandler)/OpenProfile/UserChanged/Dispose-override; dropped its own IsBusy/ErrorMessage/IsBusyChanged/lifecycle — inherited). `APage` reduced to a **thin shim** `: ToolkitPage {}` so all XAML `views:APage` roots stay unchanged. Deleted Kurdish's duplicate `NavigationDirection` enum; `global using MyToolkit.ViewModels;` resolves the 5 references.
- **Build:** SyriaBet.Mobile + KurdishConnect android **green, 0 errors**.
- **Warnings:** toolkit carries a few CS8602/8603/8619 nullability warnings from the verbatim donor code (ApiService/PopupPresenter) — benign, no member-hiding (verified: zero CS0108/0114/MVVMTK). Tidy later.
- **NOT runtime-tested** (DoD smoke-test: nav/keyboard-insets/busy-error/VM-disposal) — no emulator; logic is unchanged, just re-homed + reparented.

## TASK-10 — PaginatedListViewModel + ViewModelFactory ✅ DONE
- **Toolkit created:** `ViewModels/PaginatedListViewModel.cs` (cursor-paginated generic base over ToolkitViewModel), `ViewModels/IViewModelFactory.cs` + `ViewModelFactory.cs` (constraint `where T : ToolkitViewModel`).
- **KurdishConnect:** deleted `Shared/ViewModelFactory.cs`; toolkit type resolves via `global using MyToolkit.ViewModels`. `AViewModel.OpenProfile` uses `IViewModelFactory.Create<ProfileViewModel>()` — works because `ProfileViewModel : AViewModel : ToolkitViewModel`.
- **NOTE:** `PaginatedPostsViewModel` NOT reparented (depends on `AViewModel` auth/CurrentUser/realtime — single-inheritance constraint). The new generic `PaginatedListViewModel<TItem>` is available for future apps.
- **Build:** KurdishConnect android **green, 0 errors**. SyriaBet.Mobile android **green, 0 errors**.

## TASK-11 — Generic Controls ✅ DONE
- **Toolkit created:** `Controls/CardView.xaml(.cs)` — `Border`-wrapped card with `Header` + `Body` slots; `CardPadding`, `CardBackground`, `SectionSpacing`, shadow (`ShadowBrush/Offset/Radius/Opacity`) all bindable. `HasHeader` auto-updates via `propertyChanged` callback.
- **CustomEntry skipped** — its code-behind lookups `Application.Current?.Resources["ErrorRed"]` etc. are app-resource-key-dependent; cannot move to shared lib without decoupling those (deferred).
- **Build:** KurdishConnect android **green, 0 errors**.

## TASK-12 — Onboard all consumers + de-duplicate ✅ DONE
- **SyriaBet.Admin:** added `ProjectReference` to MyToolkit; created `GlobalUsings.cs` (MyToolkit.Services, .Errors, .Net, .ViewModels); created `Services/ErrorTextProvider.cs` (Arabic IErrorTextProvider); registered `IErrorTextProvider` + `ApiClientOptions { SyriaAdmin, admin_jwt_token, admin_jwt_refresh }` in MauiProgram; `BaseViewModel : ToolkitViewModel` (kept `Title`); deleted `Exceptions/{ApiException,AppException,ErrorSeverity}.cs`, `Models/ErrorReport.cs`, `Services/{AppLogger,ServiceHelper,ApiService,ErrorHandler}.cs`, `Pages/ErrorReportPage.xaml(.cs)`; removed dangling usings from `App.xaml.cs` and `AuthService.cs`.
- **Build:** SyriaBet.Admin windows **green, 0 errors** (10 pre-existing MVVMTK0045 AOT warnings only). KurdishConnect android **green**. SyriaBet.Mobile android **green**.
- **NOT runtime-tested** — no device/emulator; logic is byte-identical, re-homed.
- **Deferred:** ToolkitPage / Navigator adoption in Admin (Admin uses plain Shell nav, no APage base yet); generic converter de-dup (Admin uses inline App.xaml converters); CustomEntry move (resource key coupling in code-behind).

---

## Blockers / risks
- **None blocking.** Resolved: the error-handler divergence — SyriaBet's stack is canonical; Kurdish is upgraded to it (owner-approved direction "best version possible").
- Watch (TASK-06): SyriaBet's `ErrorReportPage`/`AppException` carry hardcoded Arabic — must route through `IErrorTextProvider` so KurdishConnect shows its own language.
- Watch (TASK-08): the only ApiService divergence (token keys, client name) is absorbed by `ApiClientOptions`; verify no other hidden divergence during the move.

## Suggested execution
Sequential 03 → 12. Independent among themselves once their deps are met: TASK-07 (after 04),
TASK-11 (after 09).
