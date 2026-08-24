using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using DiskAnalyzer.Core.Models;
using DiskAnalyzer.Core.Native;
using Microsoft.Win32.SafeHandles;

namespace DiskAnalyzer.Core.Mft;

/// <summary>
/// Fast NTFS USN (Update Sequence Number) Change Journal reader using FSCTL_ENUM_USN_DATA.
/// </summary>
public class NtfsUsnReader
{
    private const ulong MFT_RECORD_ROOT = 5;

    public unsafe FileSystemItem ReadDrive(string drivePath, ScanOptions? options = null, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        options ??= new ScanOptions();
        var sw = Stopwatch.StartNew();

        string driveLetter = Path.GetPathRoot(drivePath)?.TrimEnd('\\') ?? "C:";
        string volumePath = $@"\\.\{driveLetter}";

        PrivilegeManager.EnableBackupPrivileges();

        progress?.Report(new ScanProgress
        {
            Phase = ScanPhase.Initializing,
            CurrentFolder = drivePath,
            ElapsedTime = sw.Elapsed
        });

        using SafeFileHandle volumeHandle = NativeMethods.CreateFileW(
            volumePath,
            NativeMethods.GENERIC_READ,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            NativeMethods.FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero);

        if (volumeHandle.IsInvalid)
        {
            int err = Marshal.GetLastWin32Error();
            throw new UnauthorizedAccessException($"Failed to open volume '{volumePath}' for USN Journal reading. Error code: {err}");
        }

        // Query USN Journal Data
        var usnJournalData = new NativeMethods.USN_JOURNAL_DATA();
        uint returnedBytes = 0;
        bool hasJournal = NativeMethods.DeviceIoControl(
            volumeHandle,
            NativeMethods.FSCTL_QUERY_USN_JOURNAL,
            null,
            0,
            &usnJournalData,
            (uint)sizeof(NativeMethods.USN_JOURNAL_DATA),
            out returnedBytes,
            IntPtr.Zero);

        var enumData = new NativeMethods.MFT_ENUM_DATA_V0
        {
            StartFileReferenceNumber = 0,
            LowUsn = 0,
            HighUsn = hasJournal ? usnJournalData.HighestUsn : long.MaxValue
        };

        int bufferSize = Math.Max(options.BufferSize, 2 * 1024 * 1024); // 2MB
        byte[] buffer = GC.AllocateUninitializedArray<byte>(bufferSize, pinned: true);

        var itemMap = new Dictionary<ulong, FileSystemItem>(100_000);
        long filesCount = 0;
        long foldersCount = 0;

        progress?.Report(new ScanProgress
        {
            Phase = ScanPhase.ReadingUsnJournal,
            CurrentFolder = drivePath,
            ElapsedTime = sw.Elapsed
        });

        fixed (byte* bufPtr = buffer)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool success = NativeMethods.DeviceIoControl(
                    volumeHandle,
                    NativeMethods.FSCTL_ENUM_USN_DATA,
                    &enumData,
                    (uint)sizeof(NativeMethods.MFT_ENUM_DATA_V0),
                    bufPtr,
                    (uint)bufferSize,
                    out returnedBytes,
                    IntPtr.Zero);

                if (!success || returnedBytes <= sizeof(ulong))
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err == NativeMethods.ERROR_HANDLE_EOF || err == NativeMethods.ERROR_SUCCESS || returnedBytes <= sizeof(ulong))
                    {
                        break;
                    }
                    throw new InvalidOperationException($"FSCTL_ENUM_USN_DATA failed. Error code: {err}");
                }

                // First 8 bytes of output buffer is the next StartFileReferenceNumber
                ulong nextFrn = *(ulong*)bufPtr;
                enumData.StartFileReferenceNumber = nextFrn;

                int offset = sizeof(ulong);
                while (offset < returnedBytes)
                {
                    var record = (NativeMethods.USN_RECORD_V2*)(bufPtr + offset);
                    if (record->RecordLength == 0)
                        break;

                    ulong frn = record->FileReferenceNumber & 0x0000FFFFFFFFFFFF;
                    ulong parentFrn = record->ParentFileReferenceNumber & 0x0000FFFFFFFFFFFF;
                    bool isDirectory = (record->FileAttributes & NativeMethods.FILE_ATTRIBUTE_DIRECTORY) != 0;

                    if (record->FileNameLength > 0 && offset + record->FileNameOffset + record->FileNameLength <= returnedBytes)
                    {
                        char* nameChars = (char*)(bufPtr + offset + record->FileNameOffset);
                        int charCount = record->FileNameLength / sizeof(char);
                        string name = new string(nameChars, 0, charCount);

                        if (name != "." && name != "..")
                        {
                            var item = new FileSystemItem
                            {
                                Name = name,
                                FileRecordNumber = frn,
                                ParentRecordNumber = parentFrn,
                                Attributes = (FileAttributes)record->FileAttributes,
                                IsDirectory = isDirectory,
                                Extension = isDirectory ? string.Empty : Path.GetExtension(name)
                            };

                            if (record->TimeStamp > 0)
                            {
                                try { item.LastModified = DateTime.FromFileTimeUtc(record->TimeStamp); } catch { }
                            }

                            itemMap[frn] = item;

                            if (isDirectory)
                                foldersCount++;
                            else
                                filesCount++;
                        }
                    }

                    offset += (int)record->RecordLength;
                }

                if (sw.ElapsedMilliseconds % 200 < 20)
                {
                    progress?.Report(new ScanProgress
                    {
                        Phase = ScanPhase.ReadingUsnJournal,
                        FilesScanned = filesCount,
                        FoldersScanned = foldersCount,
                        ElapsedTime = sw.Elapsed
                    });
                }
            }
        }

        // Build Hierarchy
        progress?.Report(new ScanProgress
        {
            Phase = ScanPhase.BuildingTree,
            FilesScanned = filesCount,
            FoldersScanned = foldersCount,
            ElapsedTime = sw.Elapsed
        });

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
                rootItem.AddChild(item);
            }
        }

        // Aggregation & Sorting
        progress?.Report(new ScanProgress
        {
            Phase = ScanPhase.CalculatingSizes,
            FilesScanned = filesCount,
            FoldersScanned = foldersCount,
            ElapsedTime = sw.Elapsed
        });

        PostOrderAggregate(rootItem);

        progress?.Report(new ScanProgress
        {
            Phase = ScanPhase.Sorting,
            FilesScanned = filesCount,
            FoldersScanned = foldersCount,
            ElapsedTime = sw.Elapsed
        });

        rootItem.CalculateChildPercentages(true);
        rootItem.SortChildrenBySizeDescending(true);

        progress?.Report(new ScanProgress
        {
            Phase = ScanPhase.Complete,
            FilesScanned = rootItem.FileCount,
            FoldersScanned = rootItem.FolderCount,
            TotalBytes = rootItem.Size,
            ElapsedTime = sw.Elapsed
        });

        return rootItem;
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
}
