using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DiskAnalyzer.Core.Models;
using DiskAnalyzer.UI;
using DiskAnalyzer.UI.ViewModels;
using Xunit;

namespace DiskAnalyzer.Tests;

public class MultiSelectTreeViewTests
{
    [Fact]
    public void TreeView_SupportsCtrlToggleAndShiftRangeSelection()
    {
        var result = RunOnSta(() =>
        {
            var first = new FileSystemItem { Name = "one.txt" };
            var second = new FileSystemItem { Name = "two.txt" };
            var third = new FileSystemItem { Name = "three.txt" };
            var tree = new MultiSelectTreeView
            {
                ItemsSource = new ObservableCollection<FileSystemItem>([first, second, third])
            };

            tree.SelectItem(first);
            tree.SelectItem(third, MultiSelectModifiers.Shift);
            var rangeSelection = tree.SelectedItems.Cast<FileSystemItem>().ToList();

            tree.SelectItem(second, MultiSelectModifiers.Control);
            var toggledSelection = tree.SelectedItems.Cast<FileSystemItem>().ToList();

            return (
                rangeSelection,
                toggledSelection,
                firstIsSelected: first.IsSelected,
                secondIsSelected: second.IsSelected,
                thirdIsSelected: third.IsSelected);
        });

        Assert.Equal(3, result.rangeSelection.Count);
        Assert.Equal(new[] { "one.txt", "two.txt", "three.txt" }, result.rangeSelection.Select(item => item.Name));
        Assert.Equal(new[] { "one.txt", "three.txt" }, result.toggledSelection.Select(item => item.Name));
        Assert.True(result.firstIsSelected);
        Assert.False(result.secondIsSelected);
        Assert.True(result.thirdIsSelected);
    }

    [Fact]
    public void MainViewModel_UpdateSelectedItems_UsesPrimaryAndClearsSelection()
    {
        var vm = new MainViewModel();
        var first = new FileSystemItem { Name = "one.txt" };
        var second = new FileSystemItem { Name = "two.txt" };

        vm.UpdateSelectedItems([first, second], second);

        Assert.Equal(second, vm.SelectedItem);
        Assert.Equal(new[] { first, second }, vm.SelectedItems);

        vm.UpdateSelectedItems([]);

        Assert.Null(vm.SelectedItem);
        Assert.Empty(vm.SelectedItems);
    }

    [Fact]
    public void CtrlMouseClick_PreservesExistingSelection()
    {
        var selectedItems = RunOnSta(() =>
        {
            var first = new FileSystemItem { Name = "one.txt" };
            var second = new FileSystemItem { Name = "two.txt" };
            var tree = new TestMultiSelectTreeView
            {
                ItemsSource = new ObservableCollection<FileSystemItem>([first, second])
            };
            var window = new Window
            {
                Content = tree,
                Width = 320,
                Height = 180,
                ShowInTaskbar = false
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                var firstContainer = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(0)!;
                var secondContainer = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(1)!;

                RaiseLeftButtonDown(tree, firstContainer);
                var afterFirst = tree.SelectedItems.Cast<FileSystemItem>().Select(item => item.Name).ToArray();

                tree.NextModifiers = MultiSelectModifiers.Control;
                RaiseLeftButtonDown(tree, secondContainer);

                return (
                    afterFirst,
                    afterSecond: tree.SelectedItems.Cast<FileSystemItem>().Select(item => item.Name).ToArray(),
                    firstIsSelected: first.IsSelected,
                    secondIsSelected: second.IsSelected,
                    firstNativeSelected: firstContainer.IsSelected,
                    secondNativeSelected: secondContainer.IsSelected);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(new[] { "one.txt" }, selectedItems.afterFirst);
        Assert.Equal(new[] { "one.txt", "two.txt" }, selectedItems.afterSecond);
    }

    [Fact]
    public void RightClickOnSelectedItem_PreservesBatchSelection()
    {
        var selectedItems = RunOnSta(() =>
        {
            var first = new FileSystemItem { Name = "one.txt" };
            var second = new FileSystemItem { Name = "two.txt" };
            var tree = new TestMultiSelectTreeView
            {
                ItemsSource = new ObservableCollection<FileSystemItem>([first, second])
            };
            var window = new Window
            {
                Content = tree,
                Width = 320,
                Height = 180,
                ShowInTaskbar = false
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                var firstContainer = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(0)!;
                var secondContainer = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(1)!;
                tree.SelectItem(first);
                tree.SelectItem(second, MultiSelectModifiers.Control);

                RaiseRightButtonDown(tree, firstContainer);

                return tree.SelectedItems.Cast<FileSystemItem>().Select(item => item.Name).ToArray();
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(new[] { "one.txt", "two.txt" }, selectedItems);
    }

    [Fact]
    public void DoubleClick_DoesNotGetConsumedBySelectionPreview()
    {
        var handled = RunOnSta(() =>
        {
            var item = new FileSystemItem { Name = "one.txt" };
            var tree = new TestMultiSelectTreeView
            {
                ItemsSource = new ObservableCollection<FileSystemItem>([item])
            };
            var window = new Window
            {
                Content = tree,
                Width = 320,
                Height = 180,
                ShowInTaskbar = false
            };

            try
            {
                window.Show();
                window.UpdateLayout();
                var container = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(0)!;
                return RaiseLeftButtonDown(tree, container, clickCount: 2).Handled;
            }
            finally
            {
                window.Close();
            }
        });

        Assert.False(handled);
    }

    private static T RunOnSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
            throw new Exception("STA test failed.", exception);

        return result!;
    }

    private static MouseButtonEventArgs RaiseLeftButtonDown(
        MultiSelectTreeView tree,
        TreeViewItem item,
        int clickCount = 1)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
            Source = item
        };
        SetClickCount(args, clickCount);

        tree.RaiseEvent(args);
        return args;
    }

    private static void RaiseRightButtonDown(MultiSelectTreeView tree, TreeViewItem item)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
        {
            RoutedEvent = UIElement.PreviewMouseRightButtonDownEvent,
            Source = item
        };

        tree.RaiseEvent(args);
    }

    private static void SetClickCount(MouseButtonEventArgs args, int clickCount)
    {
        typeof(MouseButtonEventArgs)
            .GetProperty(nameof(MouseButtonEventArgs.ClickCount), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(args, clickCount);
    }

    private sealed class TestMultiSelectTreeView : MultiSelectTreeView
    {
        public MultiSelectModifiers NextModifiers { get; set; }

        protected override MultiSelectModifiers GetModifiers() => NextModifiers;

        protected override bool FocusItem(TreeViewItem itemContainer)
        {
            itemContainer.IsSelected = true;
            return true;
        }
    }
}
