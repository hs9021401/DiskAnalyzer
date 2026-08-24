using System;

namespace DiskAnalyzer.Core.Models;

/// <summary>
/// Mode of disk scanning engine.
/// </summary>
public enum ScanMode
{
    /// <summary>
    /// Automatically selects the fastest available mode (DirectMft -> UsnJournal -> FastWalker).
    /// </summary>
    Auto,

    /// <summary>
    /// Direct raw MFT record parser (fastest, requires NTFS & Administrator).
    /// </summary>
    DirectMft,

    /// <summary>
    /// USN Change Journal enumeration (fast, requires NTFS & Administrator).
    /// </summary>
    UsnJournal,

    /// <summary>
    /// Multi-threaded Win32 FindFirstFileExW directory walker (works on all drives, non-admin, folders).
    /// </summary>
    FastWalker
}

/// <summary>
/// Configuration options for scanning a disk volume or folder.
/// </summary>
public class ScanOptions
{
    /// <summary>
    /// Target path to scan (e.g. "C:\", "D:\Games", "\\server\share").
    /// </summary>
    public string Path { get; set; } = "C:\\";

    /// <summary>
    /// Maximum directory depth to scan (null for unlimited).
    /// </summary>
    public int? MaxDepth { get; set; }

    /// <summary>
    /// When scanning a full drive root, includes virtual items for [Free Space] and [System Reserved]
    /// so the total displayed matches the total disk capacity.
    /// </summary>
    public bool IncludeFreeSpaceItem { get; set; } = true;

    /// <summary>
    /// Desired scanning engine strategy.
    /// </summary>
    public ScanMode ScanMode { get; set; } = ScanMode.Auto;

    /// <summary>
    /// Buffer size for raw sector and MFT streaming reads (default 4MB).
    /// </summary>
    public int BufferSize { get; set; } = 4 * 1024 * 1024;

    /// <summary>
    /// Whether to follow directory symlinks / reparse points (default false to prevent infinite loops).
    /// </summary>
    public bool FollowReparsePoints { get; set; } = false;

    /// <summary>
    /// Number of concurrent threads for directory walker mode (default is Environment.ProcessorCount * 2).
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Math.Max(4, Environment.ProcessorCount * 2);
}
