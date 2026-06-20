using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MyToolkit.Models;

/// <summary>
/// ObservableCollection that supports batch insert/append with a single CollectionChanged
/// notification, avoiding the N-notification jank of individual Insert(0, item) loops.
/// </summary>
public class RangeObservableCollection<T> : ObservableCollection<T>
{
    /// <summary>
    /// Inserts all <paramref name="items"/> at index 0 and fires a single Add notification.
    /// Use instead of looping Insert(0, x) when prepending a batch.
    /// </summary>
    public void PrependRange(IList<T> items)
    {
        if (items.Count == 0) return;
        CheckReentrancy();
        for (int i = 0; i < items.Count; i++)
            Items.Insert(i, items[i]);
        OnPropertyChanged(new PropertyChangedEventArgs("Count"));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Add, (IList)items, 0));
    }

    /// <summary>
    /// Appends all <paramref name="items"/> and fires a single Add notification.
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        var list = items as IList<T> ?? items.ToList();
        if (list.Count == 0) return;
        CheckReentrancy();
        int startIndex = Items.Count;
        foreach (var item in list)
            Items.Add(item);
        OnPropertyChanged(new PropertyChangedEventArgs("Count"));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Add, (IList)list, startIndex));
    }
}
