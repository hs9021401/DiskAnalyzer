using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DiskAnalyzer.Core.Models;
using DiskAnalyzer.Core.Native;

namespace DiskAnalyzer.Core.Scanning;

/// <summary>
/// Ultra-fast multi-threaded directory scanner utilizing Win32 FindFirstFileExW and FindNextFileW
/// with large fetch buffers and work-stealing parallel queue.
/// </summary>
public class FastDirectoryScanner
{
    private const int FIND_FIRST_EX_LARGE_FETCH = 0x00000002;

    public FileSystemItem Scan(string rootPath, ScanOptions? options = null, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new ScanOptions();
        var sw = Stopwatch.StartNew();

        string normalizedPath = Path.GetFullPath(rootPath).TrimEnd('\\');
        if (normalizedPath.EndsWith(':'))
        {
            normalizedPath += "\\";
        }

        string rootName = normalizedPath;
        if (rootName.Length > 3)
        {
            rootName = Path.GetFileName(normalizedPath);
            if (string.IsNullOrEmpty(rootName)) rootName = normalizedPath;
        }

        var rootItem = new FileSystemItem
        {
            Name = rootName,
            RootPath = normalizedPath,
            IsDirectory = true,
            Attributes = FileAttributes.Directory
        };

        progress?.Report(new ScanProgress
        {
            Phase = ScanPhase.ScanningDirectories,
            CurrentFolder = normalizedPath,
            ElapsedTime = sw.Elapsed
        });

        long filesScanned = 0;
        long foldersScanned = 0;
        long totalBytes = 0;

        int maxWorkers = Math.Max(2, options.MaxDegreeOfParallelism);
        var workQueue = new ConcurrentQueue<(FileSystemItem ParentItem, string FullPath, int Depth)>();
        workQueue.Enqueue((rootItem, normalizedPath, 0));

        // Count queued and in-progress folders together. A worker must register discovered
        // folders before it completes its own item, so the count cannot momentarily reach
        // zero while another worker is about to scan a subtree.
        long pendingWorkItems = 1;
        Action<FileSystemItem, string, int> enqueueWork = (parentItem, fullPath, depth) =>
        {
            Interlocked.Increment(ref pendingWorkItems);
            workQueue.Enqueue((parentItem, fullPath, depth));
        };

        var workerTasks = new Task[maxWorkers];

        for (int i = 0; i < maxWorkers; i++)
        {
            workerTasks[i] = Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (workQueue.TryDequeue(out var work))
                    {
                        try
                        {
                            ScanDirectory(
                                work.ParentItem,
                                work.FullPath,
                                work.Depth,
                                enqueueWork,
                                options,
                                ref filesScanned,
                                ref foldersScanned,
                                ref totalBytes,
                                cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch
                        {
                            // Ignore folder access errors
                        }
                        finally
                        {
                            Interlocked.Decrement(ref pendingWorkItems);
                        }
                    }
                    else
                    {
                        if (Volatile.Read(ref pendingWorkItems) == 0)
                        {
                            break;
                        }
                        // Short wait before checking queue again
                        Thread.Sleep(1);
                    }
                }
            });
        }

        // Progress reporting loop
        long nextProgressReportMilliseconds = 0;
        while (!cancellationToken.IsCancellationRequested && Volatile.Read(ref pendingWorkItems) > 0)
        {
            long elapsedMilliseconds = sw.ElapsedMilliseconds;
            if (elapsedMilliseconds >= nextProgressReportMilliseconds)
            {
                progress?.Report(new ScanProgress
                {
                    Phase = ScanPhase.ScanningDirectories,
                    FilesScanned = Interlocked.Read(ref filesScanned),
                    FoldersScanned = Interlocked.Read(ref foldersScanned),
                    TotalBytes = Interlocked.Read(ref totalBytes),
                    ElapsedTime = sw.Elapsed,
                    CurrentFolder = normalizedPath
                });
                nextProgressReportMilliseconds = elapsedMilliseconds + 150;
            }
            Thread.Sleep(50);
        }

        Task.WaitAll(workerTasks);

        cancellationToken.ThrowIfCancellationRequested();

