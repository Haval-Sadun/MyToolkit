# architecture.md — MyToolkit (shared MAUI library)

- **Version:** 3.0 (best-of-both: KurdishConnect + SyriaBet)
- **Status:** DRAFT (awaiting approval)
- **Based on:** owner brief + reuse rules in `~/.claude/CLAUDE.md` + explorer inventory of both apps
- **Donors:** `Haval/Kurdish_Community/mobile/KurdishConnect` **and** `Haval/SyriaBet/frontend/{SyriaBet.Mobile,SyriaBet.Admin}`
- **Date:** 2026-06-16

> **Design stance (owner-directed):** the shared library must be the **best possible version**.
> For each capability we take the superior implementation from whichever app has it, generalize
> it, and put the **concrete working code** in the toolkit (not just interfaces). Components that
> already exist **identically in both SyriaBet apps** are proven-reusable and move with confidence.
> Hard rule unchanged: nothing **business-specific** (auth flows, posts, groups, betting, named
> app screens/popups, localized strings, domain models) enters MyToolkit. Where a generic type
> would otherwise touch app specifics, a small inversion contract or options object lets the app
> inject them.

---

## 1. Complexity Assessment

**SMALL library, MEDIUM integration.** One class library, no backend. The real work is (a) merging
two donors into one best-of-breed surface and (b) decoupling each moved type from app specifics
(localized text, token-key names, domain models) via small contracts/options. Three consumers,
all net10, same MVVM/popup stack.

---

## 2. Best-of-both sourcing (what moves, from where)

**Lead donor per component, with the working code.**

| Capability | Source (best version) | Notes / coupling to break |
|---|---|---|
| **Error severity** `ErrorSeverity` | SyriaBet (identical in both) | Pure enum. Move as-is. |
| **`AppException`** (UserMessage + Severity) | SyriaBet | Generic. Default `UserMessage` text comes from `IErrorTextProvider`. |
| **`ApiException`** (status, method, endpoint, server code/trace_id/stack) | SyriaBet | Generic; envelope parsing lives in `ApiService`. |
| **`ErrorHandler.HandleAsync(ex, severity?, context?)`** | SyriaBet | Logs always; on `Important` shows modal `ErrorReportPage`. Decouple hardcoded Arabic title/messages → `IErrorTextProvider`. |
| **`ErrorReport`** (data) + **`ErrorReportPage`** (modal dump + copy) | SyriaBet | Page is generic UI; localize its static labels via `IErrorTextProvider`/resources. |
| **`AppLogger`** (rolling daily file, levels, thread-safe) | SyriaBet | Strictly better than Kurdish's single-file logger. Move as-is. |
| **`ServiceHelper`** (static DI locator + `GetSafe<T>`) | SyriaBet | Generic. Move as-is. |
| **`ApiService`** (`GetAsync/GetListAsync/PostAsync/PatchAsync/PutAsync/DeleteAsync<T>`, Bearer attach, 401-refresh, DRF snake_case + AllowReadingFromString, pagination unwrap, envelope→`ApiException`) | SyriaBet | Parameterize via `ApiClientOptions` (HttpClient name, access/refresh `SecureStorage` keys, auth-skip endpoints, optional `JsonSerializerOptions`). |
| **Base VM → `ToolkitViewModel`** | **Merge** SyriaBet + Kurdish | SyriaBet: `IsBusy/IsNotBusy/Title/ErrorMessage/HasError`, `RunSafeAsync(action, errorMsg?, severity?, context?)`. Kurdish: lifecycle hooks (`OnAppearing/OnDisappearing/OnNavigatedTo/From`), `IDisposable`. Implements `ILifecycleAware`. **No auth/profile** (stays in app). |
| **`Navigator`** (static, re-entrancy-guarded Shell wrapper) | Kurdish | SyriaBet uses raw Shell — both upgrade to this. |
| **Base Page → `ToolkitPage`** (safe-area + Android keyboard-inset handling) | Kurdish | SyriaBet has no base page; both gain it. Drives VM via `ILifecycleAware`. |
| **`AInputPopup<T>`** | Kurdish | UXDivers + IME handling. |
| **`PaginatedListViewModel<TItem>`** (cursor paging skeleton) | Kurdish (generalized) | Post/realtime logic stays in Kurdish's subclass. |
| **Popups facade** `IPopupPresenter`/`PopupPresenter`/`PopupOption`/`StyledOptionSheetItem` | Either (identical; SyriaBet ported from Kurdish) | Move wholesale. |
| **`Result<T>`**, **`PagedResponse<T>`** | Kurdish | Generic DTOs the paging base + ApiService use. |
| **Generic converters**: `InvertedBoolConverter`, `IntToBoolConverter`, `EqualToZeroConverter`, `EqualToOneConverter`, `StringNotEmptyConverter` | Both (de-dup) | Currently duplicated across apps (`Admin*` prefixes). Consolidate one generic set. |
| **`TimeAgo`** | Kurdish | Pure. |
| **Generic `CardView`** (+ `CustomEntry` if generic) | New / Kurdish | Business-agnostic UI shells; app cards compose them. |
| **Platform helpers** (emulator/local-IP URL resolution, font registration) | SyriaBet | Optional `MauiAppBuilder` extension; keep app-specific base URLs in the app. |

