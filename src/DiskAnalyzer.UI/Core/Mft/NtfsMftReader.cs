using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using DiskAnalyzer.Core.Models;
using DiskAnalyzer.Core.Native;
using Microsoft.Win32.SafeHandles;

namespace DiskAnalyzer.Core.Mft;

/// <summary>
/// Ultra-fast raw NTFS Master File Table ($MFT) direct parser.
/// Reads raw MFT records directly from the volume device handle in multi-megabyte streams.
/// </summary>
public class NtfsMftReader
{
    private const ulong MFT_RECORD_ROOT = 5; // NTFS root folder record number is always 5

    /// <summary>
    /// Reads and parses the entire NTFS MFT for the specified drive.
    /// </summary>
    public unsafe FileSystemItem ReadDrive(string drivePath, ScanOptions? options = null, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        options ??= new ScanOptions();
        var sw = Stopwatch.StartNew();

        string driveLetter = Path.GetPathRoot(drivePath)?.TrimEnd('\\') ?? "C:";
        string volumePath = $@"\\.\{driveLetter}";

        // Enable Windows Backup privileges
        PrivilegeManager.EnableBackupPrivileges();

        progress?.Report(new ScanProgress
        {
            Phase = ScanPhase.Initializing,
            CurrentFolder = drivePath,
            ElapsedTime = sw.Elapsed
        });

        // Open volume handle
        using SafeFileHandle volumeHandle = NativeMethods.CreateFileW(
            volumePath,
            NativeMethods.GENERIC_READ,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            NativeMethods.FILE_FLAG_NO_BUFFERING | NativeMethods.FILE_FLAG_RANDOM_ACCESS,
            IntPtr.Zero);

        if (volumeHandle.IsInvalid)
        {
            int err = Marshal.GetLastWin32Error();
            throw new UnauthorizedAccessException($"Failed to open NTFS volume '{volumePath}'. Error code: {err}. Ensure application is running as Administrator.");
        }

        // Query NTFS Volume Data
        var volumeData = new NativeMethods.NTFS_VOLUME_DATA_BUFFER();
        uint returnedBytes = 0;
        if (!NativeMethods.DeviceIoControl(
            volumeHandle,
            NativeMethods.FSCTL_GET_NTFS_VOLUME_DATA,
            null,
            0,
            &volumeData,
            (uint)sizeof(NativeMethods.NTFS_VOLUME_DATA_BUFFER),
            out returnedBytes,
            IntPtr.Zero))
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"FSCTL_GET_NTFS_VOLUME_DATA failed on '{volumePath}'. Error code: {err}. Drive may not be NTFS.");
        }

        uint bytesPerSector = volumeData.BytesPerSector;
        uint bytesPerCluster = volumeData.BytesPerCluster;
        uint bytesPerRecord = volumeData.BytesPerFileRecordSegment;
        long mftStartLcn = volumeData.MftStartLcn;
        long mftValidLength = volumeData.MftValidDataLength;

        if (bytesPerSector == 0 || bytesPerCluster == 0 || bytesPerRecord == 0)
        {
            throw new InvalidOperationException("Invalid volume geometry retrieved from NTFS.");
        }

        // Step 1: Read MFT Record 0 ($MFT itself) to parse its $DATA extents
        List<NtfsExtent> mftExtents = ReadMftExtents(volumeHandle, mftStartLcn, bytesPerCluster, bytesPerSector, bytesPerRecord);
        if (mftExtents.Count == 0)
        {
            throw new InvalidOperationException("Failed to decode MFT data runlist from Record 0.");
        }

        progress?.Report(new ScanProgress
        {
            Phase = ScanPhase.ReadingMft,
            CurrentFolder = drivePath,
            ElapsedTime = sw.Elapsed
        });

        // Step 2: Stream the entire MFT using large aligned read buffers
        var rawRecords = new Dictionary<ulong, MftRawRecord>(100_000);
        var extensionRecords = new List<MftRawRecord>();

        int bufferSize = Math.Max(options.BufferSize, (int)(bytesPerCluster * 64)); // Align to clusters
        bufferSize = (int)((bufferSize / bytesPerCluster) * bytesPerCluster);
        byte[] readBuffer = GC.AllocateUninitializedArray<byte>(bufferSize, pinned: true);

        long totalRecordsToRead = mftValidLength > 0 ? (mftValidLength / bytesPerRecord) : 0;
        long recordsParsed = 0;
        long filesCount = 0;
        long foldersCount = 0;
        long totalBytesScanned = 0;

        fixed (byte* bufPtr = readBuffer)
        {
            long currentRecordIndex = 0;

            foreach (var extent in mftExtents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (extent.IsSparse)
                {
                    currentRecordIndex += (extent.ClusterCount * bytesPerCluster) / bytesPerRecord;
                    continue;
                }

                long extentOffset = extent.Lcn * bytesPerCluster;
                long extentBytes = extent.ClusterCount * bytesPerCluster;
                long extentBytesRead = 0;

                while (extentBytesRead < extentBytes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    uint bytesToRead = (uint)Math.Min(bufferSize, extentBytes - extentBytesRead);
                    long fileOffset = extentOffset + extentBytesRead;

                    if (!NativeMethods.SetFilePointerEx(volumeHandle, fileOffset, out _, 0 /* FILE_BEGIN */))
                    {
                        break;
                    }

                    if (!NativeMethods.ReadFile(volumeHandle, bufPtr, bytesToRead, out uint bytesRead, IntPtr.Zero) || bytesRead == 0)
                    {
                        break;
                    }

                    int recordsInBuffer = (int)(bytesRead / bytesPerRecord);
                    for (int i = 0; i < recordsInBuffer; i++)
                    {
                        byte* recordPtr = bufPtr + (i * bytesPerRecord);
                        ulong recordNumber = (ulong)(currentRecordIndex + i);

                        var rawRecord = ParseRecord(recordPtr, recordNumber, bytesPerSector, bytesPerRecord);
                        if (rawRecord != null)
                        {
                            if (rawRecord.ParentRecordNumber != 0 || rawRecord.RecordNumber == MFT_RECORD_ROOT)
                            {
                                rawRecords[rawRecord.RecordNumber] = rawRecord;
                                if (rawRecord.IsDirectory)
                                    foldersCount++;
                                else
                                {
                                    filesCount++;
                                    totalBytesScanned += rawRecord.Size;
                                }
                            }
                            else
                            {
                                extensionRecords.Add(rawRecord);
                            }
                        }

                        recordsParsed++;
                    }

                    currentRecordIndex += recordsInBuffer;
                    extentBytesRead += bytesRead;

                    if (sw.ElapsedMilliseconds % 200 < 20)
                    {
                        progress?.Report(new ScanProgress
                        {
                            Phase = ScanPhase.ReadingMft,
                            FilesScanned = filesCount,
                            FoldersScanned = foldersCount,
                            TotalBytes = totalBytesScanned,
                            ElapsedTime = sw.Elapsed,
                            PercentComplete = totalRecordsToRead > 0 ? ((double)recordsParsed / totalRecordsToRead) * 100.0 : null
                        });
                    }
                }
            }
        }

        // Step 3: Build the FileSystemItem hierarchy
        progress?.Report(new ScanProgress
        {
            Phase = ScanPhase.BuildingTree,
            FilesScanned = filesCount,
            FoldersScanned = foldersCount,
            TotalBytes = totalBytesScanned,
            ElapsedTime = sw.Elapsed
        });

        var itemMap = new Dictionary<ulong, FileSystemItem>(rawRecords.Count);

        // Create FileSystemItem nodes
        foreach (var kvp in rawRecords)
        {
            var raw = kvp.Value;
            var item = new FileSystemItem
            {
                Name = raw.Name,
                Size = raw.Size,
                AllocatedSize = raw.AllocatedSize,
                FileRecordNumber = raw.RecordNumber,
                ParentRecordNumber = raw.ParentRecordNumber,
                Attributes = raw.Attributes,
                LastModified = raw.LastModified,
                IsDirectory = raw.IsDirectory,
                Extension = raw.IsDirectory ? string.Empty : Path.GetExtension(raw.Name)
            };

            itemMap[raw.RecordNumber] = item;
        }

        // Identify or create Root node
        if (!itemMap.TryGetValue(MFT_RECORD_ROOT, out var rootItem))
        {
            rootItem = new FileSystemItem
            {
                Name = driveLetter.EndsWith('\\') ? driveLetter : (driveLetter + "\\"),
                IsDirectory = true,
                FileRecordNumber = MFT_RECORD_ROOT,
                ParentRecordNumber = MFT_RECORD_ROOT
            };
            itemMap[MFT_RECORD_ROOT] = rootItem;
        }
        else
        {
            rootItem.Name = driveLetter.EndsWith('\\') ? driveLetter : (driveLetter + "\\");
            rootItem.IsDirectory = true;
        }

        rootItem.RootPath = driveLetter.EndsWith('\\') ? driveLetter : (driveLetter + "\\");

        // Link child items to parents
        foreach (var kvp in itemMap)
        {
            var item = kvp.Value;
            if (item.FileRecordNumber == MFT_RECORD_ROOT)
                continue;

            ulong parentFrn = item.ParentRecordNumber;
            if (parentFrn != item.FileRecordNumber && itemMap.TryGetValue(parentFrn, out var parentItem))
            {
                parentItem.AddChild(item);
            }
            else
            {
                // Orphaned or root-level item
                rootItem.AddChild(item);
            }
        }

        // If scanning a specific subfolder (e.g. "C:\Users\alex"), locate subfolder node
        FileSystemItem effectiveRoot = rootItem;
        string relativeSubPath = GetRelativeSubPath(driveLetter, drivePath);
        if (!string.IsNullOrEmpty(relativeSubPath))
        {
            var found = FindSubItem(rootItem, relativeSubPath);
            if (found != null)
            {
                found.Parent = null; // Detach as root
                string normalizedTargetPath = Path.GetFullPath(drivePath).TrimEnd('\\');
                if (normalizedTargetPath.EndsWith(':'))
                {
                    normalizedTargetPath += "\\";
                }
                found.RootPath = normalizedTargetPath;
                effectiveRoot = found;
            }
        }

        // Step 4: Aggregate sizes, counts, percentages
        progress?.Report(new ScanProgress
        {
            Phase = ScanPhase.CalculatingSizes,
            FilesScanned = filesCount,
            FoldersScanned = foldersCount,
            TotalBytes = totalBytesScanned,
            ElapsedTime = sw.Elapsed
        });

        PostOrderAggregate(effectiveRoot);

        // Step 5: Sorting
        progress?.Report(new ScanProgress
        {
            Phase = ScanPhase.Sorting,
            FilesScanned = filesCount,
            FoldersScanned = foldersCount,
            TotalBytes = totalBytesScanned,
            ElapsedTime = sw.Elapsed
        });

        effectiveRoot.CalculateChildPercentages(true);
        effectiveRoot.SortChildrenBySizeDescending(true);

        // Add Virtual Free Space items if scanning root volume
        if (options.IncludeFreeSpaceItem && string.IsNullOrEmpty(relativeSubPath))
        {
            AddVirtualVolumeItems(effectiveRoot, driveLetter, volumeData);
        }

        progress?.Report(new ScanProgress
        {
            Phase = ScanPhase.Complete,
            FilesScanned = effectiveRoot.FileCount,
            FoldersScanned = effectiveRoot.FolderCount,
            TotalBytes = effectiveRoot.Size,
            ElapsedTime = sw.Elapsed
        });

        return effectiveRoot;
    }

    private static unsafe List<NtfsExtent> ReadMftExtents(
        SafeFileHandle volumeHandle,
        long mftStartLcn,
        uint bytesPerCluster,
        uint bytesPerSector,
        uint bytesPerRecord)
    {
        var extents = new List<NtfsExtent>();
        uint bufferSize = Math.Max(bytesPerCluster, bytesPerRecord);
        byte[] recordBuffer = GC.AllocateUninitializedArray<byte>((int)bufferSize, pinned: true);

        fixed (byte* bufPtr = recordBuffer)
        {
            long record0Offset = mftStartLcn * bytesPerCluster;
            if (!NativeMethods.SetFilePointerEx(volumeHandle, record0Offset, out _, 0 /* FILE_BEGIN */))
            {
                return extents;
            }

            if (!NativeMethods.ReadFile(volumeHandle, bufPtr, bufferSize, out uint bytesRead, IntPtr.Zero) || bytesRead < bytesPerRecord)
            {
                return extents;
            }

            // Apply fixup on Record 0
            if (!NtfsDataRunDecoder.ApplyFixup(bufPtr, bytesPerSector, bytesPerRecord))
            {
                return extents;
            }

            var header = (NativeMethods.MFT_RECORD_HEADER*)bufPtr;
            if (header->Magic != 0x454C4946)
                return extents;

            // Iterate attributes to find unnamed $DATA (0x80)
            int attrOffset = header->AttributeOffset;
            while (attrOffset + sizeof(NativeMethods.ATTRIBUTE_HEADER) <= header->RealSize)
            {
                var attr = (NativeMethods.ATTRIBUTE_HEADER*)(bufPtr + attrOffset);
                if (attr->AttributeTypeCode == (uint)NativeMethods.AttributeType.EndOfAttributes || attr->TotalLength == 0)
                    break;

                if (attr->AttributeTypeCode == (uint)NativeMethods.AttributeType.Data && attr->NameLength == 0)
                {
                    if (attr->NonResidentFlag != 0)
                    {
                        var nonRes = (NativeMethods.NON_RESIDENT_ATTRIBUTE*)attr;
                        byte* runPtr = bufPtr + attrOffset + nonRes->RunListOffset;
                        int maxRunLen = (int)(attr->TotalLength - nonRes->RunListOffset);
                        return NtfsDataRunDecoder.DecodeDataRuns(runPtr, maxRunLen);
                    }
                }

                attrOffset += (int)attr->TotalLength;
            }
        }

        return extents;
    }

    private static unsafe MftRawRecord? ParseRecord(byte* recordPtr, ulong defaultRecordNumber, uint bytesPerSector, uint bytesPerRecord)
    {
        var header = (NativeMethods.MFT_RECORD_HEADER*)recordPtr;
        if (header->Magic != 0x454C4946) // "FILE"
            return null;

        // Check InUse flag (0x0001)
        if ((header->Flags & (ushort)NativeMethods.MftRecordFlags.InUse) == 0)
            return null;

        // Apply USA fixup
        if (!NtfsDataRunDecoder.ApplyFixup(recordPtr, bytesPerSector, bytesPerRecord))
            return null;

        bool isDirectory = (header->Flags & (ushort)NativeMethods.MftRecordFlags.IsDirectory) != 0;
        ulong recordNumber = header->RecordNumber != 0 ? header->RecordNumber : defaultRecordNumber;
        ulong baseRecord = header->BaseFileRecord & 0x0000FFFFFFFFFFFF;

        var raw = new MftRawRecord
        {
            RecordNumber = recordNumber,
            IsDirectory = isDirectory
        };

        if (baseRecord != 0)
        {
            raw.ParentRecordNumber = baseRecord; // Link extension record
        }

        int attrOffset = header->AttributeOffset;
        while (attrOffset + sizeof(NativeMethods.ATTRIBUTE_HEADER) <= header->RealSize)
        {
            var attr = (NativeMethods.ATTRIBUTE_HEADER*)(recordPtr + attrOffset);
            if (attr->AttributeTypeCode == (uint)NativeMethods.AttributeType.EndOfAttributes || attr->TotalLength == 0)
                break;

            if (attrOffset + attr->TotalLength > bytesPerRecord)
                break;

            switch ((NativeMethods.AttributeType)attr->AttributeTypeCode)
            {
                case NativeMethods.AttributeType.StandardInformation:
                    if (attr->NonResidentFlag == 0 && attr->TotalLength >= sizeof(NativeMethods.RESIDENT_ATTRIBUTE))
                    {
                        var res = (NativeMethods.RESIDENT_ATTRIBUTE*)attr;
                        if (attrOffset + res->ValueOffset + sizeof(NativeMethods.STANDARD_INFORMATION) <= bytesPerRecord)
                        {
                            var std = (NativeMethods.STANDARD_INFORMATION*)(recordPtr + attrOffset + res->ValueOffset);
                            raw.Attributes = (FileAttributes)std->DosPermissions;
                            if (std->AlteredTime > 0)
                            {
                                try
                                {
                                    raw.LastModified = DateTime.FromFileTimeUtc(std->AlteredTime);
                                }
                                catch { }
                            }
                        }
                    }
                    break;

                case NativeMethods.AttributeType.FileName:
                    if (attr->NonResidentFlag == 0 && attr->TotalLength >= sizeof(NativeMethods.RESIDENT_ATTRIBUTE))
                    {
                        var res = (NativeMethods.RESIDENT_ATTRIBUTE*)attr;
                        if (attrOffset + res->ValueOffset + sizeof(NativeMethods.FILE_NAME_ATTRIBUTE) <= bytesPerRecord)
                        {
                            var fn = (NativeMethods.FILE_NAME_ATTRIBUTE*)(recordPtr + attrOffset + res->ValueOffset);
                            byte ns = fn->Namespace;
                            int nameLen = fn->FileNameLength;

                            if (nameLen > 0 && attrOffset + res->ValueOffset + sizeof(NativeMethods.FILE_NAME_ATTRIBUTE) + (nameLen * 2) <= bytesPerRecord)
                            {
                                char* nameChars = (char*)(recordPtr + attrOffset + res->ValueOffset + sizeof(NativeMethods.FILE_NAME_ATTRIBUTE));
                                string name = new string(nameChars, 0, nameLen);

                                // Select best namespace (Win32 & Win32AndDos > POSIX > DOS 8.3)
                                if (string.IsNullOrEmpty(raw.Name) || ns == (byte)NativeMethods.FileNameNamespace.Win32 || ns == (byte)NativeMethods.FileNameNamespace.Win32AndDos || raw.NamespacePreference == (byte)NativeMethods.FileNameNamespace.Dos)
                                {
                                    raw.Name = name;
                                    raw.ParentRecordNumber = fn->ParentDirectory & 0x0000FFFFFFFFFFFF;
                                    raw.NamespacePreference = ns;

                                    if (raw.Size == 0 && fn->RealSize > 0)
                                        raw.Size = fn->RealSize;
                                    if (raw.AllocatedSize == 0 && fn->AllocatedSize > 0)
                                        raw.AllocatedSize = fn->AllocatedSize;
                                    if (!raw.LastModified.HasValue && fn->AlteredTime > 0)
                                    {
                                        try { raw.LastModified = DateTime.FromFileTimeUtc(fn->AlteredTime); } catch { }
                                    }
                                }
                            }
                        }
                    }
                    break;

                case NativeMethods.AttributeType.Data:
                    if (attr->NameLength == 0) // Default data stream
                    {
                        raw.HasDataStream = true;
                        if (attr->NonResidentFlag == 0)
                        {
                            var res = (NativeMethods.RESIDENT_ATTRIBUTE*)attr;
                            raw.Size = res->ValueLength;
                            raw.AllocatedSize = (res->ValueLength + 511) & ~511; // 512-byte aligned
                        }
                        else
                        {
                            var nonRes = (NativeMethods.NON_RESIDENT_ATTRIBUTE*)attr;
                            raw.Size = nonRes->RealSize;
                            raw.AllocatedSize = nonRes->AllocatedSize;
                        }
                    }
                    break;
            }

            attrOffset += (int)attr->TotalLength;
        }

        if (string.IsNullOrEmpty(raw.Name))
            return null;

        // Skip root self-referencing records like "." or ".."
        if (raw.Name == "." || raw.Name == "..")
            return null;

        return raw;
    }

    private static long PostOrderAggregate(FileSystemItem item)
    {
        if (!item.IsDirectory)
        {
            item.FileCount = 1;
            item.FolderCount = 0;
            return item.Size;
        }

        long totalSize = 0;
        long totalAllocated = 0;
        long totalFiles = 0;
        long totalFolders = 0;

        if (item.HasChildren)
        {
            foreach (var child in item.Children)
            {
                PostOrderAggregate(child);
                totalSize += child.Size;
                totalAllocated += child.AllocatedSize;
                totalFiles += child.FileCount;
                totalFolders += child.FolderCount + (child.IsDirectory ? 1 : 0);
            }
        }

        item.Size = totalSize;
        item.AllocatedSize = totalAllocated;
        item.FileCount = totalFiles;
        item.FolderCount = totalFolders;
        return totalSize;
    }

    private static void AddVirtualVolumeItems(FileSystemItem root, string driveLetter, NativeMethods.NTFS_VOLUME_DATA_BUFFER volumeData)
    {
        try
        {
            long totalDiskBytes = volumeData.TotalClusters * volumeData.BytesPerCluster;
            long freeDiskBytes = volumeData.FreeClusters * volumeData.BytesPerCluster;

            if (totalDiskBytes > 0)
            {
                // Free space item
                var freeItem = new FileSystemItem
                {
                    Name = "[Free Space]",
                    Size = freeDiskBytes,
                    AllocatedSize = freeDiskBytes,
                    IsDirectory = false,
                    IsVirtual = true,
                    Extension = "[Free Space]"
                };
                root.AddChild(freeItem);

                long accountedBytes = root.AllocatedSize + freeDiskBytes;
                long unallocatedSystemBytes = totalDiskBytes - accountedBytes;
                if (unallocatedSystemBytes > 1024 * 1024) // > 1MB
                {
                    var systemItem = new FileSystemItem
                    {
                        Name = "[System / MFT / Reserved Space]",
                        Size = unallocatedSystemBytes,
                        AllocatedSize = unallocatedSystemBytes,
                        IsDirectory = false,
                        IsVirtual = true,
                        Extension = "[System Reserved]"
                    };
                    root.AddChild(systemItem);
                }

                // Adjust root overall capacity
                root.Size = totalDiskBytes;
                root.AllocatedSize = totalDiskBytes;
                root.CalculateChildPercentages(false);
                root.SortChildrenBySizeDescending(false);
            }
        }
        catch
        {
            // Ignore virtual item calculation failure
        }
    }

    private static string GetRelativeSubPath(string root, string target)
    {
        string r = root.TrimEnd('\\');
        string t = target.TrimEnd('\\');
        if (t.Length <= r.Length) return string.Empty;
        if (t.StartsWith(r, StringComparison.OrdinalIgnoreCase))
        {
            return t.Substring(r.Length).TrimStart('\\');
        }
        return string.Empty;
    }

    private static FileSystemItem? FindSubItem(FileSystemItem current, string subPath)
    {
        string[] parts = subPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        FileSystemItem? node = current;

        foreach (var part in parts)
        {
            if (node == null || !node.HasChildren) return null;
            FileSystemItem? next = null;
            foreach (var child in node.Children)
            {
                if (string.Equals(child.Name, part, StringComparison.OrdinalIgnoreCase))
                {
                    next = child;
                    break;
                }
            }
            node = next;
        }

        return node;
    }
}
