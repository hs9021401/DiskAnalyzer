using System;

namespace DiskAnalyzer.Core.Models;

/// <summary>
/// Execution phases of the disk scanning process.
/// </summary>
public enum ScanPhase
{
    Initializing,
    ReadingMft,
    ReadingUsnJournal,
    ScanningDirectories,
    BuildingTree,
    CalculatingSizes,
    Sorting,
    Complete,
    Cancelled,
    Error
}

/// <summary>
/// Progress reporting model for disk scanning operations.
/// </summary>
public class ScanProgress
{
    public ScanPhase Phase { get; set; } = ScanPhase.Initializing;
    public long FilesScanned { get; set; }
    public long FoldersScanned { get; set; }
    public long TotalItemsScanned => FilesScanned + FoldersScanned;
    public long TotalBytes { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public string CurrentFolder { get; set; } = string.Empty;
    public double? PercentComplete { get; set; }

    public double SpeedItemsPerSecond => ElapsedTime.TotalSeconds > 0.05
        ? TotalItemsScanned / ElapsedTime.TotalSeconds
        : 0;

    public double SpeedBytesPerSecond => ElapsedTime.TotalSeconds > 0.05
        ? TotalBytes / ElapsedTime.TotalSeconds
        : 0;

    public string TotalBytesFormatted => FileSystemItem.FormatBytes(TotalBytes);
    public string FilesScannedFormatted => FilesScanned.ToString("N0");
    public string FoldersScannedFormatted => FoldersScanned.ToString("N0");
    public string SpeedItemsFormatted => $"{SpeedItemsPerSecond:N0} items/s";
    public string SpeedBytesFormatted => $"{FileSystemItem.FormatBytes((long)SpeedBytesPerSecond)}/s";

    public string StatusMessage => Phase switch
    {
        ScanPhase.Initializing => "Initializing scan...",
        ScanPhase.ReadingMft => $"Reading NTFS MFT records ({FilesScannedFormatted} files, {FoldersScannedFormatted} folders)...",
        ScanPhase.ReadingUsnJournal => $"Reading USN Change Journal ({FilesScannedFormatted} items)...",
        ScanPhase.ScanningDirectories => $"Scanning: {CurrentFolder}",
        ScanPhase.BuildingTree => "Building directory hierarchy...",
        ScanPhase.CalculatingSizes => "Aggregating folder sizes and file counts...",
        ScanPhase.Sorting => "Sorting tree items...",
        ScanPhase.Complete => $"Scan complete in {ElapsedTime.TotalSeconds:F2}s ({FilesScannedFormatted} files, {FoldersScannedFormatted} folders)",
        ScanPhase.Cancelled => "Scan cancelled.",
        ScanPhase.Error => "Scan encountered an error.",
        _ => "Scanning..."
    };

    public override string ToString() => StatusMessage;
}
