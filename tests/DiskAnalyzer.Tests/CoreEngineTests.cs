using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
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
    public void CoreTypes_AreLoadedFromTheCoreAssembly()
    {
        Assert.Equal("DiskAnalyzer.Core", typeof(DiskScanEngine).Assembly.GetName().Name);
        Assert.Equal(typeof(DiskScanEngine).Assembly, typeof(ShellOperations).Assembly);
    }

    [Fact]
    public void SearchEngine_CancelsDuringEnumerationAndStopsAtTheResultLimit()
    {
        using var cts = new CancellationTokenSource();
        IEnumerable<FileSystemItem> CancelDuringEnumeration()
        {
            yield return new FileSystemItem { Name = "first" };
            cts.Cancel();
            yield return new FileSystemItem { Name = "second" };
        }
        Assert.Throws<OperationCanceledException>(() =>
            FileSearchEngine.Search(CancelDuringEnumeration(), new SearchCriteria(), cts.Token));

        IEnumerable<FileSystemItem> OnlyReadOne()
        {
            yield return new FileSystemItem { Name = "first" };
            throw new InvalidOperationException("Search read beyond its result limit.");
        }
        var result = Assert.Single(FileSearchEngine.Search(OnlyReadOne(), new SearchCriteria(),
            CancellationToken.None, maxResults: 1));
        Assert.Equal("first", result.Name);
    }

    [Fact]
    public void SearchEngine_EmptyExtensionMeansFilesWithoutAnExtension()
    {
        FileSystemItem[] items =
        [
            new() { Name = "README", Extension = "" },
            new() { Name = "notes.txt", Extension = ".txt" }
        ];
        Assert.Equal("README", Assert.Single(FileSearchEngine.Search(items,
            new SearchCriteria { Extension = "" })).Name);
        Assert.Equal(2, FileSearchEngine.Search(items, new SearchCriteria()).Count);
    }

    [Fact]
    public void NtfsUsnReader_ParsesUnicodeV2RecordsAndRejectsInvalidBounds()
    {
        byte[] name = Encoding.Unicode.GetBytes("測試.TXT");
        byte[] record = new byte[60 + name.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(record, (uint)record.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(8), 0x001200000000002A);
        BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(16), 5);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(52), (uint)FileAttributes.Archive);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(56), (ushort)name.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(58), 60);
        name.CopyTo(record, 60);

        var item = NtfsUsnReader.ParseRecord(record, out int consumed);
        Assert.Equal(record.Length, consumed);
        Assert.Equal("測試.TXT", item.Name);
        Assert.Equal(42UL, item.FileRecordNumber);
        Assert.Equal(5UL, item.ParentRecordNumber);
        Assert.False(item.IsDirectory);
        Assert.Equal(".TXT", item.Extension);

        Assert.Throws<InvalidDataException>(() => NtfsUsnReader.ParseRecord(record.AsSpan(0, 59), out _));
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(58), (ushort)record.Length);
        Assert.Throws<InvalidDataException>(() => NtfsUsnReader.ParseRecord(record, out _));
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(58), 60);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(4), 3);
        Assert.Throws<NotSupportedException>(() => NtfsUsnReader.ParseRecord(record, out _));
        BinaryPrimitives.WriteUInt32LittleEndian(record, 0);
        Assert.Throws<InvalidDataException>(() => NtfsUsnReader.ParseRecord(record, out _));
    }

    [Fact]
    public void ScanReaders_PreservePreCancelledRequests()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            new NtfsMftReader().ReadDrive(@"C:\", cancellationToken: cts.Token));
        Assert.Throws<OperationCanceledException>(() =>
            new NtfsUsnReader().ReadDrive(@"C:\", cancellationToken: cts.Token));
        Assert.Throws<OperationCanceledException>(() =>
            new FastDirectoryScanner().Scan(AppContext.BaseDirectory, cancellationToken: cts.Token));
        Assert.Throws<OperationCanceledException>(() =>
            new DiskScanEngine().Scan(new ScanOptions { Path = AppContext.BaseDirectory }, cancellationToken: cts.Token));
    }

    [Fact]
    public void PrivilegeManager_RejectsUnknownAndEmptyPrivilegeNames()
    {
        Assert.False(PrivilegeManager.EnablePrivilege(""));
        Assert.False(PrivilegeManager.EnablePrivilege("DiskAnalyzer_InvalidPrivilege"));
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        Assert.Equal(principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator),
            PrivilegeManager.IsAdministrator);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void ShellDeletion_FailureNeverFallsBackToDirectDeletion(bool recycle, bool confirm)
    {
        string path = Path.Combine(Path.GetTempPath(), $"DiskAnalyzerDeleteTest_{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "preserve this file");
        try
        {
            int calls = 0;
            int FailShellOperation(ref NativeMethods.SHFILEOPSTRUCTW operation)
            {
                calls++;
                Assert.Equal(path + "\0\0", operation.pFrom);
                Assert.Equal(recycle, (operation.fFlags & NativeMethods.FOF_ALLOWUNDO) != 0);
                Assert.Equal(!confirm, (operation.fFlags & NativeMethods.FOF_NOCONFIRMATION) != 0);
                return 5;
            }
            Assert.False(ShellOperations.DeleteUsingShell(path, confirm, recycle, FailShellOperation));
            Assert.Equal(1, calls);
            Assert.Equal("preserve this file", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(ScanMode.Auto)]
    [InlineData(ScanMode.UsnJournal)]
    public void DiskScanEngine_FolderScanNeverIncludesTheRestOfTheVolume(ScanMode mode)
    {
        string path = Path.Combine(Path.GetTempPath(), $"DiskAnalyzerScopeTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        string file = Path.Combine(path, "only.bin");
        File.WriteAllBytes(file, new byte[123]);
        try
        {
            var result = new DiskScanEngine().Scan(new ScanOptions { Path = path, ScanMode = mode });
            Assert.Equal(123, result.Size);
            Assert.Equal(file, Assert.Single(DiskScanEngine.FlattenFiles(result)).GetFullPath());
        }
        finally { Directory.Delete(path, true); }
    }

    [Fact]
    public void DiskScanEngine_DirectMftFailureFallsBackStraightToWalker()
    {
        int mftCalls = 0;
        int usnCalls = 0;
        int walkerCalls = 0;
        var engine = new DiskScanEngine(
            (_, _, _, _) => { mftCalls++; throw new IOException("MFT failure"); },
            (_, _, _, _) => { usnCalls++; return new FileSystemItem { Name = "USN" }; },
            (_, _, _, _) => { walkerCalls++; return new FileSystemItem { Name = "Walker" }; });

        var result = engine.Scan(new ScanOptions { Path = @"C:\", ScanMode = ScanMode.DirectMft });

        Assert.Equal("Walker", result.Name);
        Assert.Equal(1, mftCalls);
        Assert.Equal(0, usnCalls);
        Assert.Equal(1, walkerCalls);
    }

    [Fact]
    public void DiskScanEngine_SuccessfulDirectMftDoesNotFallback()
    {
        int mftCalls = 0;
        int usnCalls = 0;
        int walkerCalls = 0;
        var engine = new DiskScanEngine(
            (_, _, _, _) => { mftCalls++; return new FileSystemItem { Name = "MFT" }; },
            (_, _, _, _) => { usnCalls++; return new FileSystemItem { Name = "USN" }; },
            (_, _, _, _) => { walkerCalls++; return new FileSystemItem { Name = "Walker" }; });

        var result = engine.Scan(new ScanOptions { Path = @"C:\", ScanMode = ScanMode.DirectMft });

        Assert.Equal("MFT", result.Name);
        Assert.Equal(1, mftCalls);
        Assert.Equal(0, usnCalls);
        Assert.Equal(0, walkerCalls);
    }

    [Fact]
    public void DiskScanEngine_DirectMftCancellationDoesNotFallback()
    {
        int usnCalls = 0;
        int walkerCalls = 0;
        var engine = new DiskScanEngine(
            (_, _, _, _) => throw new OperationCanceledException(),
            (_, _, _, _) => { usnCalls++; return new FileSystemItem(); },
            (_, _, _, _) => { walkerCalls++; return new FileSystemItem(); });

        Assert.Throws<OperationCanceledException>(() =>
            engine.Scan(new ScanOptions { Path = @"C:\", ScanMode = ScanMode.DirectMft }));
        Assert.Equal(0, usnCalls);
        Assert.Equal(0, walkerCalls);
    }

    [Fact]
    public void DiskScanEngine_ExplicitUsnModeRunsUsnReader()
    {
        int mftCalls = 0;
        int usnCalls = 0;
        int walkerCalls = 0;
        var engine = new DiskScanEngine(
            (_, _, _, _) => { mftCalls++; return new FileSystemItem { Name = "MFT" }; },
            (_, _, _, _) => { usnCalls++; return new FileSystemItem { Name = "USN" }; },
            (_, _, _, _) => { walkerCalls++; return new FileSystemItem { Name = "Walker" }; });

        var result = engine.Scan(new ScanOptions { Path = @"C:\", ScanMode = ScanMode.UsnJournal });

        Assert.Equal("USN", result.Name);
        Assert.Equal(0, mftCalls);
        Assert.Equal(1, usnCalls);
        Assert.Equal(0, walkerCalls);
    }

    [Fact]
    public void DiskScanEngine_ExplicitUsnFailureFallsBackToWalker()
    {
        int usnCalls = 0;
        int walkerCalls = 0;
        var engine = new DiskScanEngine(
            (_, _, _, _) => throw new InvalidOperationException("Unexpected MFT call"),
            (_, _, _, _) => { usnCalls++; throw new IOException("USN failure"); },
            (_, _, _, _) => { walkerCalls++; return new FileSystemItem { Name = "Walker" }; });

        var result = engine.Scan(new ScanOptions { Path = @"C:\", ScanMode = ScanMode.UsnJournal });

        Assert.Equal("Walker", result.Name);
        Assert.Equal(1, usnCalls);
        Assert.Equal(1, walkerCalls);
    }

    [Fact]
    public void DiskScanEngine_ExplicitUsnCancellationDoesNotFallback()
    {
        int walkerCalls = 0;
        var engine = new DiskScanEngine(
            (_, _, _, _) => throw new InvalidOperationException("Unexpected MFT call"),
            (_, _, _, _) => throw new OperationCanceledException(),
            (_, _, _, _) => { walkerCalls++; return new FileSystemItem(); });

        Assert.Throws<OperationCanceledException>(() =>
            engine.Scan(new ScanOptions { Path = @"C:\", ScanMode = ScanMode.UsnJournal }));
        Assert.Equal(0, walkerCalls);
    }

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
    public void FileSystemItem_RecalculateAggregateStatistics_UsesTheEntireSubtree()
    {
        var root = new FileSystemItem { Name = "Root", IsDirectory = true };
        var folder = new FileSystemItem { Name = "Folder", IsDirectory = true };
        folder.AddChild(new FileSystemItem { Name = "first.bin", Size = 100, AllocatedSize = 4096 });
        folder.AddChild(new FileSystemItem { Name = "second.bin", Size = 250, AllocatedSize = 4096 });
        root.AddChild(folder);
        root.AddChild(new FileSystemItem { Name = "third.bin", Size = 50, AllocatedSize = 4096 });

        root.RecalculateAggregateStatistics();

        Assert.Equal(400, root.Size);
        Assert.Equal(12_288, root.AllocatedSize);
        Assert.Equal(3, root.FileCount);
        Assert.Equal(1, root.FolderCount);
        Assert.Equal(350, folder.Size);
        Assert.Equal(2, folder.FileCount);
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
    public void FastDirectoryScanner_VisitsEveryDirectoryUnderConcurrentLoad()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"DiskAnalyzerConcurrentScan_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        const int branchCount = 16;
        const int depthPerBranch = 4;
        long expectedSize = 0;
        int expectedFiles = 0;
        int expectedFolders = 0;

        try
        {
            for (int branch = 0; branch < branchCount; branch++)
            {
                string current = Path.Combine(tempDir, $"branch-{branch:D2}");
                Directory.CreateDirectory(current);
                expectedFolders++;

                for (int depth = 0; depth < depthPerBranch; depth++)
                {
                    current = Path.Combine(current, $"level-{depth:D2}");
                    Directory.CreateDirectory(current);
                    expectedFolders++;

                    int fileSize = branch + depth + 1;
                    File.WriteAllBytes(Path.Combine(current, $"file-{branch:D2}-{depth:D2}.bin"), new byte[fileSize]);
                    expectedFiles++;
                    expectedSize += fileSize;
                }
            }

            var scanner = new FastDirectoryScanner();
            var options = new ScanOptions { Path = tempDir, MaxDegreeOfParallelism = 16 };

            for (int attempt = 0; attempt < 10; attempt++)
            {
                var result = scanner.Scan(tempDir, options);

                Assert.Equal(expectedSize, result.Size);
                Assert.Equal(expectedFiles, result.FileCount);
                Assert.Equal(expectedFolders, result.FolderCount);
            }
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
    public void ComputeExtensionSummaries_UsesCanonicalExtensionLabels()
    {
        var root = new FileSystemItem { Name = "Root", IsDirectory = true };
        root.AddChild(new FileSystemItem { Name = "Report.TXT", Extension = ".TXT", Size = 100 });
        root.AddChild(new FileSystemItem { Name = "photo.txt", Extension = ".txt", Size = 200 });
        root.AddChild(new FileSystemItem { Name = "README", Extension = string.Empty, Size = 50 });
        root.RecalculateAggregateStatistics();

        var summaries = DiskScanEngine.ComputeExtensionSummaries(root);

        var text = Assert.Single(summaries, summary => summary.Extension == ".txt");
        Assert.Equal(300, text.TotalSize);
        Assert.Equal(2, text.FileCount);

        var noExtension = Assert.Single(summaries, summary => summary.Extension == "[No Extension]");
        Assert.Equal(50, noExtension.TotalSize);
        Assert.Equal(1, noExtension.FileCount);
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
