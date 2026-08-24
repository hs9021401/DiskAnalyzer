using System;
using System.Collections.Generic;

namespace DiskAnalyzer.Core.Models;

/// <summary>
/// Aggregated summary statistics for a specific file extension.
/// </summary>
public class ExtensionSummary
{
    public string Extension { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public long AllocatedSize { get; set; }
    public long FileCount { get; set; }
    public double Percentage { get; set; }
    public string ColorHex { get; set; } = "#757575";

    public string TotalSizeFormatted => FileSystemItem.FormatBytes(TotalSize);
    public string AllocatedSizeFormatted => FileSystemItem.FormatBytes(AllocatedSize);
    public string FileCountFormatted => FileCount.ToString("N0");
    public string PercentageFormatted => $"{Percentage:F2}%";

    public override string ToString() => $"{Extension}: {TotalSizeFormatted} ({FileCountFormatted} files)";

    /// <summary>
    /// Returns a curated color for popular file extensions.
    /// </summary>
    public static string GetColorForExtension(string ext)
    {
        if (string.IsNullOrEmpty(ext)) return "#78909C"; // Gray-blue
        string clean = ext.TrimStart('.').ToLowerInvariant();

        return clean switch
        {
            // Video
            "mp4" or "mkv" or "avi" or "mov" or "wmv" or "flv" or "webm" or "m4v" or "ts" => "#E53935", // Red
            // Audio
            "mp3" or "flac" or "wav" or "aac" or "ogg" or "wma" or "m4a" or "opus" => "#8E24AA", // Purple
            // Archive / Compressed
            "zip" or "rar" or "7z" or "tar" or "gz" or "bz2" or "xz" or "iso" or "img" or "vhd" or "vhdx" or "cab" => "#FB8C00", // Orange
            // Executable / Binary
            "exe" or "dll" or "sys" or "bin" or "msi" or "so" or "dylib" or "drv" => "#1E88E5", // Blue
            // Document
            "pdf" or "doc" or "docx" or "xls" or "xlsx" or "ppt" or "pptx" or "txt" or "rtf" or "odt" or "csv" => "#43A047", // Green
            // Image
            "jpg" or "jpeg" or "png" or "gif" or "bmp" or "svg" or "webp" or "ico" or "tiff" or "raw" or "psd" => "#00ACC1", // Cyan
            // Development / Source Code
            "cs" or "cpp" or "c" or "h" or "hpp" or "py" or "js" or "ts" or "html" or "css" or "json" or "xml" or "yaml" or "yml" or "rs" or "go" or "java" or "sql" => "#3949AB", // Indigo
            // Database & Virtual Machine
            "db" or "sqlite" or "mdb" or "accdb" or "mdf" or "ldf" or "vmdk" or "vdi" => "#00897B", // Teal
            // System / Temp
            "tmp" or "temp" or "log" or "bak" or "old" or "dmp" or "cache" => "#6D4C41", // Brown
            _ => GenerateDeterministicColor(clean)
        };
    }

    private static string GenerateDeterministicColor(string key)
    {
        uint hash = 2166136261;
        foreach (char c in key)
        {
            hash = (hash ^ c) * 16777619;
        }

        // Generate vibrant HSL color
        double hue = (hash % 360);
        double saturation = 0.65;
        double lightness = 0.50;

        return HslToHex(hue, saturation, lightness);
    }

    private static string HslToHex(double h, double s, double l)
    {
        double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
        double x = c * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
        double m = l - c / 2.0;

        double r = 0, g = 0, b = 0;
        if (h < 60) { r = c; g = x; }
        else if (h < 120) { r = x; g = c; }
        else if (h < 180) { g = c; b = x; }
        else if (h < 240) { g = x; b = c; }
        else if (h < 300) { r = x; b = c; }
        else { r = c; b = x; }

        int red = (int)Math.Round((r + m) * 255.0);
        int green = (int)Math.Round((g + m) * 255.0);
        int blue = (int)Math.Round((b + m) * 255.0);

        return $"#{red:X2}{green:X2}{blue:X2}";
    }
}
