using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using DiskAnalyzer.Core.Export;
using DiskAnalyzer.Core.Models;
using DiskAnalyzer.Core.Native;
using DiskAnalyzer.Core.Scanning;
using DiskAnalyzer.Core.Search;
using DiskAnalyzer.UI.Helpers;
using Microsoft.Win32;

namespace DiskAnalyzer.UI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly DiskScanEngine _engine = new();
    private CancellationTokenSource? _cts;
    private readonly Stopwatch _stopwatch = new();

    private ObservableCollection<DriveInfoModel> _drives = [];
    private DriveInfoModel? _selectedDrive;
    private string _customFolderPath = string.Empty;
    private bool _isScanning;
    private ScanProgress? _scanProgress;
    private string _statusText = "Ready to scan. Select a drive or browse a folder.";
    private string _scanMetricsText = string.Empty;
    private FileSystemItem? _rootItem;
    private ObservableCollection<FileSystemItem> _rootItems = [];
    private FileSystemItem? _treemapRoot;
    private FileSystemItem? _selectedItem;
    private ObservableCollection<FileSystemItem> _topFiles = [];
    private ObservableCollection<FileSystemItem> _filteredFiles = [];
    private ObservableCollection<ExtensionSummary> _extensionBreakdown = [];
    private ExtensionSummary? _selectedExtension;
    private string _searchQuery = string.Empty;
    private ObservableCollection<FileSystemItem> _breadcrumbPaths = [];
    private int _selectedTabIndex = 0;
    private List<FileSystemItem> _allFilesCache = [];
    private bool _showTreemap = true;

    public MainViewModel()
    {
        IsAdmin = PrivilegeManager.IsAdministrator;

        // Initialize Commands
        ScanCommand = new AsyncRelayCommand(ExecuteScanAsync, () => !IsScanning);
        CancelCommand = new RelayCommand(ExecuteCancel, () => IsScanning);
        BrowseFolderCommand = new RelayCommand(ExecuteBrowseFolder, () => !IsScanning);
        RelaunchAsAdminCommand = new RelayCommand(ExecuteRelaunchAsAdmin);

        ToggleTreemapCommand = new RelayCommand(() => ShowTreemap = !ShowTreemap);
        ZoomTreemapCommand = new RelayCommand<FileSystemItem>(ExecuteZoomTreemap);
        ZoomOutTreemapCommand = new RelayCommand(ExecuteZoomOutTreemap, () => TreemapRoot?.Parent != null);
        ResetTreemapZoomCommand = new RelayCommand(ExecuteResetTreemapZoom, () => TreemapRoot != RootItem);

        ExportCsvCommand = new AsyncRelayCommand(ExecuteExportCsvAsync, () => RootItem != null && !IsScanning);
        CopyPathCommand = new RelayCommand(ExecuteCopyPath, () => SelectedItem != null);
        CopyDetailsCommand = new RelayCommand(ExecuteCopyDetails, () => SelectedItem != null);

        OpenFileCommand = new RelayCommand(ExecuteOpenFile, () => SelectedItem != null);
        OpenFolderCommand = new RelayCommand(ExecuteOpenFolder, () => SelectedItem != null);
        OpenTerminalCommand = new RelayCommand(ExecuteOpenTerminal, () => SelectedItem != null);
        OpenPowerShellCommand = new RelayCommand(ExecuteOpenPowerShell, () => SelectedItem != null);
        DeleteToRecycleBinCommand = new RelayCommand(ExecuteDeleteToRecycleBin, () => SelectedItem != null);
        PermanentDeleteCommand = new RelayCommand(ExecutePermanentDelete, () => SelectedItem != null);
        ShowPropertiesCommand = new RelayCommand(ExecuteShowProperties, () => SelectedItem != null);

        RefreshDrives();
    }

    #region Properties

    public bool IsAdmin { get; }

    public ObservableCollection<DriveInfoModel> Drives
    {
        get => _drives;
        set => SetProperty(ref _drives, value);
    }

    public DriveInfoModel? SelectedDrive
    {
        get => _selectedDrive;
        set
        {
            if (SetProperty(ref _selectedDrive, value))
            {
                if (value != null)
                {
                    CustomFolderPath = string.Empty;
                }
            }
        }
    }

    public string CustomFolderPath
    {
        get => _customFolderPath;
        set => SetProperty(ref _customFolderPath, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(CanScan));
                ScanCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
                BrowseFolderCommand.RaiseCanExecuteChanged();
                ExportCsvCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanScan => !IsScanning;

    public ScanProgress? ScanProgress
    {
        get => _scanProgress;
        set => SetProperty(ref _scanProgress, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string ScanMetricsText
    {
        get => _scanMetricsText;
        set => SetProperty(ref _scanMetricsText, value);
    }

    public FileSystemItem? RootItem
    {
        get => _rootItem;
        set
        {
            if (SetProperty(ref _rootItem, value))
            {
                RootItems.Clear();
                if (value != null)
                {
                    RootItems.Add(value);
                }
                TreemapRoot = value;
                ExportCsvCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ObservableCollection<FileSystemItem> RootItems
    {
        get => _rootItems;
        set => SetProperty(ref _rootItems, value);
    }

    public FileSystemItem? TreemapRoot
    {
        get => _treemapRoot;
        set
        {
            if (SetProperty(ref _treemapRoot, value))
            {
                UpdateBreadcrumbs();
                ZoomOutTreemapCommand.RaiseCanExecuteChanged();
                ResetTreemapZoomCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public FileSystemItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                CopyPathCommand.RaiseCanExecuteChanged();
                CopyDetailsCommand.RaiseCanExecuteChanged();
                OpenFileCommand.RaiseCanExecuteChanged();
                OpenFolderCommand.RaiseCanExecuteChanged();
                OpenTerminalCommand.RaiseCanExecuteChanged();
                OpenPowerShellCommand.RaiseCanExecuteChanged();
                DeleteToRecycleBinCommand.RaiseCanExecuteChanged();
                PermanentDeleteCommand.RaiseCanExecuteChanged();
                ShowPropertiesCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ObservableCollection<FileSystemItem> TopFiles
    {
        get => _topFiles;
        set => SetProperty(ref _topFiles, value);
    }

    public ObservableCollection<FileSystemItem> FilteredFiles
    {
        get => _filteredFiles;
        set => SetProperty(ref _filteredFiles, value);
    }

    public ObservableCollection<ExtensionSummary> ExtensionBreakdown
    {
        get => _extensionBreakdown;
        set => SetProperty(ref _extensionBreakdown, value);
    }

    public ExtensionSummary? SelectedExtension
    {
        get => _selectedExtension;
        set
        {
            if (SetProperty(ref _selectedExtension, value))
            {
                ApplyFileFilter();
            }
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyFileFilter();
            }
        }
    }

    public ObservableCollection<FileSystemItem> BreadcrumbPaths
    {
        get => _breadcrumbPaths;
        set => SetProperty(ref _breadcrumbPaths, value);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public bool ShowTreemap
    {
        get => _showTreemap;
        set
        {
            if (SetProperty(ref _showTreemap, value))
            {
                OnPropertyChanged(nameof(TreemapToggleText));
            }
        }
    }

    public string TreemapToggleText => ShowTreemap ? "🗺️ 隱藏熱力圖" : "🗺️ 顯示熱力圖";

    #endregion

    #region Commands

    public AsyncRelayCommand ScanCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand RelaunchAsAdminCommand { get; }

    public RelayCommand ToggleTreemapCommand { get; }
    public RelayCommand<FileSystemItem> ZoomTreemapCommand { get; }
    public RelayCommand ZoomOutTreemapCommand { get; }
    public RelayCommand ResetTreemapZoomCommand { get; }

    public AsyncRelayCommand ExportCsvCommand { get; }
    public RelayCommand CopyPathCommand { get; }
    public RelayCommand CopyDetailsCommand { get; }

    public RelayCommand OpenFileCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand OpenTerminalCommand { get; }
    public RelayCommand OpenPowerShellCommand { get; }
    public RelayCommand DeleteToRecycleBinCommand { get; }
    public RelayCommand PermanentDeleteCommand { get; }
    public RelayCommand ShowPropertiesCommand { get; }

    #endregion

    #region Command Execution Methods

    public void RefreshDrives()
    {
        Task.Run(() =>
        {
            var drives = DriveInfoModel.GetDrives();
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                Drives = new ObservableCollection<DriveInfoModel>(drives);

                if (SelectedDrive == null && Drives.Count > 0)
                {
                    // Default to C: or first fixed drive
                    var defaultDrive = Drives.FirstOrDefault(d => d.DriveLetter.StartsWith("C", StringComparison.OrdinalIgnoreCase))
                                       ?? Drives.FirstOrDefault(d => d.DriveType == DriveType.Fixed)
                                       ?? Drives.First();
                    SelectedDrive = defaultDrive;
                }
            });
        });
    }

    public async Task ExecuteScanAsync()
    {
        if (IsScanning) return;

        string targetPath = !string.IsNullOrWhiteSpace(CustomFolderPath)
            ? CustomFolderPath
            : SelectedDrive?.VolumePath ?? "C:\\";

        if (string.IsNullOrWhiteSpace(targetPath) || (!Directory.Exists(targetPath) && !File.Exists(targetPath)))
        {
            MessageBox.Show($"Path '{targetPath}' is not accessible or does not exist.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _cts = new CancellationTokenSource();
        IsScanning = true;
        _stopwatch.Restart();
        StatusText = $"Scanning '{targetPath}'...";
        ScanMetricsText = "Initializing...";

        var progress = new Progress<ScanProgress>(p =>
        {
            ScanProgress = p;
            StatusText = !string.IsNullOrEmpty(p.CurrentFolder) ? $"Scanning: {p.CurrentFolder}" : p.StatusMessage;
            ScanMetricsText = $"{p.FilesScannedFormatted} files, {p.FoldersScannedFormatted} folders ({p.TotalBytesFormatted}) - {p.SpeedItemsFormatted}";
        });

        try
        {
            var options = new ScanOptions
            {
                Path = targetPath,
                ScanMode = ScanMode.Auto,
                IncludeFreeSpaceItem = true
            };

            var root = await _engine.ScanAsync(options, progress, _cts.Token);
            _stopwatch.Stop();

            // Post-process tree
            root.SortChildrenBySizeDescending(true);
            root.CalculateChildPercentages(true);
            root.IsExpanded = true;

            RootItem = root;
            TreemapRoot = root;

            // Generate Extension Breakdown
            var extensions = DiskScanEngine.ComputeExtensionSummaries(root);
            ExtensionBreakdown = new ObservableCollection<ExtensionSummary>(extensions);

            // Flatten files for high-speed tabular search and flat file grid
            _allFilesCache = DiskScanEngine.FlattenFiles(root);
            _allFilesCache.Sort((a, b) => b.Size.CompareTo(a.Size));

            TopFiles = new ObservableCollection<FileSystemItem>(_allFilesCache.Take(5000));
            ApplyFileFilter();

            double elapsedSec = _stopwatch.Elapsed.TotalSeconds;
            double filesPerSec = elapsedSec > 0 ? root.FileCount / elapsedSec : 0;

            StatusText = $"Scan completed in {elapsedSec:F2}s ({root.FileCount:N0} files, {root.FolderCount:N0} folders, {root.SizeFormatted})";
            ScanMetricsText = $"{filesPerSec:N0} files/sec - {root.SizeFormatted} total";

            // Refresh drive information to show updated usage gauge
            RefreshDrives();
        }
        catch (OperationCanceledException)
        {
            _stopwatch.Stop();
            StatusText = $"Scan canceled after {_stopwatch.Elapsed.TotalSeconds:F2}s.";
            ScanMetricsText = "Canceled";
        }
        catch (Exception ex)
        {
            _stopwatch.Stop();
            StatusText = $"Scan error: {ex.Message}";
            ScanMetricsText = "Error";
            MessageBox.Show($"Failed to scan '{targetPath}':\n\n{ex.Message}", "Scan Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsScanning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public void ExecuteCancel()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            StatusText = "Canceling scan...";
            _cts.Cancel();
        }
    }

    public void ExecuteBrowseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Folder to Scan",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            CustomFolderPath = dialog.FolderName;
            SelectedDrive = null;
        }
    }

    public void ExecuteRelaunchAsAdmin()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "DiskAnalyzer.UI.exe",
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(psi);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not elevate application privileges:\n{ex.Message}", "Elevation Required", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void ExecuteZoomTreemap(FileSystemItem? item)
    {
        if (item != null && item.IsDirectory)
        {
            TreemapRoot = item;
        }
    }

    public void ExecuteZoomOutTreemap()
    {
        if (TreemapRoot?.Parent != null)
        {
            TreemapRoot = TreemapRoot.Parent;
        }
    }

    public void ExecuteResetTreemapZoom()
    {
        if (RootItem != null)
        {
            TreemapRoot = RootItem;
        }
    }

    private void UpdateBreadcrumbs()
    {
        var breadcrumbs = new List<FileSystemItem>();
        var curr = TreemapRoot;
        while (curr != null)
        {
            breadcrumbs.Insert(0, curr);
            curr = curr.Parent;
        }
        BreadcrumbPaths = new ObservableCollection<FileSystemItem>(breadcrumbs);
    }

    public void ApplyFileFilter()
    {
        if (_allFilesCache.Count == 0)
        {
            FilteredFiles = [];
            return;
        }

        var criteria = new SearchCriteria
        {
            Query = string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery.Trim(),
            Extension = SelectedExtension?.Extension == "[No Extension]" ? string.Empty : SelectedExtension?.Extension
        };

        var filtered = FileSearchEngine.Search(_allFilesCache, criteria);
        filtered.Sort((a, b) => b.Size.CompareTo(a.Size));

        // Limit UI view to top 5,000 matches for ultra-snappy responsiveness
        FilteredFiles = new ObservableCollection<FileSystemItem>(filtered.Take(5000));
    }

    public async Task ExecuteExportCsvAsync()
    {
        if (RootItem == null) return;

        var saveDialog = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            DefaultExt = "csv",
            FileName = $"DiskAnalyzer_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            Title = "Export Disk Analysis to CSV"
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                StatusText = "Exporting to CSV...";
                await CsvExporter.ExportTreeToCsvAsync(RootItem, saveDialog.FileName);
                StatusText = $"Exported successfully to {Path.GetFileName(saveDialog.FileName)}";
                MessageBox.Show($"Export successfully saved to:\n{saveDialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText = $"Export error: {ex.Message}";
                MessageBox.Show($"Failed to export CSV:\n{ex.Message}", "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public event Action? RequestViewRefresh;

    private readonly List<FileSystemItem> _selectedItems = new();
    public IReadOnlyList<FileSystemItem> SelectedItems => _selectedItems;

    public void UpdateSelectedItems(
        IEnumerable<FileSystemItem> items,
        FileSystemItem? primaryItem = null)
    {
        var selectedItems = items.Distinct().ToList();
        _selectedItems.Clear();
        _selectedItems.AddRange(selectedItems);
        SelectedItem = primaryItem != null && _selectedItems.Contains(primaryItem)
            ? primaryItem
            : _selectedItems.FirstOrDefault();
        CopyPathCommand.RaiseCanExecuteChanged();
        CopyDetailsCommand.RaiseCanExecuteChanged();
        OpenFileCommand.RaiseCanExecuteChanged();
        OpenFolderCommand.RaiseCanExecuteChanged();
        DeleteToRecycleBinCommand.RaiseCanExecuteChanged();
        PermanentDeleteCommand.RaiseCanExecuteChanged();
    }

    private List<FileSystemItem> GetTargetItems()
    {
        if (_selectedItems.Count > 0)
        {
            var selected = _selectedItems.ToHashSet();
            return _selectedItems
                .Where(item => !HasSelectedAncestor(item.Parent, selected))
                .ToList();
        }
        if (SelectedItem != null)
            return new List<FileSystemItem> { SelectedItem };
        return new List<FileSystemItem>();
    }

    private static bool HasSelectedAncestor(
        FileSystemItem? parent,
        HashSet<FileSystemItem> selectedItems)
    {
        while (parent != null)
        {
            if (selectedItems.Contains(parent))
                return true;

            parent = parent.Parent;
        }

        return false;
    }

    public void ExecuteCopyPath()
    {
        var targets = GetTargetItems();
        if (targets.Count == 0) return;
        try
        {
            string text = string.Join(Environment.NewLine, targets.Select(t => t.GetFullPath()));
            Clipboard.SetText(text);
            StatusText = targets.Count == 1
                ? $"Copied path to clipboard: {targets[0].GetFullPath()}"
                : $"Copied {targets.Count} paths to clipboard.";
        }
        catch (Exception ex)
        {
            StatusText = $"Copy failed: {ex.Message}";
        }
    }

    public void ExecuteCopyDetails()
    {
        var targets = GetTargetItems();
        if (targets.Count == 0) return;
        try
        {
            var sb = new StringBuilder();
            foreach (var item in targets)
            {
                sb.AppendLine($"Name: {item.Name}");
                sb.AppendLine($"Full Path: {item.GetFullPath()}");
                sb.AppendLine($"Size: {item.SizeFormatted} ({item.Size:N0} bytes)");
                sb.AppendLine($"Allocated: {item.AllocatedSizeFormatted} ({item.AllocatedSize:N0} bytes)");
                sb.AppendLine($"Files: {item.FileCount:N0}");
                sb.AppendLine($"Folders: {item.FolderCount:N0}");
                sb.AppendLine($"Modified: {item.LastModified:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Attributes: {item.Attributes}");
                sb.AppendLine(new string('-', 40));
            }
            Clipboard.SetText(sb.ToString().TrimEnd());
            StatusText = targets.Count == 1
                ? $"Copied details for {targets[0].Name} to clipboard."
                : $"Copied details for {targets.Count} items to clipboard.";
        }
        catch (Exception ex)
        {
            StatusText = $"Copy failed: {ex.Message}";
        }
    }

    public void ExecuteOpenFile()
    {
        var targets = GetTargetItems();
        if (targets.Count == 0) return;

        foreach (var item in targets)
        {
            string path = item.GetFullPath();
            if (!ShellOperations.Open(path))
            {
                MessageBox.Show($"Could not open '{path}'.", "Open Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    public void ExecuteOpenFolder()
    {
        var targets = GetTargetItems();
        if (targets.Count == 0) return;

        foreach (var item in targets)
        {
            string path = item.GetFullPath();
            if (!ShellOperations.SelectInExplorer(path))
            {
                MessageBox.Show($"Could not open '{path}' in File Explorer.", "Open Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    public void ExecuteOpenTerminal()
    {
        if (SelectedItem == null) return;
        string path = SelectedItem.GetFullPath();
        ShellOperations.OpenCommandPrompt(path);
    }

    public void ExecuteOpenPowerShell()
    {
        if (SelectedItem == null) return;
        string path = SelectedItem.GetFullPath();
        ShellOperations.OpenPowerShell(path);
    }

    public void ExecuteDeleteToRecycleBin()
    {
        var targets = GetTargetItems();
        if (targets.Count == 0) return;

        string msg = targets.Count == 1
            ? $"Are you sure you want to send '{targets[0].GetFullPath()}' to the Recycle Bin?"
            : $"Are you sure you want to send {targets.Count} selected items to the Recycle Bin?";

        var confirm = MessageBox.Show(
            msg,
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes)
        {
            int deletedCount = 0;
            foreach (var item in targets)
            {
                string path = item.GetFullPath();
                if (ShellOperations.MoveToRecycleBin(path, confirm: false))
                {
                    RemoveItemFromTree(item);
                    deletedCount++;
                }
            }

            if (deletedCount > 0)
            {
                RefreshViewsAfterModification();
                StatusText = deletedCount == 1
                    ? $"Moved '{targets[0].GetFullPath()}' to Recycle Bin."
                    : $"Moved {deletedCount} items to Recycle Bin.";
            }
            else
            {
                MessageBox.Show("Failed to send item(s) to the Recycle Bin.", "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public void ExecutePermanentDelete()
    {
        var targets = GetTargetItems();
        if (targets.Count == 0) return;

        string msg = targets.Count == 1
            ? $"WARNING: Are you sure you want to PERMANENTLY delete:\n'{targets[0].GetFullPath()}'?\n\nThis cannot be undone!"
            : $"WARNING: Are you sure you want to PERMANENTLY delete {targets.Count} selected items?\n\nThis cannot be undone!";

        var confirm = MessageBox.Show(
            msg,
            "Confirm Permanent Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            int deletedCount = 0;
            foreach (var item in targets)
            {
                string path = item.GetFullPath();
                if (ShellOperations.PermanentDelete(path, confirm: false))
                {
                    RemoveItemFromTree(item);
                    deletedCount++;
                }
            }

            if (deletedCount > 0)
            {
                RefreshViewsAfterModification();
                StatusText = deletedCount == 1
                    ? $"Permanently deleted '{targets[0].GetFullPath()}'."
                    : $"Permanently deleted {deletedCount} items.";
            }
            else
            {
                MessageBox.Show("Failed to permanently delete item(s).", "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public void ExecuteShowProperties()
    {
        if (SelectedItem == null) return;
        string path = SelectedItem.GetFullPath();
        ShellOperations.ShowProperties(path);
    }

    private void RemoveItemFromTree(FileSystemItem item)
    {
        // Propagate size subtractions up to root
        var currParent = item.Parent;
        while (currParent != null)
        {
            currParent.Size = Math.Max(0, currParent.Size - item.Size);
            currParent.AllocatedSize = Math.Max(0, currParent.AllocatedSize - item.AllocatedSize);
            if (item.IsDirectory)
            {
                currParent.FolderCount = Math.Max(0, currParent.FolderCount - (item.FolderCount + 1));
                currParent.FileCount = Math.Max(0, currParent.FileCount - item.FileCount);
            }
            else
            {
                currParent.FileCount = Math.Max(0, currParent.FileCount - 1);
            }
            currParent = currParent.Parent;
        }

        item.Parent?.Children.Remove(item);
        _allFilesCache.Remove(item);
        TopFiles.Remove(item);
        FilteredFiles.Remove(item);

        if (SelectedItem == item)
        {
            SelectedItem = null;
        }
        _selectedItems.Remove(item);
    }

    private void RefreshViewsAfterModification()
    {
        RootItem?.CalculateChildPercentages(true);

        // Re-evaluate TopFiles and FilteredFiles
        ApplyFileFilter();

        // Re-calculate Extension Breakdown
        UpdateExtensionBreakdown();

        // Refresh Treemap
        var currentTreemapRoot = TreemapRoot;
        TreemapRoot = null;
        TreemapRoot = currentTreemapRoot ?? RootItem;

        // Notify TreeView & DataGrid UI controls
        RequestViewRefresh?.Invoke();
    }

    private void UpdateExtensionBreakdown()
    {
        var dict = new Dictionary<string, (long Size, long Count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in _allFilesCache)
        {
            string ext = string.IsNullOrEmpty(f.Extension) ? "[No Extension]" : f.Extension.ToUpperInvariant();
            if (dict.TryGetValue(ext, out var val))
            {
                dict[ext] = (val.Size + f.Size, val.Count + 1);
            }
            else
            {
                dict[ext] = (f.Size, 1);
            }
        }

        long totalSize = RootItem?.Size > 0 ? RootItem.Size : 1;
        var list = new List<ExtensionSummary>();
        foreach (var kvp in dict)
        {
            list.Add(new ExtensionSummary
            {
                Extension = kvp.Key,
                TotalSize = kvp.Value.Size,
                FileCount = kvp.Value.Count,
                Percentage = ((double)kvp.Value.Size / totalSize) * 100.0,
                ColorHex = ExtensionSummary.GetColorForExtension(kvp.Key)
            });
        }
        list.Sort((a, b) => b.TotalSize.CompareTo(a.TotalSize));
        ExtensionBreakdown = new ObservableCollection<ExtensionSummary>(list);
    }

    #endregion
}
