namespace MyToolkit.Behaviors;

/// <summary>
/// Makes dragging a big view smooth: on Android the view is drawn once into a GPU texture and that texture is moved, instead of redrawing the whole tree (gradients, images, rounded clips) on every frame.
/// Call <see cref="Hold"/> when a drag starts and <see cref="Release"/> when it is over; the texture costs memory while held. Does nothing on other platforms.
/// </summary>
public static class DragLayer
{
    public static void Hold(VisualElement view)
    {
#if ANDROID
        if (view.Handler?.PlatformView is Android.Views.View native)
            native.SetLayerType(Android.Views.LayerType.Hardware, null);
#endif
    }

    public static void Release(VisualElement view)
    {
#if ANDROID
        if (view.Handler?.PlatformView is Android.Views.View native)
            native.SetLayerType(Android.Views.LayerType.None, null);
#endif
    }
}
