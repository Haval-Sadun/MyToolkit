using System.Collections.ObjectModel;

namespace MyToolkit.ViewModels;

/// <summary>One page of a cursor-paginated list: the items plus the cursor for the next page.</summary>
public readonly record struct PageResult<TItem>(IReadOnlyList<TItem> Items, string? NextCursor);

/// <summary>
/// Reusable cursor-paginated list base: owns the <see cref="Items"/> collection, the cursor
/// and has-more state, and the <see cref="LoadAsync"/> / <see cref="LoadMoreAsync"/> flow
/// (guarded by <c>IsBusy</c>). Subclasses implement <see cref="FetchPageAsync"/> (adapting
/// their own service/Result shape into a <see cref="PageResult{TItem}"/>) and may hook
/// <see cref="AfterItemsLoaded"/>, <see cref="OnItemEvicted"/> (e.g. to dispose items), and
/// <see cref="OnLoadError"/>. Carries no auth/domain coupling.
/// </summary>
public abstract partial class PaginatedListViewModel<TItem> : ToolkitViewModel
{
    private string? _cursor;
    private bool _hasMore = true;

    public ObservableCollection<TItem> Items { get; } = new();

    /// <summary>True while another page remains (the last fetch returned a non-empty cursor).</summary>
    public bool HasMore => _hasMore;

    /// <summary>Fetch one page. <paramref name="cursor"/> is null for the first page.</summary>
    protected abstract Task<PageResult<TItem>> FetchPageAsync(string? cursor);

    /// <summary>Hook after the first page lands (e.g. to derive aggregate state).</summary>
    protected virtual void AfterItemsLoaded(IReadOnlyList<TItem> items) { }

    /// <summary>Called for each item removed from <see cref="Items"/> (override to dispose).</summary>
    protected virtual void OnItemEvicted(TItem item) { }

    /// <summary>Called when a fetch throws (override to log / surface).</summary>
    protected virtual void OnLoadError(Exception ex, string operation) { }

    /// <summary>(Re)load the first page, clearing any existing items.</summary>
    protected async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            ClearItems();
            _cursor = null;
            _hasMore = true;

            var page = await FetchPageAsync(null);
            foreach (var item in page.Items) Items.Add(item);

            AfterItemsLoaded(page.Items);
            _cursor = page.NextCursor;
            _hasMore = !string.IsNullOrEmpty(_cursor);
        }
        catch (Exception ex)
        {
            OnLoadError(ex, nameof(LoadAsync));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Append the next page, if any.</summary>
    protected async Task LoadMoreAsync()
    {
        if (IsBusy || !_hasMore) return;
        IsBusy = true;
        try
        {
            var page = await FetchPageAsync(_cursor);
            foreach (var item in page.Items) Items.Add(item);

            _cursor = page.NextCursor;
            _hasMore = !string.IsNullOrEmpty(_cursor);
        }
        catch (Exception ex)
        {
            OnLoadError(ex, nameof(LoadMoreAsync));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Evict (via <see cref="OnItemEvicted"/>) and clear all items.</summary>
    protected void ClearItems()
    {
        foreach (var item in Items) OnItemEvicted(item);
        Items.Clear();
    }

    public override void Dispose()
    {
        ClearItems();
        base.Dispose();
    }
}
