using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DiskAnalyzer.Core.Models;
using DiskAnalyzer.UI.ViewModels;

namespace DiskAnalyzer.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        DataContext = vm;

        vm.RequestViewRefresh += () =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                FileDataGrid.Items.Refresh();
                MainTreeView.PruneSelection();
            });
        };
    }

    private void OnClearSearchClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.SearchQuery = string.Empty;
        }
    }

    private void OnTreeViewSelectionChanged(
        object sender,
        MultiSelectTreeViewSelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var items = e.SelectedItems.OfType<FileSystemItem>();
            vm.UpdateSelectedItems(items, e.CurrentItem as FileSystemItem);
        }
    }

    private void OnTreeViewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.SelectedItem != null)
        {
            if (!vm.SelectedItem.IsDirectory)
            {
                vm.ExecuteOpenFile();
                e.Handled = true;
            }
        }
    }

    private void OnFileDataGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is DataGrid dg)
        {
            var list = new List<FileSystemItem>();
            foreach (var item in dg.SelectedItems)
            {
                if (item is FileSystemItem fsi)
                {
                    list.Add(fsi);
                }
            }
            vm.UpdateSelectedItems(list, dg.SelectedItem as FileSystemItem);
        }
    }

    private void OnFileDataGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.SelectedItem != null)
        {
            if (vm.SelectedItem.IsDirectory)
            {
                vm.ZoomTreemapCommand.Execute(vm.SelectedItem);
            }
            else
            {
                vm.ExecuteOpenFile();
            }
            e.Handled = true;
        }
    }

    private void OnFolderPathBlockLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock tb && tb.DataContext is FileSystemItem item)
        {
            tb.Text = item.Parent != null
                ? item.Parent.GetFullPath()
                : (Path.GetDirectoryName(item.GetFullPath()) ?? string.Empty);
        }
    }
}
