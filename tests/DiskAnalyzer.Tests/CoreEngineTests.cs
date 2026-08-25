using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DiskAnalyzer.Core.Export;
using DiskAnalyzer.Core.Mft;
using DiskAnalyzer.Core.Models;
using DiskAnalyzer.Core.Native;
using DiskAnalyzer.Core.Scanning;
using DiskAnalyzer.Core.Search;
using DiskAnalyzer.Core.Treemap;
using Xunit;

namespace DiskAnalyzer.Tests;

public class CoreEngineTests
{
    [Fact]
    public void FileSystemItem_Formatting_WorksCorrectly()
    {
        Assert.Equal("500 B", FileSystemItem.FormatBytes(500));
        Assert.Equal("1.0 KB", FileSystemItem.FormatBytes(1024));
        Assert.Equal("1.50 MB", FileSystemItem.FormatBytes((long)(1.5 * 1024 * 1024)));
        Assert.Equal("2.50 GB", FileSystemItem.FormatBytes((long)(2.5 * 1024 * 1024 * 1024)));
        Assert.Equal("1.00 TB", FileSystemItem.FormatBytes(1024L * 1024 * 1024 * 1024));
    }

    [Fact]
    public void FileSystemItem_FullPathResolution_WorksCorrectly()
    {
        var root = new FileSystemItem { Name = @"C:\", IsDirectory = true };
        var users = new FileSystemItem { Name = "Users", IsDirectory = true };
        var alex = new FileSystemItem { Name = "Alex", IsDirectory = true };
        var file = new FileSystemItem { Name = "test.txt", IsDirectory = false, Size = 100 };

        root.AddChild(users);
        users.AddChild(alex);
        alex.AddChild(file);

        Assert.Equal(@"C:\", root.GetFullPath());
        Assert.Equal(@"C:\Users", users.GetFullPath());
        Assert.Equal(@"C:\Users\Alex", alex.GetFullPath());
        Assert.Equal(@"C:\Users\Alex\test.txt", file.GetFullPath());
    }

    [Fact]
    public void FileSystemItem_PercentageAndSorting_WorksCorrectly()
    {
        var root = new FileSystemItem { Name = "Root", IsDirectory = true, Size = 1000 };
        var child1 = new FileSystemItem { Name = "Small", Size = 200 };
        var child2 = new FileSystemItem { Name = "Large", Size = 800 };

        root.AddChild(child1);
        root.AddChild(child2);

        root.CalculateChildPercentages(false);
        Assert.Equal(20.0, child1.Percentage);
        Assert.Equal(80.0, child2.Percentage);

        root.SortChildrenBySizeDescending(false);
        Assert.Equal("Large", root.Children[0].Name);
        Assert.Equal("Small", root.Children[1].Name);
    }

    [Fact]
    public void ExtensionSummary_Colors_AreDeterministicAndCurated()
    {
        string mp4Color = ExtensionSummary.GetColorForExtension(".mp4");
        string exeColor = ExtensionSummary.GetColorForExtension("exe");
        string zipColor = ExtensionSummary.GetColorForExtension(".zip");

        Assert.StartsWith("#", mp4Color);
        Assert.StartsWith("#", exeColor);
        Assert.StartsWith("#", zipColor);
        Assert.NotEqual(mp4Color, exeColor);
    }

    [Fact]
    public void SquarifiedTreemap_GeneratesValidBounds()
    {
        var root = new FileSystemItem { Name = "Root", Size = 1000, IsDirectory = true };
        root.AddChild(new FileSystemItem { Name = "Item1", Size = 600 });
        root.AddChild(new FileSystemItem { Name = "Item2", Size = 300 });
        root.AddChild(new FileSystemItem { Name = "Item3", Size = 100 });

        var bounds = new RectD(0, 0, 800, 600);
        var nodes = SquarifiedTreemap.ComputeLayout(root, bounds);

        Assert.NotEmpty(nodes);
        foreach (var node in nodes)
        {
            Assert.True(node.Bounds.Width > 0);
            Assert.True(node.Bounds.Height > 0);
            Assert.True(node.Bounds.X >= 0 && node.Bounds.Right <= 800.01);
            Assert.True(node.Bounds.Y >= 0 && node.Bounds.Bottom <= 600.01);
        }

        // Hit test
        var hit = SquarifiedTreemap.HitTest(nodes, 50, 50);
        Assert.NotNull(hit);
    }

    [Fact]
    public void SearchEngine_WildcardAndFilters_WorkCorrectly()
    {
        var items = new List<FileSystemItem>
        {
            new() { Name = "document.pdf", Size = 500 * 1024, Extension = ".pdf" },
            new() { Name = "movie.mp4", Size = 1500L * 1024 * 1024, Extension = ".mp4" },
            new() { Name = "archive.zip", Size = 50 * 1024 * 1024, Extension = ".zip" }
        };

        // Wildcard *.mp4
        var results = FileSearchEngine.Search(items, new SearchCriteria { Query = "*.mp4" });
        Assert.Single(results);
        Assert.Equal("movie.mp4", results[0].Name);

        // Size > 1GB
        var (min, max) = SearchCriteria.ParseSizeConstraint(">1GB");
        var sizeResults = FileSearchEngine.Search(items, new SearchCriteria { MinSize = min, MaxSize = max });
        Assert.Single(sizeResults);
        Assert.Equal("movie.mp4", sizeResults[0].Name);

        // Size 1MB..100MB
        var (min2, max2) = SearchCriteria.ParseSizeConstraint("1MB..100MB");
        var rangeResults = FileSearchEngine.Search(items, new SearchCriteria { MinSize = min2, MaxSize = max2 });
        Assert.Single(rangeResults);
        Assert.Equal("archive.zip", rangeResults[0].Name);
    }

    [Fact]
    public async Task CsvExporter_ExportsValidStandardCsvFormat()
    {
        var root = new FileSystemItem { Name = @"C:\TestFolder", IsDirectory = true, Size = 1200 };
        var child = new FileSystemItem
        {
            Name = "sample.txt",
            Size = 1200,
            AllocatedSize = 4096,
            LastModified = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            Attributes = FileAttributes.Archive,
            IsDirectory = false
        };
        root.AddChild(child);

        string tempFile = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid():N}.csv");
        try
        {
            await CsvExporter.ExportTreeToCsvAsync(root, tempFile);
            string[] lines = await File.ReadAllLinesAsync(tempFile);

            Assert.True(lines.Length >= 3);
            Assert.Equal("FileName,Size,Allocated,Modified,Attributes,Files,Folders", lines[0]);
            Assert.Contains("sample.txt", lines[2]);
            Assert.Contains("1200", lines[2]);
            Assert.Contains("4096", lines[2]);
            Assert.Contains(((int)FileAttributes.Archive).ToString(), lines[2]);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public unsafe void NtfsDataRunDecoder_DecodesCorrectExtents()
    {
        // Construct a sample NTFS data run:
        // Byte 0: 0x21 -> lenBytes = 1, offsetBytes = 2
        // Run 1 length: 0x10 (16 clusters)
        // Run 1 offset delta: 0x0100 (256 LCN)
        // Next run: 0x12 -> lenBytes = 2, offsetBytes = 1
        // Run 2 length: 0x0020 (32 clusters)
        // Run 2 offset delta: 0x10 (+16 LCN -> 272 LCN)
        // Byte N: 0x00 (end of run)

        byte[] runData = [
            0x21, 0x10, 0x00, 0x01,
            0x12, 0x20, 0x00, 0x10,
            0x00
        ];

        fixed (byte* ptr = runData)
        {
            var extents = NtfsDataRunDecoder.DecodeDataRuns(ptr, runData.Length);
            Assert.Equal(2, extents.Count);

            Assert.Equal(256, extents[0].Lcn);
            Assert.Equal(16, extents[0].ClusterCount);

            Assert.Equal(272, extents[1].Lcn);
            Assert.Equal(32, extents[1].ClusterCount);
        }
    }

    [Fact]
    public void FastDirectoryScanner_ScansLocalDirectorySuccessfully()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"DiskAnalyzerScanTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string subDir = Path.Combine(tempDir, "Sub");
        Directory.CreateDirectory(subDir);

        File.WriteAllBytes(Path.Combine(tempDir, "file1.dat"), new byte[100]);
        File.WriteAllBytes(Path.Combine(subDir, "file2.dat"), new byte[250]);

        try
        {
            var scanner = new FastDirectoryScanner();
            var options = new ScanOptions { Path = tempDir };
            var result = scanner.Scan(tempDir, options);

            Assert.NotNull(result);
            Assert.Equal(350, result.Size);
            Assert.Equal(2, result.FileCount);
            Assert.Equal(1, result.FolderCount);

            // Extension breakdown
            var summaries = DiskScanEngine.ComputeExtensionSummaries(result);
            Assert.Single(summaries);
            Assert.Equal(".dat", summaries[0].Extension);
            Assert.Equal(350, summaries[0].TotalSize);
            Assert.Equal(2, summaries[0].FileCount);
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
    public void FastDirectoryScanner_BrowseScanKeepsAbsoluteRootPath()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"DiskAnalyzerBrowsePathTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string filePath = Path.Combine(tempDir, "properties-target.txt");
        File.WriteAllText(filePath, "properties");

        try
        {
            var scanner = new FastDirectoryScanner();
            var result = scanner.Scan(tempDir, new ScanOptions { Path = tempDir });
            var scannedFile = Assert.Single(DiskScanEngine.FlattenFiles(result));

            Assert.True(Path.IsPathFullyQualified(result.GetFullPath()));
            Assert.Equal(Path.GetFullPath(tempDir), result.GetFullPath());
            Assert.Equal(filePath, scannedFile.GetFullPath());
            Assert.True(File.Exists(scannedFile.GetFullPath()));
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
    public void Benchmark_ScanRealDirectory()
    {
        string scanDir = AppContext.BaseDirectory;
        var scanner = new FastDirectoryScanner();
        var options = new ScanOptions { Path = scanDir };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = scanner.Scan(scanDir, options);
        sw.Stop();

        Assert.NotNull(result);
        Assert.True(result.FileCount > 0);
        Assert.True(result.Size > 0);

        var allFiles = DiskScanEngine.FlattenFiles(result);
        Assert.NotEmpty(allFiles);

        var extBreakdown = DiskScanEngine.ComputeExtensionSummaries(result);
        Assert.NotEmpty(extBreakdown);
    }
}

