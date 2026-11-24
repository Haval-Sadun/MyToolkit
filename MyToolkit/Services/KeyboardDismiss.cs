namespace MyToolkit.Services;

/// <summary>Hides the soft keyboard wherever the focus is, sheets and popups included. Does nothing off Android.</summary>
public static class KeyboardDismiss
{
    public static void Hide()
    {
#if ANDROID
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity?.CurrentFocus is not { } focus)
            return;

        if (activity.GetSystemService(Android.Content.Context.InputMethodService) is Android.Views.InputMethods.InputMethodManager manager)
            manager.HideSoftInputFromWindow(focus.WindowToken, Android.Views.InputMethods.HideSoftInputFlags.None);

        focus.ClearFocus();
#endif
    }
}