**Stays app-specific (never moves):** `AuthService` (role/auth logic), `BettingService`, all domain
models (Bet/Match/Wallet/User/Group/Post…), business converters (BetStatus/DepositStatus/GroupStatus),
named popups & pages, Kurdish's `OpenProfile`, every app's localized strings and base URLs.

---

## 3. Inversion contracts & options (the only new abstractions)

1. **`IErrorTextProvider`** — `Title`, `Generic`, `Network`, `Timeout` (localized strings).
   *Why:* the toolkit `ErrorHandler`/`ErrorReportPage`/`AppException` must show localized text without
   knowing any app's resource keys. Each app implements it over its localization (KurdishConnect resx;
   SyriaBet Arabic resources).

2. **`IApiError`** — `string Message { get; }`.
   *Why:* lets `ErrorHandler`/logger accept an API error without depending on a concrete model.

3. **`ILifecycleAware`** — `OnAppearing/OnDisappearing/OnNavigatedTo/OnNavigatedFrom(NavigationDirection)`.
   *Why:* `ToolkitPage` drives VM lifecycle without referencing a concrete VM. `ToolkitViewModel` implements it.

4. **`ApiClientOptions`** (POCO) — `HttpClientName`, `AccessTokenKey`, `RefreshTokenKey`,
   `AuthSkipEndpoints` (login/register/refresh/logout), optional `JsonSerializerOptions` (defaults to
   DRF snake_case + `AllowReadingFromString`).
   *Why:* the single divergence between the two SyriaBet `ApiService` copies was token-key + client
   names. This options object collapses them into one shared implementation; each app registers its own.

No `IAppLogger` interface (the concrete `AppLogger` is itself generic — apps depend on the class).

---

## 4. Library structure

```
Haval/Shared/MyToolkit/
├── MyToolkit.csproj
├── ViewModels/
│   ├── ToolkitViewModel.cs           # merged: IsBusy/IsNotBusy/Title/ErrorMessage/HasError,
│   │                                 #   RunSafeAsync, lifecycle hooks, IDisposable, ErrorHandler
│   ├── PaginatedListViewModel.cs     # generic<TItem> cursor paging skeleton
│   ├── ILifecycleAware.cs
│   ├── IViewModelFactory.cs
│   └── ViewModelFactory.cs
├── Views/
│   ├── ToolkitPage.cs                # safe-area + Android keyboard insets (Kurdish)
│   ├── AInputPopup.cs                # AInputPopup<T> (Kurdish)
│   └── ErrorReportPage.xaml(.cs)     # modal error dump (SyriaBet)
├── Services/
│   ├── AppLogger.cs                  # rolling daily file logger (SyriaBet)
│   ├── ServiceHelper.cs              # static DI locator (SyriaBet)
│   ├── Errors/
│   │   ├── ErrorSeverity.cs
│   │   ├── AppException.cs
│   │   ├── ApiException.cs
│   │   ├── ErrorReport.cs
│   │   ├── ErrorHandler.cs           # HandleAsync; modal on Important
│   │   ├── IErrorTextProvider.cs
│   │   └── IApiError.cs
│   ├── Net/
│   │   ├── ApiService.cs             # generic HTTP client (SyriaBet), parameterized
│   │   ├── ApiClientOptions.cs
│   │   ├── Result.cs                 # Result<T>
│   │   └── PagedResponse.cs          # PagedResponse<T>
│   ├── Popups/
│   │   ├── IPopupPresenter.cs
│   │   ├── PopupPresenter.cs
│   │   ├── PopupOption.cs
│   │   └── StyledOptionSheetItem.cs
│   ├── Navigation/
│   │   └── Navigator.cs              # re-entrancy-guarded Shell wrapper (Kurdish)
│   └── Time/
│       └── TimeAgo.cs
├── Controls/
│   ├── CardView.xaml(.cs)            # generic card shell
│   └── CustomEntry.xaml(.cs)         # if proven generic
└── Converters/
    ├── InvertedBoolConverter.cs
    ├── IntToBoolConverter.cs
    ├── EqualToIndexConverter.cs      # generalizes EqualToZero/One/Two (param = index)
    └── StringNotEmptyConverter.cs
```

