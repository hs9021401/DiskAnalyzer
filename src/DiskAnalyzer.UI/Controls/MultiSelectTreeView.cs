using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace DiskAnalyzer.UI;

/// <summary>
/// Selection modifiers understood by <see cref="MultiSelectTreeView"/>.
/// </summary>
[Flags]
public enum MultiSelectModifiers
{
    None = 0,
    Control = 1,
    Shift = 2
}

/// <summary>
/// Selection state emitted by <see cref="MultiSelectTreeView"/>.
/// </summary>
public sealed class MultiSelectTreeViewSelectionChangedEventArgs : EventArgs
{
    public MultiSelectTreeViewSelectionChangedEventArgs(
        IReadOnlyList<object> selectedItems,
        object? currentItem)
    {
        SelectedItems = selectedItems;
        CurrentItem = currentItem;
    }

    public IReadOnlyList<object> SelectedItems { get; }

    public object? CurrentItem { get; }
}

/// <summary>
/// A hierarchical tree that keeps its own selection set instead of relying on
/// TreeView's built-in single-selection state.
/// </summary>
public class MultiSelectTreeView : TreeView
{
    public static readonly DependencyProperty IsMultiSelectedProperty =
        DependencyProperty.RegisterAttached(
            "IsMultiSelected",
            typeof(bool),
            typeof(MultiSelectTreeView),
            new FrameworkPropertyMetadata(false));

    private readonly List<object> _selectedItems = [];
    private readonly IReadOnlyList<object> _selectedItemsView;
    private object? _anchorItem;
    private object? _currentItem;
    private TreeViewItem? _nativeSelectedContainer;
    private bool _isUpdatingNativeSelection;

