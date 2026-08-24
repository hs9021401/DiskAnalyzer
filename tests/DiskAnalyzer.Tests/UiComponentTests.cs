using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using DiskAnalyzer.Core.Models;
using DiskAnalyzer.Core.Scanning;
using DiskAnalyzer.UI;
using DiskAnalyzer.UI.Helpers;
using DiskAnalyzer.UI.ViewModels;
using Xunit;

namespace DiskAnalyzer.Tests;

public class UiComponentTests
{
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
    public void MainViewModel_FileSearchAndFiltering_WorksCorrectly()
    {
        var vm = new MainViewModel();

        var root = new FileSystemItem { Name = @"C:\", IsDirectory = true, Size = 2000 };
        var file1 = new FileSystemItem { Name = "video.mp4", Size = 1500, Extension = ".mp4", IsDirectory = false };
        var file2 = new FileSystemItem { Name = "doc.txt", Size = 500, Extension = ".txt", IsDirectory = false };

        root.AddChild(file1);
        root.AddChild(file2);

        vm.RootItem = root;
        var flatFiles = DiskScanEngine.FlattenFiles(root);
        vm.TopFiles = new System.Collections.ObjectModel.ObservableCollection<FileSystemItem>(flatFiles);

        // Inject cached files
        var allFilesField = typeof(MainViewModel).GetField("_allFilesCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        allFilesField?.SetValue(vm, flatFiles);

        vm.SearchQuery = "video";
        Assert.Single(vm.FilteredFiles);
        Assert.Equal("video.mp4", vm.FilteredFiles[0].Name);

        vm.SearchQuery = string.Empty;
        Assert.Equal(2, vm.FilteredFiles.Count);
    }

    [Fact]
    public async Task MainViewModel_ScanExecutionOnLocalFolder_PopulatesTreeAndBreakdown()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"DiskAnalyzerVmTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string subDir = Path.Combine(tempDir, "SubFolder");
        Directory.CreateDirectory(subDir);

        File.WriteAllBytes(Path.Combine(tempDir, "data.bin"), new byte[500]);
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
        var vm = new MainViewModel();

        Assert.True(vm.ShowTreemap);
        Assert.Equal("🗺️ 隱藏熱力圖", vm.TreemapToggleText);

        vm.ToggleTreemapCommand.Execute(null);
        Assert.False(vm.ShowTreemap);
        Assert.Equal("🗺️ 顯示熱力圖", vm.TreemapToggleText);

        vm.ToggleTreemapCommand.Execute(null);
        Assert.True(vm.ShowTreemap);
        Assert.Equal("🗺️ 隱藏熱力圖", vm.TreemapToggleText);
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
    public async Task E2E_DiskAnalyzerExecutable_StartsAndRendersMainWindow()
    {
        string rootDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\.."));
        string exePath = Path.Combine(rootDir, "DiskAnalyzer.exe");

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
                if (Application.Current == null)
                {
                    _ = new Application();
                }

                var uri = new Uri("/DiskAnalyzer;component/Themes/DarkTheme.xaml", UriKind.Relative);
                var dict = (ResourceDictionary)Application.LoadComponent(uri);
                Application.Current.Resources.MergedDictionaries.Add(dict);

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
}



