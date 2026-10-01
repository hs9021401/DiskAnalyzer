using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
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
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new ScanOptions();
        var sw = Stopwatch.StartNew();

        string driveLetter = Path.GetPathRoot(drivePath)?.TrimEnd('\\') ?? "C:";
        if (!string.Equals(Path.GetFullPath(drivePath).TrimEnd('\\'), driveLetter, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("USN enumeration requires a drive root.", nameof(drivePath));
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
        long nextProgressReportMilliseconds = 0;

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

                if (!success)
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err == NativeMethods.ERROR_HANDLE_EOF)
                    {
                        break;
                    }
                    throw new InvalidOperationException($"FSCTL_ENUM_USN_DATA failed. Error code: {err}");
                }
                if (returnedBytes <= sizeof(ulong)) break;
                if (returnedBytes > buffer.Length)
                    throw new InvalidDataException("USN output exceeds the supplied buffer.");

                // First 8 bytes of output buffer is the next StartFileReferenceNumber
                ulong nextFrn = *(ulong*)bufPtr;
                enumData.StartFileReferenceNumber = nextFrn;

                int offset = sizeof(ulong);
                while (offset < returnedBytes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var item = ParseRecord(buffer.AsSpan(offset, (int)returnedBytes - offset), out int recordLength);
                    if (item.Name != "." && item.Name != "..")
                    {
                        itemMap[item.FileRecordNumber] = item;
                        if (item.IsDirectory) foldersCount++;
                        else filesCount++;
                    }
                    offset += recordLength;
                }

                long elapsedMilliseconds = sw.ElapsedMilliseconds;
                if (elapsedMilliseconds >= nextProgressReportMilliseconds)
                {
                    progress?.Report(new ScanProgress
                    {
                        Phase = ScanPhase.ReadingUsnJournal,
                        FilesScanned = filesCount,
                        FoldersScanned = foldersCount,
                        ElapsedTime = sw.Elapsed
                    });
                    nextProgressReportMilliseconds = elapsedMilliseconds + 200;
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

        rootItem.RootPath = driveLetter.EndsWith('\\') ? driveLetter : (driveLetter + "\\");

        foreach (var kvp in itemMap)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

        // USN records contain names and attributes, but no file sizes. Resolve metadata
        // before aggregation; failures propagate to the directory-walker fallback.
        foreach (var item in itemMap.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.IsDirectory) continue;
            var info = new FileInfo(item.GetFullPath());
            item.Size = info.Length;
            item.AllocatedSize = (item.Size + 4095) & ~4095L; // Same estimate as FastWalker.
        }

        rootItem.RecalculateAggregateStatistics();

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

    internal static FileSystemItem ParseRecord(ReadOnlySpan<byte> data, out int recordLength)
    {
        const int headerLength = 60;
        if (data.Length < headerLength)
            throw new InvalidDataException("Truncated USN record header.");
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (length < headerLength || length > data.Length)
            throw new InvalidDataException("Invalid USN record length.");
        if (BinaryPrimitives.ReadUInt16LittleEndian(data[4..]) != 2)
            throw new NotSupportedException("Only USN V2 records are supported.");
        int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(data[56..]);
        int nameOffset = BinaryPrimitives.ReadUInt16LittleEndian(data[58..]);
        if (nameLength == 0 || (nameLength & 1) != 0 || nameOffset < headerLength
            || (nameOffset & 1) != 0 || nameOffset + nameLength > length)
            throw new InvalidDataException("Invalid USN filename bounds.");

        recordLength = (int)length;
        string name = Encoding.Unicode.GetString(data.Slice(nameOffset, nameLength));
        var attributes = (FileAttributes)BinaryPrimitives.ReadUInt32LittleEndian(data[52..]);
        bool isDirectory = (attributes & FileAttributes.Directory) != 0;
        var item = new FileSystemItem
        {
            Name = name,
            FileRecordNumber = BinaryPrimitives.ReadUInt64LittleEndian(data[8..]) & 0x0000FFFFFFFFFFFF,
            ParentRecordNumber = BinaryPrimitives.ReadUInt64LittleEndian(data[16..]) & 0x0000FFFFFFFFFFFF,
            Attributes = attributes,
            IsDirectory = isDirectory,
            Extension = isDirectory ? string.Empty : Path.GetExtension(name)
        };
        long timestamp = BinaryPrimitives.ReadInt64LittleEndian(data[32..]);
        if (timestamp > 0)
        {
            try { item.LastModified = DateTime.FromFileTimeUtc(timestamp); }
            catch (ArgumentOutOfRangeException) { }
        }
        return item;
    }

}
