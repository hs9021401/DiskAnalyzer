using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DiskAnalyzer.Core.Models;

namespace DiskAnalyzer.Core.Export;

/// <summary>
/// High-speed CSV exporter for disk analysis reports:
/// FileName,Size,Allocated,Modified,Attributes,Files,Folders
/// </summary>
public static class CsvExporter
{
    public const string CSV_HEADER = "FileName,Size,Allocated,Modified,Attributes,Files,Folders";

    /// <summary>
    /// Exports the full tree rooted at rootItem to a CSV file.
    /// </summary>
    public static async Task ExportTreeToCsvAsync(
        FileSystemItem rootItem,
        string filePath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        await using var writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
        {
            NewLine = "\r\n"
        };

        await writer.WriteLineAsync(CSV_HEADER);

        var stack = new Stack<FileSystemItem>();
        stack.Push(rootItem);

        int exportedCount = 0;
        var sb = new StringBuilder(512);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = stack.Pop();
            if (!item.IsVirtual)
            {
                FormatCsvLine(sb, item);
                await writer.WriteLineAsync(sb.ToString());
                exportedCount++;

                if (exportedCount % 5000 == 0)
                {
                    progress?.Report(exportedCount);
                }
            }

            if (item.HasChildren)
            {
                // Push in reverse order so children are processed in order
                for (int i = item.Children.Count - 1; i >= 0; i--)
                {
                    stack.Push(item.Children[i]);
                }
            }
        }

        await writer.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Exports a flat list of file system items to a CSV file.
    /// </summary>
    public static async Task ExportListToCsvAsync(
        IEnumerable<FileSystemItem> items,
        string filePath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        await using var writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
        {
            NewLine = "\r\n"
        };

        await writer.WriteLineAsync(CSV_HEADER);

        int exportedCount = 0;
        var sb = new StringBuilder(512);

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!item.IsVirtual)
            {
                FormatCsvLine(sb, item);
                await writer.WriteLineAsync(sb.ToString());
                exportedCount++;

                if (exportedCount % 5000 == 0)
                {
                    progress?.Report(exportedCount);
                }
            }
        }

        await writer.FlushAsync(cancellationToken);
    }

    private static void FormatCsvLine(StringBuilder sb, FileSystemItem item)
    {
        sb.Clear();

        // 1. File Name (escaped & ensure trailing backslash for directories)
        string fullPath = item.GetFullPath();
        if (item.IsDirectory && !fullPath.EndsWith('\\') && !fullPath.EndsWith('/'))
        {
            fullPath += "\\";
        }

        fullPath = SanitizeString(fullPath);

        sb.Append('\"');
        sb.Append(fullPath.Replace("\"", "\"\""));
        sb.Append("\",");

        // 2. Size (integer bytes)
        sb.Append(item.Size.ToString(CultureInfo.InvariantCulture));
        sb.Append(',');

        // 3. Allocated (integer bytes)
        sb.Append(item.AllocatedSize.ToString(CultureInfo.InvariantCulture));
        sb.Append(',');

        // 4. Modified (quoted timestamp)
        sb.Append('\"');
        if (item.LastModified.HasValue)
        {
            sb.Append(item.LastModified.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }
        sb.Append("\",");

        // 5. Attributes (integer bitmask for standard parser compatibility)
        sb.Append(((int)item.Attributes).ToString(CultureInfo.InvariantCulture));
        sb.Append(',');

        // 6. Files
        sb.Append(item.FileCount.ToString(CultureInfo.InvariantCulture));
        sb.Append(',');

        // 7. Folders
        sb.Append(item.FolderCount.ToString(CultureInfo.InvariantCulture));
    }

    private static string SanitizeString(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            if (c >= 32 || c == '\t')
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Formats FileAttributes into standard single-character flags (D, R, H, S, A, C, E, L).
    /// </summary>
    public static string FormatAttributes(FileAttributes attrs, bool isDirectory)
    {
        var sb = new StringBuilder(8);

        if (isDirectory || (attrs & FileAttributes.Directory) != 0) sb.Append('D');
        if ((attrs & FileAttributes.ReadOnly) != 0) sb.Append('R');
        if ((attrs & FileAttributes.Hidden) != 0) sb.Append('H');
        if ((attrs & FileAttributes.System) != 0) sb.Append('S');
        if ((attrs & FileAttributes.Archive) != 0) sb.Append('A');
        if ((attrs & FileAttributes.Compressed) != 0) sb.Append('C');
        if ((attrs & FileAttributes.Encrypted) != 0) sb.Append('E');
        if ((attrs & FileAttributes.ReparsePoint) != 0) sb.Append('L');

        return sb.ToString();
    }
}
