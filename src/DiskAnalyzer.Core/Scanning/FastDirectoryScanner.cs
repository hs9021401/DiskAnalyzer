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

        int activeWorkers = 0;
        using var workSignal = new ManualResetEventSlim(true);
        using var finishedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var workerTasks = new Task[maxWorkers];

        for (int i = 0; i < maxWorkers; i++)
        {
            workerTasks[i] = Task.Run(() =>
            {
                while (!finishedCts.IsCancellationRequested)
                {
                    if (workQueue.TryDequeue(out var work))
                    {
                        Interlocked.Increment(ref activeWorkers);
                        try
                        {
                            ScanDirectory(
                                work.ParentItem,
                                work.FullPath,
                                work.Depth,
                                workQueue,
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
                            int remaining = Interlocked.Decrement(ref activeWorkers);
                            if (workQueue.IsEmpty && remaining == 0)
                            {
                                finishedCts.Cancel(); // Signal all workers to stop
                            }
                        }
                    }
                    else
                    {
                        if (activeWorkers == 0 && workQueue.IsEmpty)
                        {
                            finishedCts.Cancel();
                            break;
                        }
                        // Short wait before checking queue again
                        Thread.SpinWait(100);
                    }
                }
            }, cancellationToken);
        }

        // Progress reporting loop
        while (!finishedCts.IsCancellationRequested)
        {
            if (sw.ElapsedMilliseconds % 150 < 25)
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
            }
            Thread.Sleep(50);
        }

        try
        {
            Task.WaitAll(workerTasks, TimeSpan.FromSeconds(5));
        }
        catch { }

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

        PostOrderAggregate(rootItem);

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
        ConcurrentQueue<(FileSystemItem ParentItem, string FullPath, int Depth)> queue,
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

        var localChildren = new List<FileSystemItem>();

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

                localChildren.Add(item);

                if (isDir)
                {
                    Interlocked.Increment(ref foldersScanned);
                    if (!isReparse || options.FollowReparsePoints)
                    {
                        string childFullPath = Path.Combine(dirPath, fileName);
                        queue.Enqueue((item, childFullPath, currentDepth + 1));
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

        lock (parentItem)
        {
            parentItem.AddChildren(localChildren);
        }
    }

    private static string MakeExtendedPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        if (path.StartsWith(@"\\?\") || path.StartsWith(@"\\.\")) return path;
        if (path.StartsWith(@"\\")) return @"\\?\UNC\" + path.Substring(2);
        return @"\\?\" + path;
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
