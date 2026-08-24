using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using DiskAnalyzer.Core.Models;

namespace DiskAnalyzer.Core.Search;

public enum SearchItemType
{
    All,
    FilesOnly,
    FoldersOnly
}

/// <summary>
/// Criteria for filtering file system items.
/// </summary>
public class SearchCriteria
{
    public string? Query { get; set; }
    public bool UseRegex { get; set; }
    public bool MatchCase { get; set; }
    public string? Extension { get; set; }
    public long? MinSize { get; set; }
    public long? MaxSize { get; set; }
    public DateTime? ModifiedAfter { get; set; }
    public DateTime? ModifiedBefore { get; set; }
    public SearchItemType ItemType { get; set; } = SearchItemType.All;
    public FileAttributes? RequiredAttributes { get; set; }
    public FileAttributes? ExcludedAttributes { get; set; }

    /// <summary>
    /// Parses a size constraint string like ">1GB", "<500MB", "10MB..50MB", ">=100KB".
    /// </summary>
    public static (long? MinSize, long? MaxSize) ParseSizeConstraint(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (null, null);

        string s = input.Trim().ToUpperInvariant();

        if (s.Contains(".."))
        {
            string[] parts = s.Split([".."], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                long? min = ParseSingleSize(parts[0]);
                long? max = ParseSingleSize(parts[1]);
                return (min, max);
            }
        }

        if (s.StartsWith(">="))
            return (ParseSingleSize(s[2..]), null);
        if (s.StartsWith(">"))
            return (ParseSingleSize(s[1..]), null);
        if (s.StartsWith("<="))
            return (null, ParseSingleSize(s[2..]));
        if (s.StartsWith("<"))
            return (null, ParseSingleSize(s[1..]));

        long? exact = ParseSingleSize(s);
        return (exact, null);
    }

    private static long? ParseSingleSize(string str)
    {
        str = str.Trim();
        if (string.IsNullOrEmpty(str)) return null;

        double multiplier = 1;
        string numPart = str;

        if (str.EndsWith("TB") || str.EndsWith("T"))
        {
            multiplier = 1024L * 1024L * 1024L * 1024L;
            numPart = str.TrimEnd('T', 'B', ' ');
        }
        else if (str.EndsWith("GB") || str.EndsWith("G"))
        {
            multiplier = 1024L * 1024L * 1024L;
            numPart = str.TrimEnd('G', 'B', ' ');
        }
        else if (str.EndsWith("MB") || str.EndsWith("M"))
        {
            multiplier = 1024L * 1024L;
            numPart = str.TrimEnd('M', 'B', ' ');
        }
        else if (str.EndsWith("KB") || str.EndsWith("K"))
        {
            multiplier = 1024L;
            numPart = str.TrimEnd('K', 'B', ' ');
        }
        else if (str.EndsWith("B"))
        {
            multiplier = 1;
            numPart = str.TrimEnd('B', ' ');
        }

        if (double.TryParse(numPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
        {
            return (long)(val * multiplier);
        }

        return null;
    }
}

/// <summary>
/// High-performance search and filtering engine for file items.
/// </summary>
public static class FileSearchEngine
{
    /// <summary>
    /// Searches flat list of file system items matching criteria.
    /// </summary>
    public static List<FileSystemItem> Search(IEnumerable<FileSystemItem> items, SearchCriteria criteria)
    {
        var result = new List<FileSystemItem>();
        Regex? regex = null;
        string? queryPattern = null;

        if (!string.IsNullOrWhiteSpace(criteria.Query))
        {
            if (criteria.UseRegex)
            {
                var options = criteria.MatchCase ? RegexOptions.Compiled : RegexOptions.Compiled | RegexOptions.IgnoreCase;
                regex = new Regex(criteria.Query, options);
            }
            else
            {
                queryPattern = criteria.Query;
            }
        }

        string? targetExt = criteria.Extension?.TrimStart('.').ToLowerInvariant();

        foreach (var item in items)
        {
            if (item.IsVirtual)
                continue;

            // Item Type filter
            if (criteria.ItemType == SearchItemType.FilesOnly && item.IsDirectory)
                continue;
            if (criteria.ItemType == SearchItemType.FoldersOnly && !item.IsDirectory)
                continue;

            // Size filter
            if (criteria.MinSize.HasValue && item.Size < criteria.MinSize.Value)
                continue;
            if (criteria.MaxSize.HasValue && item.Size > criteria.MaxSize.Value)
                continue;

            // Date Modified filter
            if (criteria.ModifiedAfter.HasValue && (!item.LastModified.HasValue || item.LastModified.Value < criteria.ModifiedAfter.Value))
                continue;
            if (criteria.ModifiedBefore.HasValue && (!item.LastModified.HasValue || item.LastModified.Value > criteria.ModifiedBefore.Value))
                continue;

            // Extension filter
            if (!string.IsNullOrEmpty(targetExt))
            {
                string ext = item.Extension.TrimStart('.').ToLowerInvariant();
                if (!string.Equals(ext, targetExt, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            // Attributes filter
            if (criteria.RequiredAttributes.HasValue && (item.Attributes & criteria.RequiredAttributes.Value) != criteria.RequiredAttributes.Value)
                continue;
            if (criteria.ExcludedAttributes.HasValue && (item.Attributes & criteria.ExcludedAttributes.Value) != 0)
                continue;

            // Query / Name filter
            if (regex != null)
            {
                if (!regex.IsMatch(item.Name))
                    continue;
            }
            else if (!string.IsNullOrEmpty(queryPattern))
            {
                if (!WildcardMatch(item.Name, queryPattern, criteria.MatchCase))
                    continue;
            }

            result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Fast wildcard matcher supporting '*' and '?'.
    /// </summary>
    public static bool WildcardMatch(string text, string pattern, bool matchCase = false)
    {
        if (pattern == "*") return true;

        var comp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (!pattern.Contains('*') && !pattern.Contains('?'))
        {
            return text.IndexOf(pattern, comp) >= 0;
        }

        // Convert simple wildcard to regex
        string regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".") + "$";

        var regexOpt = matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
        return Regex.IsMatch(text, regexPattern, regexOpt);
    }
}