        // Step 2: Post-order aggregate sizes
        progress?.Report(new ScanProgress
        {
            Phase = ScanPhase.CalculatingSizes,
            FilesScanned = Interlocked.Read(ref filesScanned),
            FoldersScanned = Interlocked.Read(ref foldersScanned),
            TotalBytes = Interlocked.Read(ref totalBytes),
            ElapsedTime = sw.Elapsed
        });

        rootItem.RecalculateAggregateStatistics();
        cancellationToken.ThrowIfCancellationRequested();

        // Step 3: Sort children and compute percentages
        progress?.Report(new ScanProgress
        {
            Phase = ScanPhase.Sorting,
            FilesScanned = Interlocked.Read(ref filesScanned),
            FoldersScanned = Interlocked.Read(ref foldersScanned),
            TotalBytes = Interlocked.Read(ref totalBytes),
            ElapsedTime = sw.Elapsed
        });

        rootItem.CalculateChildPercentages(true);
        rootItem.SortChildrenBySizeDescending(true);
        cancellationToken.ThrowIfCancellationRequested();

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

    private static unsafe void ScanDirectory(
        FileSystemItem parentItem,
        string dirPath,
        int currentDepth,
        Action<FileSystemItem, string, int> enqueueWork,
        ScanOptions options,
        ref long filesScanned,
        ref long foldersScanned,
        ref long totalBytes,
        CancellationToken ct)
    {
        if (options.MaxDepth.HasValue && currentDepth >= options.MaxDepth.Value)
            return;

        ct.ThrowIfCancellationRequested();

        string searchPattern = MakeExtendedPath(dirPath) + @"\*";
        var findData = new NativeMethods.WIN32_FIND_DATAW();

        IntPtr hFind = NativeMethods.FindFirstFileExW(
            searchPattern,
            NativeMethods.FINDEX_INFO_LEVELS.FindExInfoBasic,
            out findData,
            NativeMethods.FINDEX_SEARCH_OPS.FindExSearchNameMatch,
            IntPtr.Zero,
            FIND_FIRST_EX_LARGE_FETCH);

        if (hFind == IntPtr.Zero || hFind == (IntPtr)(-1))
            return;

        try
        {
            do
            {
                ct.ThrowIfCancellationRequested();

                string fileName = new string(findData.cFileName);

                if (fileName == "." || fileName == "..")
                    continue;

                uint attrs = findData.dwFileAttributes;
                bool isDir = (attrs & NativeMethods.FILE_ATTRIBUTE_DIRECTORY) != 0;
                bool isReparse = (attrs & NativeMethods.FILE_ATTRIBUTE_REPARSE_POINT) != 0;

                long size = 0;
                long allocatedSize = 0;

                if (!isDir)
                {
                    size = ((long)findData.nFileSizeHigh << 32) | findData.nFileSizeLow;
                    allocatedSize = (size + 4095) & ~4095; // Align to 4KB cluster
                }

                var item = new FileSystemItem
                {
                    Name = fileName,
                    Size = size,
                    AllocatedSize = allocatedSize,
                    Attributes = (FileAttributes)attrs,
                    LastModified = findData.ftLastWriteTime.ToDateTimeUtc(),
                    IsDirectory = isDir,
                    Extension = isDir ? string.Empty : Path.GetExtension(fileName)
                };

                // Link before publishing work. Parent assignment invalidates descendant path
                // caches and must not race another worker populating this item's children.
                parentItem.AddChild(item);

                if (isDir)
                {
                    Interlocked.Increment(ref foldersScanned);
                    if (!isReparse || options.FollowReparsePoints)
                    {
                        string childFullPath = Path.Combine(dirPath, fileName);
                        enqueueWork(item, childFullPath, currentDepth + 1);
                    }
                }
                else
                {
                    Interlocked.Increment(ref filesScanned);
                    Interlocked.Add(ref totalBytes, size);
                }

            } while (NativeMethods.FindNextFileW(hFind, out findData));
        }
        finally
        {
            NativeMethods.FindClose(hFind);
        }

    }

    private static string MakeExtendedPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        if (path.StartsWith(@"\\?\") || path.StartsWith(@"\\.\")) return path;
        if (path.StartsWith(@"\\")) return @"\\?\UNC\" + path.Substring(2);
        return @"\\?\" + path;
    }

}
