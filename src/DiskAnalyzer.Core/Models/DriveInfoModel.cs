using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using DiskAnalyzer.Core.Native;

namespace DiskAnalyzer.Core.Models;

/// <summary>
/// Model representing disk drive details, capacity, filesystem type, and NTFS capability.
/// </summary>
public class DriveInfoModel
{
    public string DriveLetter { get; set; } = string.Empty;
    public string VolumePath { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string FileSystemName { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);
    public bool IsMftSupported => string.Equals(FileSystemName, "NTFS", StringComparison.OrdinalIgnoreCase);
    public bool IsAdmin => PrivilegeManager.IsAdministrator;
    public DriveType DriveType { get; set; } = DriveType.Fixed;
    public uint ClusterSize { get; set; }
    public uint SectorSize { get; set; }

    public double UsagePercentage => TotalBytes > 0 ? ((double)UsedBytes / TotalBytes) * 100.0 : 0.0;
    public string UsagePercentageFormatted => $"{UsagePercentage:F1}%";

    public string TotalBytesFormatted => FileSystemItem.FormatBytes(TotalBytes);
    public string FreeBytesFormatted => FileSystemItem.FormatBytes(FreeBytes);
    public string UsedBytesFormatted => FileSystemItem.FormatBytes(UsedBytes);

    public string DisplayTitle
    {
        get
        {
            string name = string.IsNullOrWhiteSpace(Label) ? "Local Disk" : Label;
            return $"{name} ({DriveLetter}) [{FileSystemName} - {TotalBytesFormatted}]";
        }
    }

    public override string ToString() => DisplayTitle;

    /// <summary>
    /// Enumerates all system drives with detailed volume and filesystem information.
    /// </summary>
    public static List<DriveInfoModel> GetDrives()
    {
        var result = new List<DriveInfoModel>();

        try
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (var d in drives)
            {
                try
                {
                    if (!d.IsReady)
                        continue;

                    string root = d.RootDirectory.FullName; // e.g. "C:\"
                    string letter = d.Name.TrimEnd('\\');

                    var model = new DriveInfoModel
                    {
                        DriveLetter = letter,
                        VolumePath = root,
                        Label = d.VolumeLabel,
                        FileSystemName = d.DriveFormat,
                        TotalBytes = d.TotalSize,
                        FreeBytes = d.TotalFreeSpace,
                        DriveType = d.DriveType
                    };

                    // Query NTFS/Win32 geometry if Windows
                    if (OperatingSystem.IsWindows())
                    {
                        TryGetVolumeGeometry(root, model);
                    }

                    result.Add(model);
                }
                catch
                {
                    // Drive might have been disconnected or inaccessible
                }
            }
        }
        catch
        {
            // Fallback
        }

        return result;
    }

    private static unsafe void TryGetVolumeGeometry(string rootPath, DriveInfoModel model)
    {
        try
        {
            char* volName = stackalloc char[261];
            char* fsName = stackalloc char[261];

            if (NativeMethods.GetVolumeInformationW(
                rootPath,
                volName,
                260,
                out uint serial,
                out uint maxComponent,
                out uint flags,
                fsName,
                260))
            {
                string detectedFs = new string(fsName);
                if (!string.IsNullOrEmpty(detectedFs))
                {
                    model.FileSystemName = detectedFs;
                }
            }
        }
        catch
        {
            // Ignore geometry probing failure
        }
    }
}
