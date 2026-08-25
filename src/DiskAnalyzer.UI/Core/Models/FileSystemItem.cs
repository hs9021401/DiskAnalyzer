using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace DiskAnalyzer.Core.Models;

/// <summary>
/// High-efficiency tree node representing a file or directory in the scanned hierarchy.
/// Implements INotifyPropertyChanged for real-time visual updates.
/// </summary>
public class FileSystemItem : INotifyPropertyChanged
{
    private string? _cachedFullPath;
    private ObservableCollection<FileSystemItem>? _children;
    private string _name = string.Empty;
    private string? _rootPath;
    private FileSystemItem? _parent;
    private long _size;
    private long _allocatedSize;
    private long _fileCount;
    private long _folderCount;
    private double _percentage;
    private bool _isExpanded;
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set
        {
            if (SetField(ref _name, value))
            {
                InvalidateFullPathCache();
            }
        }
    }

    /// <summary>
    /// Absolute path represented by the root node. The display name remains in <see cref="Name"/>.
    /// </summary>
    public string? RootPath
    {
        get => _rootPath;
        set
        {
            if (SetField(ref _rootPath, value))
            {
                InvalidateFullPathCache();
            }
        }
    }

    public long Size
    {
        get => _size;
        set
        {
            if (SetField(ref _size, value))
            {
                OnPropertyChanged(nameof(SizeFormatted));
            }
        }
    }

    public long AllocatedSize
    {
        get => _allocatedSize;
        set
        {
            if (SetField(ref _allocatedSize, value))
            {
                OnPropertyChanged(nameof(AllocatedSizeFormatted));
            }
        }
    }

    public long FileCount
    {
        get => _fileCount;
        set
        {
            if (SetField(ref _fileCount, value))
            {
                OnPropertyChanged(nameof(FileCountFormatted));
            }
        }
    }

    public long FolderCount
    {
        get => _folderCount;
        set
        {
            if (SetField(ref _folderCount, value))
            {
                OnPropertyChanged(nameof(FolderCountFormatted));
            }
        }
    }

    public double Percentage
    {
        get => _percentage;
        set
        {
            if (SetField(ref _percentage, value))
            {
                OnPropertyChanged(nameof(PercentageFormatted));
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public ulong FileRecordNumber { get; set; }
    public ulong ParentRecordNumber { get; set; }
    public FileAttributes Attributes { get; set; }
    public DateTime? LastModified { get; set; }
    public bool IsDirectory { get; set; }
    public FileSystemItem? Parent
    {
        get => _parent;
        set
        {
            if (SetField(ref _parent, value))
            {
                InvalidateFullPathCache();
            }
        }
    }
    public string Extension { get; set; } = string.Empty;
    public bool IsVirtual { get; set; }

    public ObservableCollection<FileSystemItem> Children
    {
        get => _children ??= new ObservableCollection<FileSystemItem>();
        set
        {
            if (SetField(ref _children, value))
            {
                OnPropertyChanged(nameof(HasChildren));
                OnPropertyChanged(nameof(ChildCount));
            }
        }
    }

    public bool HasChildren => _children != null && _children.Count > 0;

    public int ChildCount => _children?.Count ?? 0;

    public string SizeFormatted => FormatBytes(Size);
    public string AllocatedSizeFormatted => FormatBytes(AllocatedSize);
    public string FileCountFormatted => FileCount.ToString("N0");
    public string FolderCountFormatted => FolderCount.ToString("N0");
    public string PercentageFormatted => $"{Percentage:F1}%";

    public void AddChild(FileSystemItem child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public void AddChildren(IEnumerable<FileSystemItem> children)
    {
        foreach (var child in children)
        {
            AddChild(child);
        }
    }

    /// <summary>
    /// Computes the full path from the root node down to this node.
    /// </summary>
    public string GetFullPath()
    {
        if (_cachedFullPath != null)
            return _cachedFullPath;

        if (Parent == null)
        {
            _cachedFullPath = !string.IsNullOrWhiteSpace(RootPath) ? RootPath! : Name;
            return _cachedFullPath;
        }

        // Collect ancestors
        var stack = new Stack<string>();
        var current = this;
        while (current?.Parent != null)
        {
            if (!string.IsNullOrEmpty(current.Name))
            {
                stack.Push(current.Name);
            }
            current = current.Parent;
        }

        if (current != null)
        {
            stack.Push(!string.IsNullOrWhiteSpace(current.RootPath) ? current.RootPath! : current.Name);
        }

        var sb = new StringBuilder(260);
        bool first = true;
        while (stack.Count > 0)
        {
            string part = stack.Pop();
            if (first)
            {
                sb.Append(part);
                if (part.EndsWith('\\') || part.EndsWith('/'))
                {
                    // Drive root like "C:\"
                }
                else if (part.Length == 2 && part[1] == ':')
                {
                    sb.Append('\\');
                }
                first = false;
            }
            else
            {
                if (sb.Length > 0 && sb[^1] != '\\' && sb[^1] != '/')
                {
                    sb.Append('\\');
                }
                sb.Append(part);
            }
        }

        _cachedFullPath = sb.ToString();
        return _cachedFullPath;
    }

    private void InvalidateFullPathCache()
    {
        _cachedFullPath = null;

        if (_children == null)
            return;

        foreach (var child in _children)
        {
            child.InvalidateFullPathCache();
        }
    }

    /// <summary>
    /// Sorts children recursively or at current level by Size descending.
    /// </summary>
    public void SortChildrenBySizeDescending(bool recursive = false)
    {
        if (_children == null || _children.Count <= 1)
            return;

        var sorted = _children.OrderByDescending(c => c.Size).ToList();
        _children.Clear();
        foreach (var item in sorted)
        {
            _children.Add(item);
        }

        if (recursive)
        {
            foreach (var child in _children)
            {
                if (child.IsDirectory)
                {
                    child.SortChildrenBySizeDescending(true);
                }
            }
        }
    }

    /// <summary>
    /// Calculates percentages for immediate children relative to this item's size.
    /// </summary>
    public void CalculateChildPercentages(bool recursive = false)
    {
        if (_children == null || _children.Count == 0)
            return;

        double total = Size > 0 ? Size : 1;
        foreach (var child in _children)
        {
            child.Percentage = Math.Clamp((double)child.Size / total * 100.0, 0.0, 100.0);
            if (recursive && child.IsDirectory)
            {
                child.CalculateChildPercentages(true);
            }
        }
    }

    /// <summary>
    /// Fast and human-readable byte size formatter (B, KB, MB, GB, TB, PB).
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        if (bytes < 0)
            return "-" + FormatBytes(-bytes);

        if (bytes < 1024)
            return $"{bytes} B";

        double len = bytes;
        int order = 0;
        string[] sizes = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024.0;
        }

        return order switch
        {
            1 => $"{len:F1} KB",
            2 => $"{len:F2} MB",
            3 => $"{len:F2} GB",
            4 => $"{len:F2} TB",
            _ => $"{len:F2} {sizes[order]}"
        };
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() => $"{Name} ({SizeFormatted})";
}
