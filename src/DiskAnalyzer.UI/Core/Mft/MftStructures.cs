using System;
using System.Collections.Generic;
using System.IO;
using DiskAnalyzer.Core.Native;

namespace DiskAnalyzer.Core.Mft;

/// <summary>
/// Represents a contiguous cluster run (extent) on disk.
/// </summary>
public readonly record struct NtfsExtent(long Lcn, long ClusterCount)
{
    public bool IsSparse => Lcn == 0;
}

/// <summary>
/// Fast internal record structure during raw MFT parsing before tree construction.
/// </summary>
public sealed class MftRawRecord
{
    public ulong RecordNumber { get; set; }
    public ulong ParentRecordNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public long AllocatedSize { get; set; }
    public FileAttributes Attributes { get; set; }
    public DateTime? LastModified { get; set; }
    public bool IsDirectory { get; set; }
    public byte NamespacePreference { get; set; } // 0 = POSIX, 1 = Win32, 2 = DOS, 3 = Win32AndDOS
    public bool HasDataStream { get; set; }
    public List<MftRawRecord>? Hardlinks { get; set; }
}

/// <summary>
/// Helper utilities for decoding NTFS data runs and fixup arrays.
/// </summary>
public static unsafe class NtfsDataRunDecoder
{
    /// <summary>
    /// Decodes an NTFS non-resident attribute data run list into a list of cluster extents.
    /// </summary>
    public static List<NtfsExtent> DecodeDataRuns(byte* runPtr, int maxLength)
    {
        var extents = new List<NtfsExtent>();
        if (runPtr == null || maxLength <= 0)
            return extents;

        long currentLcn = 0;
        int offset = 0;

        while (offset < maxLength)
        {
            byte header = runPtr[offset++];
            if (header == 0) // 0x00 terminates run list
                break;

            int lenBytes = header & 0x0F;
            int offsetBytes = (header >> 4) & 0x0F;

            if (offset + lenBytes + offsetBytes > maxLength)
                break;

            // Read cluster count (unsigned)
            long runLength = 0;
            for (int i = 0; i < lenBytes; i++)
            {
                runLength |= ((long)runPtr[offset++]) << (i * 8);
            }

            // Read LCN offset delta (signed)
            long offsetDelta = 0;
            if (offsetBytes > 0)
            {
                for (int i = 0; i < offsetBytes; i++)
                {
                    offsetDelta |= ((long)runPtr[offset++]) << (i * 8);
                }

                // Sign extend
                int shift = (8 - offsetBytes) * 8;
                offsetDelta = (offsetDelta << shift) >> shift;
                currentLcn += offsetDelta;

                extents.Add(new NtfsExtent(currentLcn, runLength));
            }
            else
            {
                // Sparse run
                extents.Add(new NtfsExtent(0, runLength));
            }
        }

        return extents;
    }

    /// <summary>
    /// Applies the NTFS Update Sequence Array (USA) fixup to restore sector endings.
    /// </summary>
    public static bool ApplyFixup(byte* recordPtr, uint bytesPerSector, uint recordSize)
    {
        if (recordPtr == null || bytesPerSector == 0 || recordSize == 0)
            return false;

        var header = (NativeMethods.MFT_RECORD_HEADER*)recordPtr;
        if (header->Magic != 0x454C4946) // "FILE" in ASCII little-endian
            return false;

        ushort usaOffset = header->UpdateSequenceArrayOffset;
        ushort usaCount = header->UpdateSequenceArraySize;

        if (usaOffset + (usaCount * 2) > recordSize)
            return false;

        ushort* usaArray = (ushort*)(recordPtr + usaOffset);
        ushort usn = usaArray[0];

        int sectors = (int)(recordSize / bytesPerSector);
        if (usaCount - 1 < sectors)
            sectors = usaCount - 1;

        for (int i = 0; i < sectors; i++)
        {
            int sectorEndOffset = (int)((i + 1) * bytesPerSector - 2);
            if (sectorEndOffset + 2 > recordSize)
                break;

            ushort* sectorEnd = (ushort*)(recordPtr + sectorEndOffset);
            if (*sectorEnd != usn)
            {
                // Sector USN does not match; corrupted or partially written sector
                return false;
            }

            *sectorEnd = usaArray[i + 1];
        }

        return true;
    }
}
