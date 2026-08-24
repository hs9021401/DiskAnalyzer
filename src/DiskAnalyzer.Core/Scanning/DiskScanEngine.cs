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
    private readonly NtfsMftReader _mftReader = new();
    private readonly NtfsUsnReader _usnReader = new();
    private readonly FastDirectoryScanner _dirScanner = new();

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
            if (OperatingSystem.IsWindows() && isNtfs && isAdmin)
            {
                effectiveMode = ScanMode.DirectMft;
            }
            else
            {
                effectiveMode = ScanMode.FastWalker;
            }
        }

        // Try selected mode with fallback chain
        if (effectiveMode == ScanMode.DirectMft)
        {
            try
            {
                return _mftReader.ReadDrive(targetPath, options, progress, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Fallback to USN Journal
                try
                {
                    return _usnReader.ReadDrive(targetPath, options, progress, cancellationToken);
                }
                catch
                {
                    // Fallback to multi-threaded FastDirectoryScanner
                    return _dirScanner.Scan(targetPath, options, progress, cancellationToken);
                }
            }
        }
        else if (effectiveMode == ScanMode.UsnJournal)
        {
            try
            {
                return _usnReader.ReadDrive(targetPath, options, progress, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return _dirScanner.Scan(targetPath, options, progress, cancellationToken);
            }
        }
        else
        {
            return _dirScanner.Scan(targetPath, options, progress, cancellationToken);
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
}
