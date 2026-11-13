namespace MyToolkit.Services.Popups;

/// <summary>Implemented by a custom sheet's content to pin a footer (primary buttons) below the scroll area, above the keyboard.</summary>
public interface ISheetFooterProvider
{
    View? Footer { get; }
}
