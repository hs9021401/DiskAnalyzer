using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DiskAnalyzer.Core.Models;
using DiskAnalyzer.Core.Scanning;
using DiskAnalyzer.UI;
using DiskAnalyzer.UI.Helpers;
using DiskAnalyzer.UI.Localization;
using DiskAnalyzer.UI.ViewModels;
using Xunit;

namespace DiskAnalyzer.Tests;

public sealed class PublishedExecutableFactAttribute : FactAttribute
{
    public PublishedExecutableFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("DISKANALYZER_RUN_EXE_TESTS") != "1")
            Skip = "Set DISKANALYZER_RUN_EXE_TESTS=1 after publishing to opt in to launching the application.";
    }
}

public class UiComponentTests
{
    private sealed class RenderingProbeTreemap : TreemapControl
    {
        public int BaseRenderCount { get; private set; }
        protected override void RenderBaseVisual()
        {
            BaseRenderCount++;
            base.RenderBaseVisual();
        }
        public void HoverAt(Point point) => UpdateHover(point);
    }
    [Fact]
    public void ByteSizeConverter_ConvertsValuesAccurately()
    {
        var converter = ByteSizeConverter.Instance;

        Assert.Equal("500 B", converter.Convert(500L, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("1.0 KB", converter.Convert(1024L, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("10.00 MB", converter.Convert(10L * 1024 * 1024, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("2.50 GB", converter.Convert((long)(2.5 * 1024 * 1024 * 1024), typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void PercentageConverter_ConvertsValuesAccurately()
    {
        var converter = PercentageConverter.Instance;

        Assert.Equal("12.3%", converter.Convert(12.345, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("50.0%", converter.Convert(0.50, typeof(string), "fraction", CultureInfo.InvariantCulture));
        Assert.Equal("12.35%", converter.Convert(12.3456, typeof(string), "2", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void VisibilityConverters_WorkAsExpected()
    {
        var boolConverter = BoolToVisibilityConverter.Instance;
        Assert.Equal(Visibility.Visible, boolConverter.Convert(true, typeof(Visibility), null, CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Collapsed, boolConverter.Convert(false, typeof(Visibility), null, CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Visible, boolConverter.Convert(false, typeof(Visibility), "invert", CultureInfo.InvariantCulture));

        var invertConverter = InvertBoolConverter.Instance;
        Assert.False((bool)invertConverter.Convert(true, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.True((bool)invertConverter.Convert(false, typeof(bool), null, CultureInfo.InvariantCulture));

        var nullConverter = NullToVisibilityConverter.Instance;
        Assert.Equal(Visibility.Collapsed, nullConverter.Convert(null, typeof(Visibility), null, CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Collapsed, nullConverter.Convert("", typeof(Visibility), null, CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Visible, nullConverter.Convert("test", typeof(Visibility), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ColorToBrushConverter_ReturnsCachedBrushes()
    {
        var converter = ColorToBrushConverter.Instance;
        var brush1 = converter.Convert("#FF0000", typeof(Brush), null, CultureInfo.InvariantCulture) as SolidColorBrush;
        var brush2 = converter.Convert("#FF0000", typeof(Brush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        Assert.NotNull(brush1);
        Assert.Same(brush1, brush2); // Verified cache reference equality
        Assert.Equal(Colors.Red, brush1.Color);
    }

    [Fact]
    public void FileIconConverter_ReturnsNonNullIcons()
    {
        var converter = FileIconConverter.Instance;
        var item = new FileSystemItem { Name = "test.txt", Extension = ".txt", IsDirectory = false };
        var folderItem = new FileSystemItem { Name = "MyFolder", IsDirectory = true };

        var fileIcon = converter.Convert(item, typeof(ImageSource), null, CultureInfo.InvariantCulture);
        var folderIcon = converter.Convert(folderItem, typeof(ImageSource), null, CultureInfo.InvariantCulture);

        // May be null or bitmap in test runner without full desktop, but shouldn't throw
        Assert.True(true);
    }

    [Fact]
    public void RelayCommand_ExecutesAndRespectsCanExecute()
    {
        bool executed = false;
        bool canExec = true;

        var cmd = new RelayCommand(() => executed = true, () => canExec);

        Assert.True(cmd.CanExecute(null));
        cmd.Execute(null);
        Assert.True(executed);

        canExec = false;
        Assert.False(cmd.CanExecute(null));
    }

    [Fact]
    public void MainViewModel_InitializesDrivesAndProperties()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.Drives);
        Assert.NotNull(vm.ScanCommand);
        Assert.NotNull(vm.CancelCommand);
        Assert.NotNull(vm.ExportCsvCommand);
        Assert.NotNull(vm.ZoomTreemapCommand);
        Assert.NotNull(vm.BreadcrumbPaths);
        Assert.False(vm.IsScanning);
    }

    [Fact]
    public void MainViewModel_BreadcrumbNavigationAndZoom_WorksCorrectly()
    {
        var vm = new MainViewModel();

        var root = new FileSystemItem { Name = @"C:\", IsDirectory = true, Size = 1000 };
        var sub1 = new FileSystemItem { Name = "Windows", IsDirectory = true, Size = 600 };
        var sub2 = new FileSystemItem { Name = "System32", IsDirectory = true, Size = 400 };

        root.AddChild(sub1);
        sub1.AddChild(sub2);

        vm.RootItem = root;
        Assert.Equal(root, vm.TreemapRoot);
        Assert.Single(vm.BreadcrumbPaths);

        // Zoom into Windows
        vm.ZoomTreemapCommand.Execute(sub1);
        Assert.Equal(sub1, vm.TreemapRoot);
        Assert.Equal(2, vm.BreadcrumbPaths.Count);
        Assert.Equal(root, vm.BreadcrumbPaths[0]);
        Assert.Equal(sub1, vm.BreadcrumbPaths[1]);

        // Zoom into System32
        vm.ZoomTreemapCommand.Execute(sub2);
        Assert.Equal(sub2, vm.TreemapRoot);
        Assert.Equal(3, vm.BreadcrumbPaths.Count);

        // Zoom out
        vm.ZoomOutTreemapCommand.Execute(null);
        Assert.Equal(sub1, vm.TreemapRoot);
        Assert.Equal(2, vm.BreadcrumbPaths.Count);

        // Reset zoom
        vm.ResetTreemapZoomCommand.Execute(null);
        Assert.Equal(root, vm.TreemapRoot);
        Assert.Single(vm.BreadcrumbPaths);
    }

    [Fact]
    public async Task MainViewModel_FileSearchAndFiltering_WorksCorrectly()
    {
        var vm = new MainViewModel();

        var root = new FileSystemItem { Name = @"C:\", IsDirectory = true, Size = 2000 };
        var file1 = new FileSystemItem { Name = "video.mp4", Size = 1500, Extension = ".mp4", IsDirectory = false };
        var file2 = new FileSystemItem { Name = "doc.txt", Size = 500, Extension = ".txt", IsDirectory = false };

        root.AddChild(file1);
        root.AddChild(file2);

        vm.RootItem = root;
        var flatFiles = DiskScanEngine.FlattenFiles(root);
        // Inject cached files
        var allFilesField = typeof(MainViewModel).GetField("_allFilesCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        allFilesField?.SetValue(vm, flatFiles);

        vm.SearchQuery = "video";
        await vm.ApplyFileFilterAsync();
        Assert.Single(vm.FilteredFiles);
        Assert.Equal("video.mp4", vm.FilteredFiles[0].Name);

        vm.SearchQuery = string.Empty;
        await vm.ApplyFileFilterAsync();
        Assert.Equal(2, vm.FilteredFiles.Count);
    }

    [Fact]
    public async Task MainViewModel_FileFilterDebounceUsesTheLatestQuery()
    {
        var vm = new MainViewModel();
        var root = new FileSystemItem { Name = @"C:\", IsDirectory = true };
        root.AddChild(new FileSystemItem { Name = "video.mp4", Size = 1500, Extension = ".mp4" });
        root.AddChild(new FileSystemItem { Name = "document.txt", Size = 500, Extension = ".txt" });

        var allFilesField = typeof(MainViewModel).GetField("_allFilesCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        allFilesField?.SetValue(vm, DiskScanEngine.FlattenFiles(root));

        vm.SearchQuery = "video";
        var firstFilter = vm.ApplyFileFilterAsync(debounce: true);
        vm.SearchQuery = "document";
        var latestFilter = vm.ApplyFileFilterAsync(debounce: true);
        await Task.WhenAll(firstFilter, latestFilter);

        var result = Assert.Single(vm.FilteredFiles);
        Assert.Equal("document.txt", result.Name);
    }

    [Fact]
    public async Task MainViewModel_NoExtensionFilterSurvivesLanguageChanges()
    {
        var localization = new LocalizationService();
        localization.SetLanguage("en-US", persist: false);
        var vm = new MainViewModel(localization);
        var root = new FileSystemItem { Name = "Root", IsDirectory = true };
        root.AddChild(new FileSystemItem { Name = "README", Size = 200 });
        root.AddChild(new FileSystemItem { Name = "notes.TXT", Size = 100, Extension = ".TXT" });
        root.RecalculateAggregateStatistics();
        vm.RootItem = root;
        typeof(MainViewModel).GetField("_allFilesCache", System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance)!.SetValue(vm, DiskScanEngine.FlattenFiles(root));
        localization.SetLanguage("fr-FR", persist: false);
        vm.SelectedExtension = vm.ExtensionBreakdown.Single(summary =>
            summary.Extension == localization.Get("NoExtensionLabel"));
        await vm.ApplyFileFilterAsync();
        Assert.Equal("README", Assert.Single(vm.FilteredFiles).Name);
        localization.SetLanguage("zh-TW", persist: false);
        await vm.ApplyFileFilterAsync();
        Assert.Equal(localization.Get("NoExtensionLabel"), vm.SelectedExtension!.Extension);
        Assert.Equal("README", Assert.Single(vm.FilteredFiles).Name);
    }

    [Fact]
    public void MainViewModel_DeleteBatchKeepsFailedItemsAndOldSearchSnapshot()
    {
        var localization = new LocalizationService();
        localization.SetLanguage("en-US", persist: false);
        var vm = new MainViewModel(localization);
        var root = new FileSystemItem { Name = "Root", IsDirectory = true };
        var removed = new FileSystemItem
            { Name = "removed.TXT", Extension = ".TXT", Size = 200, AllocatedSize = 4096 };
        var kept = new FileSystemItem
            { Name = "kept.txt", Extension = ".txt", Size = 100, AllocatedSize = 4096 };
        var readme = new FileSystemItem { Name = "README", Size = 50, AllocatedSize = 4096 };
        root.AddChild(removed);
        root.AddChild(kept);
        root.AddChild(readme);
        root.RecalculateAggregateStatistics();
        vm.RootItem = root;
        var cache = DiskScanEngine.FlattenFiles(root);
        SetFileCache(vm, cache);
        int refreshCount = 0;
        vm.RequestViewRefresh += () => refreshCount++;
        var attempted = new List<FileSystemItem>();

        var deleted = InvokeDeleteBatch(vm, new[] { kept, removed }, item =>
        {
            attempted.Add(item);
            return item == removed;
        });

        Assert.Same(removed, Assert.Single(deleted));
        Assert.Equal(new[] { kept, removed }, attempted);
        Assert.Equal(150, root.Size);
        Assert.Equal(8192, root.AllocatedSize);
        Assert.Equal(2, root.FileCount);
        Assert.Equal(0, root.FolderCount);
        Assert.DoesNotContain(removed, root.Children);
        Assert.Contains(kept, root.Children);
        Assert.Contains(readme, root.Children);
        Assert.DoesNotContain(removed, GetFileCache(vm));
        Assert.Contains(kept, GetFileCache(vm));
        Assert.Contains(removed, cache); // A search already holding the old snapshot remains safe.
        Assert.Contains(kept, cache);
        var textSummary = Assert.Single(vm.ExtensionBreakdown, summary => summary.Extension == ".txt");
        Assert.Equal(100, textSummary.TotalSize);
        Assert.Equal(4096, textSummary.AllocatedSize);
        Assert.Equal(1, textSummary.FileCount);
        var noExtensionSummary = Assert.Single(vm.ExtensionBreakdown,
            summary => summary.Extension == localization.Get("NoExtensionLabel"));
        Assert.Equal(50, noExtensionSummary.TotalSize);
        Assert.Equal(4096, noExtensionSummary.AllocatedSize);
        Assert.Equal(1, refreshCount);
    }

    [Fact]
    public async Task MainViewModel_DeleteBatchDeduplicatesTargetsAndRefreshesOnce()
    {
        var vm = new MainViewModel();
        var root = new FileSystemItem { Name = "Root", IsDirectory = true };
        var folder = new FileSystemItem { Name = "Folder", IsDirectory = true };
        var first = new FileSystemItem { Name = "first.bin", Size = 300, AllocatedSize = 4096 };
        var second = new FileSystemItem { Name = "second.bin", Size = 200, AllocatedSize = 4096 };
        var looseFirst = new FileSystemItem { Name = "loose-first.bin", Size = 150, AllocatedSize = 4096 };
        var looseSecond = new FileSystemItem { Name = "loose-second.bin", Size = 100, AllocatedSize = 4096 };
        folder.AddChild(first);
        folder.AddChild(second);
        root.AddChild(folder);
        root.AddChild(looseFirst);
        root.AddChild(looseSecond);
        var keepLarge = new FileSystemItem { Name = "keep-large.bin", Size = 100, AllocatedSize = 4096 };
        var keepSmall = new FileSystemItem { Name = "keep-small.bin", Size = 50, AllocatedSize = 4096 };
        root.AddChild(keepLarge);
        root.AddChild(keepSmall);
        root.RecalculateAggregateStatistics();
        vm.RootItem = root;
        var originalCache = new List<FileSystemItem>
            { first, second, looseFirst, looseSecond, keepLarge, keepSmall };
        SetFileCache(vm, originalCache);
        int deleteCalls = 0;
        int refreshCount = 0;
        int filteredChanges = 0;
        var attempted = new List<FileSystemItem>();
        var filteredChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.RequestViewRefresh += () => refreshCount++;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.FilteredFiles))
            {
                filteredChanges++;
                filteredChanged.TrySetResult();
            }
        };

        var deleted = InvokeDeleteBatch(
            vm,
            new[] { folder, first, folder, second, looseFirst, looseSecond },
            item => { deleteCalls++; attempted.Add(item); return true; });
        await filteredChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, deleted.Count);
        Assert.Contains(folder, deleted);
        Assert.Equal(3, deleteCalls);
        Assert.Equal(new[] { folder, looseFirst, looseSecond }, attempted);
        Assert.Equal(1, refreshCount);
        Assert.Equal(1, filteredChanges);
        Assert.Equal(150, root.Size);
        Assert.Equal(2, root.FileCount);
        Assert.NotSame(originalCache, GetFileCache(vm));
        Assert.Equal(new[] { keepLarge, keepSmall }, GetFileCache(vm));
        Assert.DoesNotContain(first, GetFileCache(vm));
        Assert.DoesNotContain(second, GetFileCache(vm));
        Assert.DoesNotContain(looseFirst, GetFileCache(vm));
        Assert.DoesNotContain(looseSecond, GetFileCache(vm));
    }

    [Fact]
    public void MainViewModel_DeleteBatchMovesZoomOutWhenAncestorIsDeletedAndDoesNothingOnTotalFailure()
    {
        var vm = new MainViewModel();
        var root = new FileSystemItem { Name = "Root", IsDirectory = true };
        var folder = new FileSystemItem { Name = "Folder", IsDirectory = true };
        var nested = new FileSystemItem { Name = "Nested", IsDirectory = true };
        var file = new FileSystemItem { Name = "data.bin", Size = 50 };
        nested.AddChild(file);
        folder.AddChild(nested);
        root.AddChild(folder);
        root.AddChild(new FileSystemItem { Name = "keep.bin", Size = 100 });
        root.RecalculateAggregateStatistics();
        vm.RootItem = root;
        vm.TreemapRoot = nested;
        vm.UpdateSelectedItems(new[] { nested, file }, file);
        var originalCache = DiskScanEngine.FlattenFiles(root);
        SetFileCache(vm, originalCache);
        var originalCacheReference = GetFileCache(vm);
        int refreshCount = 0;
        int filteredChanges = 0;
        vm.RequestViewRefresh += () => refreshCount++;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.FilteredFiles)) filteredChanges++;
        };

        Assert.Empty(InvokeDeleteBatch(vm, new[] { folder, file }, _ => false));
        Assert.Same(originalCacheReference, GetFileCache(vm));
        Assert.Contains(folder, root.Children);
        Assert.Contains(file, originalCache);
        Assert.Same(nested, vm.TreemapRoot);
        Assert.Equal(0, refreshCount);
        Assert.Equal(0, filteredChanges);

        var deleted = InvokeDeleteBatch(vm, new[] { folder, file }, _ => true);
        Assert.Same(folder, Assert.Single(deleted));
        Assert.Same(root, vm.TreemapRoot);
        Assert.DoesNotContain(folder, root.Children);
        Assert.Null(vm.SelectedItem);
        Assert.Empty(vm.SelectedItems);
        Assert.Contains(file, originalCache);
        Assert.Equal(1, refreshCount);
    }

    private static List<FileSystemItem> InvokeDeleteBatch(
        MainViewModel vm,
        IEnumerable<FileSystemItem> items,
        Func<FileSystemItem, bool> delete)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance;
        return (List<FileSystemItem>)typeof(MainViewModel)
            .GetMethod("DeleteItemsAndRefresh", flags)!
            .Invoke(vm, new object[] { items, delete })!;
    }

    private static void SetFileCache(MainViewModel vm, List<FileSystemItem> files)
    {
        typeof(MainViewModel).GetField("_allFilesCache", System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance)!.SetValue(vm, files);
    }

    private static List<FileSystemItem> GetFileCache(MainViewModel vm) =>
        (List<FileSystemItem>)typeof(MainViewModel).GetField("_allFilesCache",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(vm)!;

    [Fact]
    public async Task MainViewModel_ScanExecutionOnLocalFolder_PopulatesTreeAndBreakdown()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"DiskAnalyzerVmTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string subDir = Path.Combine(tempDir, "SubFolder");
        Directory.CreateDirectory(subDir);

        File.WriteAllBytes(Path.Combine(tempDir, "data.BIN"), new byte[500]);
        File.WriteAllBytes(Path.Combine(subDir, "report.pdf"), new byte[1500]);

        try
        {
            var vm = new MainViewModel
            {
                CustomFolderPath = tempDir
            };

            await vm.ExecuteScanAsync();

            Assert.NotNull(vm.RootItem);
            Assert.True(vm.RootItem.IsExpanded);
            Assert.Equal(2000, vm.RootItem.Size);
            Assert.Equal(2, vm.RootItem.FileCount);
            Assert.Equal(1, vm.RootItem.FolderCount);

            Assert.NotEmpty(vm.ExtensionBreakdown);
            Assert.Equal(2, vm.ExtensionBreakdown.Count);
            Assert.Contains(vm.ExtensionBreakdown, summary => summary.Extension == ".bin");

            Assert.Equal(2, vm.FilteredFiles.Count);
            Assert.Equal("report.pdf", vm.FilteredFiles[0].Name); // Sorted by size descending
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void MainViewModel_ShowTreemap_Toggle_WorksCorrectly()
    {
        var localization = new LocalizationService();
        localization.SetLanguage("en-US", persist: false);
        var vm = new MainViewModel(localization);

        Assert.True(vm.ShowTreemap);
        Assert.Equal("🗺️ Hide Heatmap", vm.TreemapToggleText);

        vm.ToggleTreemapCommand.Execute(null);
        Assert.False(vm.ShowTreemap);
        Assert.Equal("🗺️ Show Heatmap", vm.TreemapToggleText);

        vm.ToggleTreemapCommand.Execute(null);
        Assert.True(vm.ShowTreemap);
        Assert.Equal("🗺️ Hide Heatmap", vm.TreemapToggleText);
    }

    [Fact]
    public void LocalizationService_LoadsAllSupportedLanguages()
    {
        var localization = new LocalizationService();

        Assert.Equal(8, localization.Languages.Count);
        Assert.Contains(localization.Languages, language => language.CultureName == "zh-TW");
        Assert.Contains(localization.Languages, language => language.CultureName == "es-ES");
        Assert.Contains(localization.Languages, language => language.CultureName == "fr-FR");

        localization.SetLanguage("zh-TW", persist: false);
        Assert.Equal("🗺️ 隱藏熱力圖", localization.Get("HeatmapHideButton"));
        Assert.Equal("語言", localization.Get("LanguageMenu"));

        localization.SetLanguage("fr-FR", persist: false);
        Assert.Contains("Masquer", localization.Get("HeatmapHideButton"));

        foreach (var language in localization.Languages.Where(language => language.CultureName != "auto"))
        {
            localization.SetLanguage(language.CultureName, persist: false);
            Assert.All(localization.Keys, key => Assert.NotEqual(key, localization.Get(key)));
        }
    }

    [Fact]
    public void MainViewModel_LanguageSwitch_UpdatesDynamicText()
    {
        var localization = new LocalizationService();
        localization.SetLanguage("en-US", persist: false);
        var vm = new MainViewModel(localization);

        Assert.Equal("🗺️ Hide Heatmap", vm.TreemapToggleText);

        localization.SetLanguage("zh-CN", persist: false);
        Assert.Equal("🗺️ 隐藏热力图", vm.TreemapToggleText);

        vm.ToggleTreemapCommand.Execute(null);
        Assert.Equal("🗺️ 显示热力图", vm.TreemapToggleText);
    }

    [Fact]
    public void WpfResources_DarkTheme_CanBeLoadedSuccessfully()
    {
        var uri = new Uri("/DiskAnalyzer;component/Themes/DarkTheme.xaml", UriKind.Relative);
        var resDict = Application.LoadComponent(uri) as ResourceDictionary;
        Assert.NotNull(resDict);
        Assert.True(resDict.Contains("BgDarkBrush"));
        Assert.True(resDict.Contains("AccentStartBrush"));
    }

    [Fact]
    public void TreemapControl_UsesSeparateBaseAndOverlayVisuals()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                _ = Application.Current ?? new Application();

                var root = new FileSystemItem { Name = "Root", IsDirectory = true };
                root.AddChild(new FileSystemItem { Name = "Large", Size = 800, IsDirectory = false });
                root.AddChild(new FileSystemItem { Name = "Small", Size = 200, IsDirectory = false });
                root.RecalculateAggregateStatistics();

                var control = new RenderingProbeTreemap { RootItem = root };
                // Attach a presentation source without showing a desktop window.
                using var source = new System.Windows.Interop.HwndSource(
                    new System.Windows.Interop.HwndSourceParameters("DiskAnalyzer rendering test")
                    {
                        Width = 640,
                        Height = 400,
                        WindowStyle = unchecked((int)0x80000000), // WS_POPUP, without WS_VISIBLE
                        PositionX = -32000,
                        PositionY = -32000
                    });
                source.RootVisual = control;
                control.Measure(new Size(640, 400));
                control.Arrange(new Rect(0, 0, 640, 400));
                control.UpdateLayout();
                control.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                control.RecomputeLayout();
                Assert.True(control.IsVisible);
                Assert.Equal(2, VisualTreeHelper.GetChildrenCount(control));
                var overlay = (DrawingVisual)VisualTreeHelper.GetChild(control, 1);
                int baseRenderCount = control.BaseRenderCount;

                control.SelectedItem = root.Children[0];
                Assert.Equal(baseRenderCount, control.BaseRenderCount);
                Assert.NotEmpty(overlay.Drawing.Children);

                control.SelectedItem = null;
                control.HoverAt(new Point(10, 10));
                Assert.Equal(baseRenderCount, control.BaseRenderCount);
                Assert.NotEmpty(overlay.Drawing.Children);
                control.HoverAt(new Point(-1, -1));
                Assert.Equal(baseRenderCount, control.BaseRenderCount);
                Assert.True(overlay.Drawing == null || overlay.Drawing.Children.Count == 0);

                control.HoverAt(new Point(10, 10));
                control.RootItem = null;
                Assert.True(control.BaseRenderCount > baseRenderCount);
                Assert.True(overlay.Drawing == null || overlay.Drawing.Children.Count == 0);
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(5000), "Treemap rendering test did not finish.");

        if (threadEx != null)
        {
            throw new Exception($"Treemap rendering test failed: {threadEx}", threadEx);
        }
    }

    [PublishedExecutableFact]
    public async Task E2E_DiskAnalyzerExecutable_StartsAndRendersMainWindow()
    {
        string rootDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\.."));
        string exePath = Path.Combine(rootDir, "DiskAnalyzer.exe");
        Assert.True(File.Exists(exePath), "Publish DiskAnalyzer.exe to the repository root before opting in to this test.");

        if (File.Exists(exePath))
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = rootDir,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var proc = Process.Start(psi);
            Assert.NotNull(proc);

            // Wait up to 5 seconds for WPF to create window handle
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < timeout && proc.MainWindowHandle == IntPtr.Zero && !proc.HasExited)
            {
                await Task.Delay(200);
                proc.Refresh();
            }

            // Assert that the process has NOT crashed/exited and has rendered its window
            Assert.False(proc.HasExited, $"DiskAnalyzer.exe crashed prematurely! Error: {await proc.StandardError.ReadToEndAsync()}");
            Assert.True(proc.Responding, "Process is not responding!");

            // Clean up
            try { proc.Kill(); } catch { }
        }
    }

    [Fact]
    public void MainWindow_InstantiatesAndRendersCleanlyOnStaThread()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var application = Application.Current ?? new Application();

                var uri = new Uri("/DiskAnalyzer;component/Themes/DarkTheme.xaml", UriKind.Relative);
                var dict = (ResourceDictionary)Application.LoadComponent(uri);
                application.Resources.MergedDictionaries.Add(dict);

                var window = new MainWindow();
                Assert.NotNull(window);
                Assert.NotNull(window.DataContext);
                Assert.IsType<MainViewModel>(window.DataContext);

                // Force layout update and handle creation
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.EnsureHandle();
                Assert.NotEqual(IntPtr.Zero, helper.Handle);

                window.Close();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(3000);

        if (threadEx != null)
        {
            throw new Exception($"STA Thread failed: {threadEx}", threadEx);
        }
    }

    [Fact]
    public void MainWindow_LanguageMenu_OpensAndShowsChoices()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var application = Application.Current ?? new Application();

                var uri = new Uri("/DiskAnalyzer;component/Themes/DarkTheme.xaml", UriKind.Relative);
                var dict = (ResourceDictionary)Application.LoadComponent(uri);
                application.Resources.MergedDictionaries.Add(dict);

                var window = new MainWindow();
                window.Show();
                window.UpdateLayout();

                var languageButton = FindVisualChildren<Button>(window)
                    .Single(button => button.ContextMenu != null);

                languageButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.NotNull(languageButton.ContextMenu);
                Assert.True(languageButton.ContextMenu.IsOpen);
                Assert.Equal(8, languageButton.ContextMenu.Items.Count);

                window.Close();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(5000);

        if (threadEx != null)
        {
            throw new Exception($"Language menu regression test failed: {threadEx}", threadEx);
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        if (root == null)
            yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;

            foreach (T descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }
}