    public MultiSelectTreeView()
    {
        _selectedItemsView = _selectedItems.AsReadOnly();

        AddHandler(
            PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnPreviewMouseLeftButtonDown),
            true);
        AddHandler(
            PreviewMouseRightButtonDownEvent,
            new MouseButtonEventHandler(OnPreviewMouseRightButtonDown),
            true);
        AddHandler(
            PreviewKeyDownEvent,
            new KeyEventHandler(OnPreviewKeyDown),
            true);
        AddHandler(
            TreeViewItem.SelectedEvent,
            new RoutedEventHandler(OnNativeItemSelected),
            true);
        AddHandler(
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnTreeItemLoaded),
            true);
    }

    public IReadOnlyList<object> SelectedItems => _selectedItemsView;

    public object? CurrentItem => _currentItem;

    public event EventHandler<MultiSelectTreeViewSelectionChangedEventArgs>? SelectionChanged;

    public static bool GetIsMultiSelected(DependencyObject element) =>
        (bool)element.GetValue(IsMultiSelectedProperty);

    public static void SetIsMultiSelected(DependencyObject element, bool value) =>
        element.SetValue(IsMultiSelectedProperty, value);

    /// <summary>
    /// Applies normal, Ctrl, or Shift selection to an item.
    /// </summary>
    public void SelectItem(object item, MultiSelectModifiers modifiers = MultiSelectModifiers.None)
    {
        if (item == null)
            return;

        var visibleItems = GetVisibleItems().ToList();
        var selectedBefore = _selectedItems.ToHashSet();
        var selectedAfter = new List<object>();
        var currentContainer = FindContainer(item);

        if ((modifiers & MultiSelectModifiers.Shift) != 0)
        {
            var anchor = _anchorItem ?? _currentItem ?? item;
            int anchorIndex = visibleItems.FindIndex(value => ReferenceEquals(value, anchor));
            int itemIndex = visibleItems.FindIndex(value => ReferenceEquals(value, item));

            if (anchorIndex < 0 || itemIndex < 0)
            {
                selectedAfter.Add(item);
            }
            else
            {
                if ((modifiers & MultiSelectModifiers.Control) != 0)
                {
                    selectedAfter.AddRange(_selectedItems);
                }

                int start = Math.Min(anchorIndex, itemIndex);
                int end = Math.Max(anchorIndex, itemIndex);
                for (int index = start; index <= end; index++)
                {
                    AddIfMissing(selectedAfter, visibleItems[index]);
                }
            }
        }
        else if ((modifiers & MultiSelectModifiers.Control) != 0)
        {
            selectedAfter.AddRange(_selectedItems);
            if (Contains(selectedAfter, item))
            {
                RemoveItem(selectedAfter, item);
            }
            else
            {
                selectedAfter.Add(item);
            }
        }
        else
        {
            selectedAfter.Add(item);
        }

        _selectedItems.Clear();
        _selectedItems.AddRange(selectedAfter);
        _currentItem = Contains(_selectedItems, item)
            ? item
            : _selectedItems.LastOrDefault();

        if ((modifiers & MultiSelectModifiers.Shift) == 0 || _anchorItem == null)
        {
            _anchorItem = item;
        }

        SyncSelectionState(
            selectedBefore,
            Contains(_selectedItems, item) ? currentContainer : FindContainer(_currentItem));
        RaiseSelectionChanged();
    }

    public void ClearSelection()
    {
        if (_selectedItems.Count == 0 && _currentItem == null)
            return;

        var selectedBefore = _selectedItems.ToHashSet();
        _selectedItems.Clear();
        _anchorItem = null;
        _currentItem = null;
        SyncSelectionState(selectedBefore, null);
        RaiseSelectionChanged();
    }

    /// <summary>
    /// Removes deleted or otherwise detached data items from the selection.
    /// </summary>
    public void PruneSelection()
    {
        if (_selectedItems.Count == 0)
            return;

        var selectedBefore = _selectedItems.ToHashSet();
        var remaining = _selectedItems.Where(IsItemInTree).ToList();
        if (remaining.Count == _selectedItems.Count)
            return;

        _selectedItems.Clear();
        _selectedItems.AddRange(remaining);
        if (_currentItem == null || !Contains(_selectedItems, _currentItem))
        {
            _currentItem = _selectedItems.LastOrDefault();
        }

        if (_anchorItem != null && !Contains(_selectedItems, _anchorItem))
        {
            _anchorItem = _currentItem;
        }

        SyncSelectionState(selectedBefore, FindContainer(_currentItem));
        RaiseSelectionChanged();
    }

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        PruneSelection();
    }

    protected virtual bool FocusItem(TreeViewItem itemContainer) => itemContainer.Focus();

    private void FocusItemWithoutNativeSelection(TreeViewItem itemContainer)
    {
        _isUpdatingNativeSelection = true;
        try
        {
            FocusItem(itemContainer);
        }
        finally
        {
            _isUpdatingNativeSelection = false;
        }
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var itemContainer = FindTreeViewItem(e.OriginalSource as DependencyObject);
        if (itemContainer == null || IsInsideExpander(e.OriginalSource as DependencyObject, itemContainer))
            return;

        var item = GetDataItem(itemContainer);
        if (item == null)
            return;

        // Let the second click continue through WPF so MouseDoubleClick can
        // reach the window-level open handler.
        e.Handled = e.ClickCount < 2;
        var modifiers = GetModifiers();

        // TreeViewItem selects itself when it receives keyboard focus. Suppress
        // that native event while focusing so it cannot clear a Ctrl selection
        // before our multi-selection state is applied.
        FocusItemWithoutNativeSelection(itemContainer);

        SelectItem(item, modifiers, itemContainer);
    }

    private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var itemContainer = FindTreeViewItem(e.OriginalSource as DependencyObject);
        if (itemContainer == null || IsInsideExpander(e.OriginalSource as DependencyObject, itemContainer))
            return;

        var item = GetDataItem(itemContainer);
        if (item == null)
            return;

        e.Handled = true;
        FocusItemWithoutNativeSelection(itemContainer);

        // Keep a batch selection when the user opens its context menu. An
        // unselected right-click starts a new, single-item selection.
        if (!Contains(_selectedItems, item))
        {
            SelectItem(item);
        }
        else
        {
            SetCurrentItem(item, itemContainer);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ClearSelection();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SelectAllVisibleItems();
            e.Handled = true;
        }
    }

    private void OnNativeItemSelected(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingNativeSelection || e.OriginalSource is not TreeViewItem itemContainer)
            return;

        var item = GetDataItem(itemContainer);
        if (item != null)
        {
            SelectItem(item, MultiSelectModifiers.None, itemContainer);
        }
    }

    private void OnTreeItemLoaded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem itemContainer)
        {
            var item = GetDataItem(itemContainer);
            SetIsMultiSelected(itemContainer, item != null && Contains(_selectedItems, item));
        }
    }

    private void SelectItem(
        object item,
        MultiSelectModifiers modifiers,
        TreeViewItem? itemContainer)
    {
        if (itemContainer == null)
        {
            SelectItem(item, modifiers);
            return;
        }

        SelectItem(item, modifiers);
        SetNativeCurrentContainer(itemContainer);
    }

    private void SelectAllVisibleItems()
    {
        var selectedBefore = _selectedItems.ToHashSet();
        var visibleItems = GetVisibleItems().ToList();
        _selectedItems.Clear();
        _selectedItems.AddRange(visibleItems);
        _currentItem = visibleItems.LastOrDefault();
        _anchorItem = visibleItems.FirstOrDefault();
        SyncSelectionState(selectedBefore, FindContainer(_currentItem));
        RaiseSelectionChanged();
    }

    private void SetCurrentItem(object item, TreeViewItem itemContainer)
    {
        if (ReferenceEquals(_currentItem, item))
            return;

        _currentItem = item;
        _anchorItem = item;
        SetNativeCurrentContainer(itemContainer);
        RaiseSelectionChanged();
    }

    private void SyncSelectionState(
        HashSet<object> selectedBefore,
        TreeViewItem? currentContainer)
    {
        foreach (var item in selectedBefore)
        {
            if (!Contains(_selectedItems, item))
                SetModelSelection(item, false);
        }

        foreach (var item in _selectedItems)
        {
            SetModelSelection(item, true);
        }

        foreach (var container in EnumerateRealizedContainers(this))
        {
            var item = GetDataItem(container);
            SetIsMultiSelected(container, item != null && Contains(_selectedItems, item));
        }

        SetNativeCurrentContainer(currentContainer ?? FindContainer(_currentItem));
    }

    private void SetNativeCurrentContainer(TreeViewItem? container)
    {
        _isUpdatingNativeSelection = true;
        try
        {
            if (_nativeSelectedContainer != null && !ReferenceEquals(_nativeSelectedContainer, container))
            {
                _nativeSelectedContainer.IsSelected = false;
            }

            if (container != null && !container.IsSelected)
            {
                container.IsSelected = true;
            }

            _nativeSelectedContainer = container;
        }
        finally
        {
            _isUpdatingNativeSelection = false;
        }
    }

    private void RaiseSelectionChanged()
    {
        SelectionChanged?.Invoke(
            this,
            new MultiSelectTreeViewSelectionChangedEventArgs(_selectedItemsView, _currentItem));
    }

    protected virtual MultiSelectModifiers GetModifiers()
    {
        var modifiers = MultiSelectModifiers.None;
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            modifiers |= MultiSelectModifiers.Control;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            modifiers |= MultiSelectModifiers.Shift;
        return modifiers;
    }

    private IEnumerable<object> GetVisibleItems()
    {
        return EnumerateVisibleItems(this);
    }

    private static IEnumerable<object> EnumerateVisibleItems(ItemsControl parent)
    {
        for (int index = 0; index < parent.Items.Count; index++)
        {
            var item = parent.Items[index];
            yield return item;

            if (parent.ItemContainerGenerator.ContainerFromIndex(index) is TreeViewItem container &&
                container.IsExpanded)
            {
                foreach (var child in EnumerateVisibleItems(container))
                {
                    yield return child;
                }
            }
        }
    }

    private IEnumerable<TreeViewItem> EnumerateRealizedContainers(ItemsControl parent)
    {
        for (int index = 0; index < parent.Items.Count; index++)
        {
            if (parent.ItemContainerGenerator.ContainerFromIndex(index) is not TreeViewItem container)
                continue;

            yield return container;
            if (container.IsExpanded)
            {
                foreach (var child in EnumerateRealizedContainers(container))
                {
                    yield return child;
                }
            }
        }
    }

    private bool IsItemInTree(object item)
    {
        return EnumerateAllItems(this).Any(value => ReferenceEquals(value, item));
    }

    private static IEnumerable<object> EnumerateAllItems(ItemsControl parent)
    {
        for (int index = 0; index < parent.Items.Count; index++)
        {
            var item = parent.Items[index];
            yield return item;

            if (parent.ItemContainerGenerator.ContainerFromIndex(index) is ItemsControl container)
            {
                foreach (var child in EnumerateAllItems(container))
                {
                    yield return child;
                }
            }
        }
    }

    private TreeViewItem? FindContainer(object? item)
    {
        return item == null ? null : FindContainer(this, item);
    }

    private static TreeViewItem? FindContainer(ItemsControl parent, object item)
    {
        for (int index = 0; index < parent.Items.Count; index++)
        {
            var dataItem = parent.Items[index];
            var container = parent.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItem;
            if (ReferenceEquals(dataItem, item) || ReferenceEquals(container?.DataContext, item))
                return container;

            if (container != null)
            {
                var nested = FindContainer(container, item);
                if (nested != null)
                    return nested;
            }
        }

        return null;
    }

    private static object? GetDataItem(TreeViewItem item) => item.DataContext ?? item.Header;

    private static TreeViewItem? FindTreeViewItem(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is TreeViewItem item)
                return item;

            source = GetVisualOrLogicalParent(source);
        }

        return null;
    }

    private static bool IsInsideExpander(DependencyObject? source, TreeViewItem item)
    {
        while (source != null && !ReferenceEquals(source, item))
        {
            if (source is ToggleButton)
                return true;

            source = GetVisualOrLogicalParent(source);
        }

        return false;
    }

    private static DependencyObject? GetVisualOrLogicalParent(DependencyObject source)
    {
        if (source is Visual || source is Visual3D)
            return VisualTreeHelper.GetParent(source);

        return LogicalTreeHelper.GetParent(source);
    }

    private static bool Contains(IEnumerable<object> items, object item) =>
        items.Any(value => ReferenceEquals(value, item));

    private static void AddIfMissing(List<object> items, object item)
    {
        if (!Contains(items, item))
            items.Add(item);
    }

    private static void RemoveItem(List<object> items, object item)
    {
        int index = items.FindIndex(value => ReferenceEquals(value, item));
        if (index >= 0)
            items.RemoveAt(index);
    }

    private static void SetModelSelection(object item, bool isSelected)
    {
        if (item is global::DiskAnalyzer.Core.Models.FileSystemItem fileSystemItem)
        {
            fileSystemItem.IsSelected = isSelected;
        }
    }
}
