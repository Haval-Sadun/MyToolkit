using CommunityToolkit.Mvvm.ComponentModel;
using MyToolkit.Services;
using MyToolkit.Services.Errors;

namespace MyToolkit.ViewModels;

/// <summary>
/// Shared base view-model: busy/error state, a guarded <see cref="RunSafeAsync"/> that
/// routes failures to the central <see cref="ErrorHandler"/>, navigation lifecycle hooks
/// (<see cref="ILifecycleAware"/>), and <see cref="IDisposable"/>. Intentionally carries
/// NO authentication / current-user concept — that belongs in each app's own subclass.
/// </summary>
public abstract partial class ToolkitViewModel : ObservableObject, ILifecycleAware, IDisposable
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasError;

    public bool IsNotBusy => !IsBusy;

    protected void SetError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }

    protected void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }

    // The [ObservableProperty] hook for IsBusy is generated here, so this is the only
    // place it can be wired. Derived VMs override IsBusyChanged to react.
    partial void OnIsBusyChanged(bool value) => IsBusyChanged(value);
    protected virtual void IsBusyChanged(bool value) { }

    /// <summary>
    /// Runs <paramref name="action"/> guarded by IsBusy. On failure it both shows an
    /// inline message on the screen AND routes the exception to the central
    /// <see cref="ErrorHandler"/> — which always logs it and, for important exceptions,
    /// opens the full error-report screen.
    /// </summary>
    /// <param name="errorMsg">Optional override for the inline message.</param>
    /// <param name="severity">Force a severity; otherwise it's inferred.</param>
    /// <param name="context">Label for the log (defaults to the view-model name).</param>
    protected async Task RunSafeAsync(Func<Task> action, string? errorMsg = null, ErrorSeverity? severity = null, string? context = null)
    {
        if (IsBusy) 
            return;
        ClearError();
        IsBusy = true;
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // Navigation/teardown cancellation — nothing to report.
        }
        catch (Exception ex)
        {
            SetError(errorMsg ?? FriendlyMessage(ex));
            try
            {
                await ServiceHelper.Get<ErrorHandler>()
                    .HandleAsync(ex, severity, context ?? GetType().Name);
            }
            catch (Exception handlerEx)
            {
                System.Diagnostics.Debug.WriteLine($"ErrorHandler failed: {handlerEx}");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Short message suitable for inline display. App-specific copy comes from
    /// the registered <see cref="IErrorTextProvider"/> (falls back to the raw message).</summary>
    private static string FriendlyMessage(Exception ex)
    {
        var text = ServiceHelper.GetSafe<IErrorTextProvider>();
        return ex switch
        {
            ApiException api => api.Message,
            AppException app => app.UserMessage,
            HttpRequestException => text?.NetworkError ?? ex.Message,
            _ => text?.UnexpectedError ?? ex.Message
        };
    }

    // ILifecycleAware — overridable navigation hooks (no-ops by default).
    public virtual void OnAppearing() { }
    public virtual void OnDisappearing() { }
    public virtual void OnNavigatedTo(NavigationDirection direction) { }
    public virtual void OnNavigatedFrom(NavigationDirection direction) { }

    public virtual void Dispose() { }
}
