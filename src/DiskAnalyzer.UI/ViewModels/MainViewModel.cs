using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
using DiskAnalyzer.UI.Localization;
using Microsoft.Win32;

namespace DiskAnalyzer.UI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly LocalizationService _localization;
    private readonly DiskScanEngine _engine = new();
    private CancellationTokenSource? _cts;
    private readonly Stopwatch _stopwatch = new();

    private ObservableCollection<DriveInfoModel> _drives = [];
    private DriveInfoModel? _selectedDrive;
    private string _customFolderPath = string.Empty;
    private bool _isScanning;
    private ScanProgress? _scanProgress;
    private string _statusText = string.Empty;
    private string _scanMetricsText = string.Empty;
    private string _statusMessageKey = "StatusReady";
    private object?[] _statusMessageArguments = [];
    private string? _scanMetricsKey;
    private object?[] _scanMetricsArguments = [];
    private FileSystemItem? _rootItem;
    private ObservableCollection<FileSystemItem> _rootItems = [];
    private FileSystemItem? _treemapRoot;
    private FileSystemItem? _selectedItem;
    private ObservableCollection<FileSystemItem> _topFiles = [];
    private ObservableCollection<FileSystemItem> _filteredFiles = [];
    private ObservableCollection<ExtensionSummary> _extensionBreakdown = [];
    private ExtensionSummary? _selectedExtension;
    private bool _selectedExtensionIsNoExtension;
    private string _searchQuery = string.Empty;
    private ObservableCollection<FileSystemItem> _breadcrumbPaths = [];
    private int _selectedTabIndex = 0;
    private List<FileSystemItem> _allFilesCache = [];
    private bool _showTreemap = true;

    public MainViewModel(LocalizationService? localization = null)
    {
        _localization = localization ?? LocalizationService.Instance;
        _localization.LanguageChanged += OnLanguageChanged;
        IsAdmin = PrivilegeManager.IsAdministrator;

        // Initialize Commands
        ScanCommand = new AsyncRelayCommand(ExecuteScanAsync, () => !IsScanning);
        CancelCommand = new RelayCommand(ExecuteCancel, () => IsScanning);
        BrowseFolderCommand = new RelayCommand(ExecuteBrowseFolder, () => !IsScanning);
        RelaunchAsAdminCommand = new RelayCommand(ExecuteRelaunchAsAdmin);
        ChangeLanguageCommand = new RelayCommand<string>(ExecuteChangeLanguage);

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

        SetStatus("StatusReady");
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

    public ReadOnlyObservableCollection<LanguageOption> AvailableLanguages => _localization.Languages;

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
                OnPropertyChanged(nameof(ActiveTreemapText));
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
                OnPropertyChanged(nameof(SelectedItemText));
                OnPropertyChanged(nameof(SelectedItemSizeText));
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
                _selectedExtensionIsNoExtension = value != null && string.Equals(
                    value.Extension,
                    _localization.Get("NoExtensionLabel"),
                    StringComparison.OrdinalIgnoreCase);
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

    public string TreemapToggleText => _localization.Get(
        ShowTreemap ? "HeatmapHideButton" : "HeatmapShowButton");

    public string ActiveTreemapText => TreemapRoot == null
        ? string.Empty
        : _localization.Format("ActiveFormat", TreemapRoot.SizeFormatted);

    public string SelectedItemText => SelectedItem == null
        ? string.Empty
        : _localization.Format("SelectedFormat", SelectedItem.Name);

    public string SelectedItemSizeText => SelectedItem == null
        ? string.Empty
        : _localization.Format("SelectedSizeFormat", SelectedItem.SizeFormatted);

    #endregion

    #region Commands

    public AsyncRelayCommand ScanCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand RelaunchAsAdminCommand { get; }
    public RelayCommand<string> ChangeLanguageCommand { get; }

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

    private void ExecuteChangeLanguage(string? cultureName)
    {
        if (!string.IsNullOrWhiteSpace(cultureName))
        {
            _localization.SetLanguage(cultureName);
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        string? previousExtension = _selectedExtension?.Extension;
        bool hadNoExtensionSelected = _selectedExtensionIsNoExtension;

        StatusText = _localization.Format(_statusMessageKey, _statusMessageArguments);
        ScanMetricsText = _scanMetricsKey == null
            ? string.Empty
            : _localization.Format(_scanMetricsKey, _scanMetricsArguments);

        OnPropertyChanged(nameof(TreemapToggleText));
        OnPropertyChanged(nameof(ActiveTreemapText));
        OnPropertyChanged(nameof(SelectedItemText));
        OnPropertyChanged(nameof(SelectedItemSizeText));
        OnPropertyChanged(nameof(Drives));

        if (RootItem != null)
        {
            UpdateExtensionBreakdown();

            ExtensionSummary? replacement = hadNoExtensionSelected
                ? ExtensionBreakdown.FirstOrDefault(extension => string.Equals(
                    extension.Extension,
                    _localization.Get("NoExtensionLabel"),
                    StringComparison.OrdinalIgnoreCase))
                : previousExtension == null
                    ? null
                    : ExtensionBreakdown.FirstOrDefault(extension => string.Equals(
                        extension.Extension,
                        previousExtension,
                        StringComparison.OrdinalIgnoreCase));

            if (!ReferenceEquals(_selectedExtension, replacement))
            {
                SelectedExtension = replacement;
            }
            ApplyFileFilter();
        }
    }

    private void SetStatus(string resourceKey, params object?[] arguments)
    {
        _statusMessageKey = resourceKey;
        _statusMessageArguments = arguments;
        StatusText = _localization.Format(resourceKey, arguments);
    }

    private void SetMetrics(string? resourceKey, params object?[] arguments)
    {
        _scanMetricsKey = resourceKey;
        _scanMetricsArguments = arguments;
        ScanMetricsText = resourceKey == null
            ? string.Empty
            : _localization.Format(resourceKey, arguments);
    }

    private void SetProgressStatus(ScanProgress progress)
    {
        switch (progress.Phase)
        {
            case ScanPhase.Initializing:
                SetStatus("ProgressInitializing");
                break;
            case ScanPhase.ReadingMft:
                SetStatus("ProgressReadingMft", progress.FilesScannedFormatted, progress.FoldersScannedFormatted);
                break;
            case ScanPhase.ReadingUsnJournal:
                SetStatus("ProgressReadingUsn", progress.FilesScannedFormatted);
                break;
            case ScanPhase.ScanningDirectories:
                SetStatus("ProgressScanningFolder", progress.CurrentFolder);
                break;
            case ScanPhase.BuildingTree:
                SetStatus("ProgressBuildingTree");
                break;
            case ScanPhase.CalculatingSizes:
                SetStatus("ProgressCalculatingSizes");
                break;
            case ScanPhase.Sorting:
                SetStatus("ProgressSorting");
                break;
            case ScanPhase.Complete:
                SetStatus(
                    "ProgressComplete",
                    progress.ElapsedTime.TotalSeconds.ToString("F2", _localization.CurrentCulture),
                    progress.FilesScannedFormatted,
                    progress.FoldersScannedFormatted);
                break;
            case ScanPhase.Cancelled:
                SetStatus("ProgressCancelled");
                break;
            case ScanPhase.Error:
                SetStatus("ProgressError");
                break;
            default:
                SetStatus("ProgressScanning");
                break;
        }
    }

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
            MessageBox.Show(
                _localization.Format("InvalidPathMessage", targetPath),
                _localization.Get("InvalidPathTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _cts = new CancellationTokenSource();
        IsScanning = true;
        _stopwatch.Restart();
        SetStatus("ScanningPathStatus", targetPath);
        SetMetrics("InitializingStatus");

        var progress = new Progress<ScanProgress>(p =>
        {
            ScanProgress = p;
            SetProgressStatus(p);
            SetMetrics(
                "ScanMetricsFormat",
                p.FilesScannedFormatted,
                p.FoldersScannedFormatted,
                p.TotalBytesFormatted,
                p.SpeedItemsFormatted);
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
            LocalizeNoExtensionLabels(extensions);
            ExtensionBreakdown = new ObservableCollection<ExtensionSummary>(extensions);

            // Flatten files for high-speed tabular search and flat file grid
            _allFilesCache = DiskScanEngine.FlattenFiles(root);
            _allFilesCache.Sort((a, b) => b.Size.CompareTo(a.Size));

            TopFiles = new ObservableCollection<FileSystemItem>(_allFilesCache.Take(5000));
            ApplyFileFilter();

            double elapsedSec = _stopwatch.Elapsed.TotalSeconds;
            double filesPerSec = elapsedSec > 0 ? root.FileCount / elapsedSec : 0;

            SetStatus(
                "ScanCompletedStatus",
                elapsedSec.ToString("F2", _localization.CurrentCulture),
                root.FileCount.ToString("N0", _localization.CurrentCulture),
                root.FolderCount.ToString("N0", _localization.CurrentCulture),
                root.SizeFormatted);
            SetMetrics(
                "ScanCompletedMetrics",
                filesPerSec.ToString("N0", _localization.CurrentCulture),
                root.SizeFormatted);

            // Refresh drive information to show updated usage gauge
            RefreshDrives();
        }
        catch (OperationCanceledException)
        {
            _stopwatch.Stop();
            SetStatus(
                "ScanCanceledStatus",
                _stopwatch.Elapsed.TotalSeconds.ToString("F2", _localization.CurrentCulture));
            SetMetrics("CanceledMetrics");
        }
        catch (Exception ex)
        {
            _stopwatch.Stop();
            SetStatus("ScanErrorStatus", ex.Message);
            SetMetrics("ErrorMetrics");
            MessageBox.Show(
                _localization.Format("ScanErrorMessage", targetPath, ex.Message),
                _localization.Get("ScanErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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
            SetStatus("CancelingStatus");
            _cts.Cancel();
        }
    }

    public void ExecuteBrowseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = _localization.Get("SelectFolderDialogTitle"),
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
            MessageBox.Show(
                _localization.Format("ElevationMessage", ex.Message),
                _localization.Get("ElevationTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
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
            Extension = SelectedExtension?.Extension == _localization.Get("NoExtensionLabel")
                ? string.Empty
                : SelectedExtension?.Extension
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
            Filter = _localization.Get("ExportCsvFilter"),
            DefaultExt = "csv",
            FileName = $"DiskAnalyzer_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            Title = _localization.Get("ExportDialogTitle")
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                SetStatus("ExportingStatus");
                await CsvExporter.ExportTreeToCsvAsync(RootItem, saveDialog.FileName);
                SetStatus("ExportedStatus", Path.GetFileName(saveDialog.FileName));
                MessageBox.Show(
                    _localization.Format("ExportCompleteMessage", saveDialog.FileName),
                    _localization.Get("ExportCompleteTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetStatus("ExportErrorStatus", ex.Message);
                MessageBox.Show(
                    _localization.Format("ExportFailedMessage", ex.Message),
                    _localization.Get("ExportFailedTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
            SetStatus(
                targets.Count == 1 ? "CopiedPathStatus" : "CopiedPathsStatus",
                targets.Count == 1 ? targets[0].GetFullPath() : targets.Count);
        }
        catch (Exception ex)
        {
            SetStatus("CopyFailedStatus", ex.Message);
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
                sb.AppendLine($"{_localization.Get("TreeNameHeader")}: {item.Name}");
                sb.AppendLine($"{_localization.Get("FullPathLabel")}: {item.GetFullPath()}");
                sb.AppendLine($"{_localization.Get("SizeHeader")}: {item.SizeFormatted} ({item.Size.ToString("N0", _localization.CurrentCulture)} {_localization.Get("BytesLabel")})");
                sb.AppendLine($"{_localization.Get("AllocatedHeader")}: {item.AllocatedSizeFormatted} ({item.AllocatedSize.ToString("N0", _localization.CurrentCulture)} {_localization.Get("BytesLabel")})");
                sb.AppendLine($"{_localization.Get("FilesHeader")}: {item.FileCount.ToString("N0", _localization.CurrentCulture)}");
                sb.AppendLine($"{_localization.Get("FoldersHeader")}: {item.FolderCount.ToString("N0", _localization.CurrentCulture)}");
                sb.AppendLine($"{_localization.Get("LastModifiedHeader")}: {item.LastModified:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"{_localization.Get("AttributesHeader")}: {item.Attributes}");
                sb.AppendLine(new string('-', 40));
            }
            Clipboard.SetText(sb.ToString().TrimEnd());
            SetStatus(
                targets.Count == 1 ? "CopiedDetailsStatus" : "CopiedDetailsManyStatus",
                targets.Count == 1 ? targets[0].Name : targets.Count);
        }
        catch (Exception ex)
        {
            SetStatus("CopyFailedStatus", ex.Message);
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
                MessageBox.Show(
                    _localization.Format("OpenFileFailedMessage", path),
                    _localization.Get("OpenFailedTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
                MessageBox.Show(
                    _localization.Format("OpenFolderFailedMessage", path),
                    _localization.Get("OpenFailedTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
            ? _localization.Format("ConfirmDeleteSingle", targets[0].GetFullPath())
            : _localization.Format("ConfirmDeleteMany", targets.Count);

        var confirm = MessageBox.Show(
            msg,
            _localization.Get("ConfirmDeleteTitle"),
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
                SetStatus(
                    deletedCount == 1 ? "MovedToRecycleBinStatus" : "MovedItemsToRecycleBinStatus",
                    deletedCount == 1 ? targets[0].GetFullPath() : deletedCount);
            }
            else
            {
                MessageBox.Show(
                    _localization.Get("DeleteFailedMessage"),
                    _localization.Get("DeleteFailedTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    public void ExecutePermanentDelete()
    {
        var targets = GetTargetItems();
        if (targets.Count == 0) return;

        string msg = targets.Count == 1
            ? _localization.Format("ConfirmPermanentDeleteSingle", targets[0].GetFullPath())
            : _localization.Format("ConfirmPermanentDeleteMany", targets.Count);

        var confirm = MessageBox.Show(
            msg,
            _localization.Get("ConfirmPermanentDeleteTitle"),
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
                SetStatus(
                    deletedCount == 1 ? "PermanentlyDeletedStatus" : "PermanentlyDeletedItemsStatus",
                    deletedCount == 1 ? targets[0].GetFullPath() : deletedCount);
            }
            else
            {
                MessageBox.Show(
                    _localization.Get("PermanentDeleteFailedMessage"),
                    _localization.Get("PermanentDeleteFailedTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
            string ext = string.IsNullOrEmpty(f.Extension)
                ? _localization.Get("NoExtensionLabel")
                : f.Extension.ToUpperInvariant();
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

    private void LocalizeNoExtensionLabels(IEnumerable<ExtensionSummary> extensions)
    {
        foreach (var extension in extensions)
        {
            if (string.Equals(extension.Extension, "[No Extension]", StringComparison.OrdinalIgnoreCase))
            {
                extension.Extension = _localization.Get("NoExtensionLabel");
            }
        }
    }

    #endregion
}
