using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiskAnalyzer.Core.Mft;
using DiskAnalyzer.Core.Models;
using DiskAnalyzer.Core.Native;

namespace DiskAnalyzer.Core.Scanning;

/// <summary>
/// Main scanning orchestrator for DiskAnalyzer.
/// Automatically detects file system type, elevated privileges, and selects the fastest scanning strategy.
/// </summary>
public class DiskScanEngine
{
    private readonly Func<string, ScanOptions, IProgress<ScanProgress>?, CancellationToken, FileSystemItem> _scanMft;
    private readonly Func<string, ScanOptions, IProgress<ScanProgress>?, CancellationToken, FileSystemItem> _scanUsn;
    private readonly Func<string, ScanOptions, IProgress<ScanProgress>?, CancellationToken, FileSystemItem> _scanDirectory;

    public DiskScanEngine()
    {
        var mftReader = new NtfsMftReader();
        var usnReader = new NtfsUsnReader();
        var directoryScanner = new FastDirectoryScanner();
        _scanMft = (path, options, progress, token) => mftReader.ReadDrive(path, options, progress, token);
        _scanUsn = (path, options, progress, token) => usnReader.ReadDrive(path, options, progress, token);
        _scanDirectory = (path, options, progress, token) => directoryScanner.Scan(path, options, progress, token);
    }

    internal DiskScanEngine(
        Func<string, ScanOptions, IProgress<ScanProgress>?, CancellationToken, FileSystemItem> scanMft,
        Func<string, ScanOptions, IProgress<ScanProgress>?, CancellationToken, FileSystemItem> scanUsn,
        Func<string, ScanOptions, IProgress<ScanProgress>?, CancellationToken, FileSystemItem> scanDirectory)
    {
        _scanMft = scanMft ?? throw new ArgumentNullException(nameof(scanMft));
        _scanUsn = scanUsn ?? throw new ArgumentNullException(nameof(scanUsn));
        _scanDirectory = scanDirectory ?? throw new ArgumentNullException(nameof(scanDirectory));
    }