- **Root namespace:** `MyToolkit`, folder-matching child namespaces (`MyToolkit.ViewModels`,
  `MyToolkit.Views`, `MyToolkit.Services`, `MyToolkit.Services.Errors`, `MyToolkit.Services.Net`,
  `MyToolkit.Services.Popups`, `MyToolkit.Services.Navigation`, `MyToolkit.Services.Time`,
  `MyToolkit.Controls`, `MyToolkit.Converters`).
- **Naming:** `Toolkit`-prefixed bases (`ToolkitViewModel`, `ToolkitPage`); other types keep plain
  names. Each app keeps thin `AViewModel : ToolkitViewModel` / `APage : ToolkitPage` (Kurdish) and
  `BaseViewModel : ToolkitViewModel` (SyriaBet) so existing code barely changes.

---

## 5. Per-app integration

**KurdishConnect:** `AViewModel : ToolkitViewModel` keeps `AuthService`, `CurrentUser`, `UserChanged`,
`OpenProfile`. Implements `IErrorTextProvider` over its resx; `ApiError : IApiError`. Gains the richer
error handler + report page, the better logger, ServiceHelper, and (optionally) the generic ApiService.

**SyriaBet.Mobile + Admin:** `BaseViewModel : ToolkitViewModel` (drops the duplicated `RunSafeAsync`/
error wiring — now inherited). Each implements `IErrorTextProvider` over its Arabic resources, marks its
API error `: IApiError`, registers `ApiClientOptions` with its own token keys/client name. Both delete
their duplicated `ErrorHandler/AppLogger/ServiceHelper/ApiException/ErrorReport(Page)/ApiService` and the
duplicated generic converters, and gain `Navigator` + `ToolkitPage` (safe-area/keyboard) they didn't have.

Each app registers in `MauiProgram.cs`: `AppLogger`, `ErrorHandler`, `IPopupPresenter→PopupPresenter`
(where used), `IViewModelFactory→ViewModelFactory`, its `IErrorTextProvider`, and `ApiClientOptions`.

---

## 6. Referencing & versions

`<ProjectReference Include="...\Shared\MyToolkit\MyToolkit.csproj" />` now; NuGet later (§7).
Versions aligned (TASK-01, done): Maui.Controls 10.0.60, CommunityToolkit.Maui 14.2.0,
CommunityToolkit.Mvvm 8.4.2, UXDivers.Popups.Maui 0.9.4.

## 7. NuGet-later path

Stabilize API → `IsPackable`/`PackageId=MyToolkit`/`Version` → `dotnet pack` → private feed →
swap `ProjectReference` for `PackageReference` → SemVer. Stay on ProjectReference until the API settles.

---

## 8. Migration sequence (see tasks.md for the task breakdown)

Build the toolkit best-of-breed, verifying each component against **its lead donor first**, then
onboard the other app(s). Order chosen so dependencies land before dependents and every step ends
with a green build in every referencing app.

1. Align versions (done).
2. Create MyToolkit project (done).
3. Zero-coupling moves: `Result<T>`, `PagedResponse<T>`, `TimeAgo`, generic converters, `AInputPopup<T>`, Popups facade.
4. `AppLogger` (SyriaBet) + `ServiceHelper` (SyriaBet).
5. Contracts: `IErrorTextProvider`, `IApiError`, `ILifecycleAware`.
6. Error stack (SyriaBet): `ErrorSeverity`, `AppException`, `ApiException`, `ErrorReport`, `ErrorReportPage`, `ErrorHandler` (decoupled via `IErrorTextProvider`).
7. `Navigator` (Kurdish).
8. `ApiService` + `ApiClientOptions` (SyriaBet, parameterized).
9. `ToolkitViewModel` (merged) + `ToolkitPage` (Kurdish).
10. `PaginatedListViewModel<TItem>` (Kurdish) + `ViewModelFactory`.
11. Generic `Controls` (CardView, CustomEntry).
12. Onboard all consumers: thin base subclasses, contracts impls, DI registrations; delete duplicated app copies.

Per-task verification = green MAUI build (android for Mobile/Kurdish, windows for Admin) of every app referencing the toolkit at that point.

## 9. Open Questions
*(empty)*

## 10. Conflicts
*(empty — the earlier "two error handlers" tension is resolved: SyriaBet's is canonical; Kurdish upgrades to it.)*

---

## Approval checklist
- [ ] Best-of-both sourcing table (§2) approved — SyriaBet leads error/logging/HTTP/ServiceHelper; Kurdish leads navigation/page/paging/popups; base VM merged.
- [ ] KurdishConnect being **upgraded** to SyriaBet's richer error handler + report page is desired.
- [ ] Contracts + `ApiClientOptions` (§3) acceptable.
- [ ] Structure & naming (§4) approved.
- [ ] Expanded migration sequence (§8) → tasks.md.
