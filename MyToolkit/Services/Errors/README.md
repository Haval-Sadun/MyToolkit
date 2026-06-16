# MyToolkit · Services/Errors

The error model and the central exception sink. Three files:

| File | Types | Role |
|------|-------|------|
| `Exceptions.cs` | `ErrorSeverity`, `IApiError`, `ApiException`, `AppException` | the error data model + the app-agnostic contract |
| `ErrorHandler.cs` | `ErrorReport`, `ErrorHandler` | the central sink that logs and (for `Important`) shows the report screen |
| `IErrorTextProvider.cs` | `IErrorTextProvider` | app-implemented localized copy for the error UI |

## How an end app wires it up

`ErrorHandler` depends on `AppLogger` (toolkit) and `IErrorTextProvider` (yours). Register
all three in `MauiProgram.cs`:

```csharp
builder.Services.AddSingleton<AppLogger>();
builder.Services.AddSingleton<IErrorTextProvider, AppErrorText>();  // your localized strings
builder.Services.AddSingleton<ErrorHandler>();
```

Implement `IErrorTextProvider` once per app, pulling from your localization layer:

```csharp
public sealed class AppErrorText : IErrorTextProvider
{
    private readonly ILocalizationService _l;
    public AppErrorText(ILocalizationService l) => _l = l;

    public string ErrorReportTitle => _l.Get("Error_ReportTitle");
    public string CopyDetails      => _l.Get("Error_CopyDetails");
    public string Copied           => _l.Get("Error_Copied");
    public string Close            => _l.Get("Common_Close");
    public string UnexpectedError  => _l.Get("Error_Generic");
    public string NetworkError     => _l.Get("Error_Network");
    public string TraceLabel(string id, string time) => $"{id} • {time}";
}
```

## Using it from app code

**Send any caught exception to the sink** — severity is auto-classified
(`ApiException`/`AppException` carry their own; network/cancel = `Minor`; everything
else = `Important` → opens the report screen):

```csharp
try { await DoWork(); }
catch (Exception ex) { await _errorHandler.HandleAsync(ex, context: "sync feed"); }
```

**Raise an app-level failure** you want surfaced prominently:

```csharp
throw new AppException(_text.Get("Profile_SaveFailed"), ErrorSeverity.Important);
```

**Read an API failure generically** via `IApiError` (e.g. from a `Result` failure) without
caring whether it came from a parsed HTTP response (`ApiException`) or a transport fault
(`SimpleApiError`):

```csharp
void Handle(IApiError error) =>
    _logger.LogWarning($"[{error.StatusCode}] {error.ErrorCode}: {error.Message}");
```

> `ApiException` is built **only** by `ApiExceptionFactory` in `Services/Net`. App code
> catches it or reads it through `Result.Error` — it never constructs one.