    /// <summary>
    /// Executes a scan asynchronously with automatic strategy selection.
    /// </summary>
    public Task<FileSystemItem> ScanAsync(
        ScanOptions options,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Scan(options, progress, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Executes a scan on a specific drive or path with default options asynchronously.
    /// </summary>
    public Task<FileSystemItem> ScanPathAsync(
        string path,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var options = new ScanOptions { Path = path };
        return ScanAsync(options, progress, cancellationToken);
    }

    /// <summary>
    /// Synchronously scans a drive or directory hierarchy using the optimal engine.
    /// </summary>
    public FileSystemItem Scan(
        ScanOptions options,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string targetPath = options.Path;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            targetPath = "C:\\";
        }

        string root = Path.GetPathRoot(targetPath) ?? "C:\\";
        bool isFullDrive = string.Equals(
            Path.GetFullPath(targetPath).TrimEnd('\\'),
            Path.GetFullPath(root).TrimEnd('\\'),
            StringComparison.OrdinalIgnoreCase);

        bool isAdmin = PrivilegeManager.IsAdministrator;
        bool isNtfs = IsDriveNtfs(root);

        ScanMode effectiveMode = options.ScanMode;

        if (effectiveMode == ScanMode.Auto)
        {
            if (OperatingSystem.IsWindows() && isFullDrive && isNtfs && isAdmin)
            {
                effectiveMode = ScanMode.DirectMft;
            }
            else
            {
                effectiveMode = ScanMode.FastWalker;
            }
        }

        // USN enumerates a volume, not an arbitrary subtree. Respect folder scope.
        if (effectiveMode == ScanMode.UsnJournal && !isFullDrive)
            effectiveMode = ScanMode.FastWalker;

        // Try the selected mode and fall back directly to the directory walker when needed.
        if (effectiveMode == ScanMode.DirectMft)
        {
            try
            {
                return _scanMft(targetPath, options, progress, cancellationToken);
            }
            catch (Exception mftException) when (mftException is not OperationCanceledException)
            {
                TraceFallback(ScanMode.DirectMft, ScanMode.FastWalker, mftException);
                return _scanDirectory(targetPath, options, progress, cancellationToken);
            }
        }
        else if (effectiveMode == ScanMode.UsnJournal)
        {
            try
            {
                return _scanUsn(targetPath, options, progress, cancellationToken);
            }
            catch (Exception usnException) when (usnException is not OperationCanceledException)
            {
                TraceFallback(ScanMode.UsnJournal, ScanMode.FastWalker, usnException);
                return _scanDirectory(targetPath, options, progress, cancellationToken);
            }
        }
        else
        {
            return _scanDirectory(targetPath, options, progress, cancellationToken);
        }
    }

    /// <summary>
    /// Computes aggregated statistics grouped by file extension.
    /// </summary>
    public static List<ExtensionSummary> ComputeExtensionSummaries(FileSystemItem root)
    {
        var summaries = new Dictionary<string, ExtensionSummary>(StringComparer.OrdinalIgnoreCase);
        long totalSize = root.Size > 0 ? root.Size : 1;

        void Traverse(FileSystemItem item)
        {
            if (item.IsVirtual) return;

            if (!item.IsDirectory)
            {
                string ext = string.IsNullOrEmpty(item.Extension)
                    ? "[No Extension]"
                    : item.Extension.ToLowerInvariant();

                if (!summaries.TryGetValue(ext, out var summary))
                {
                    summary = new ExtensionSummary
                    {
                        Extension = ext,
                        ColorHex = ExtensionSummary.GetColorForExtension(ext)
                    };
                    summaries[ext] = summary;
                }

                summary.TotalSize += item.Size;
                summary.AllocatedSize += item.AllocatedSize;
                summary.FileCount++;
            }
            else if (item.HasChildren)
            {
                foreach (var child in item.Children)
                {
                    Traverse(child);
                }
            }
        }

        Traverse(root);

        var list = summaries.Values.ToList();
        foreach (var summary in list)
        {
            summary.Percentage = Math.Clamp(((double)summary.TotalSize / totalSize) * 100.0, 0.0, 100.0);
        }

        list.Sort((a, b) => b.TotalSize.CompareTo(a.TotalSize));
        return list;
    }

    /// <summary>
    /// Flattens all file nodes into a single list for fast searching and tabular display.
    /// </summary>
    public static List<FileSystemItem> FlattenFiles(FileSystemItem root)
    {
        var list = new List<FileSystemItem>(10_000);

        void Collect(FileSystemItem item)
        {
            if (!item.IsDirectory && !item.IsVirtual)
            {
                list.Add(item);
            }
            else if (item.HasChildren)
            {
                foreach (var child in item.Children)
                {
                    Collect(child);
                }
            }
        }

        Collect(root);
        return list;
    }

    /// <summary>
    /// Flattens all nodes (files and folders) into a list.
    /// </summary>
    public static List<FileSystemItem> FlattenAll(FileSystemItem root)
    {
        var list = new List<FileSystemItem>(10_000);

        void Collect(FileSystemItem item)
        {
            list.Add(item);
            if (item.HasChildren)
            {
                foreach (var child in item.Children)
                {
                    Collect(child);
                }
            }
        }

        Collect(root);
        return list;
    }

    private static bool IsDriveNtfs(string rootPath)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            var drive = new DriveInfo(rootPath);
            return string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void TraceFallback(ScanMode failedMode, ScanMode fallbackMode, Exception exception)
    {
        Trace.TraceWarning(
            "DiskAnalyzer scan fallback from {0} to {1}: {2} (0x{3:X8})",
            failedMode,
            fallbackMode,
            exception.GetType().Name,
            exception.HResult);
    }
}
