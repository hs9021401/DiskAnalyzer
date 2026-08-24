using System;
using System.Collections.Generic;
using DiskAnalyzer.Core.Models;

namespace DiskAnalyzer.Core.Treemap;

/// <summary>
/// Color generation and mapping for treemap nodes.
/// </summary>
public static class ColorPalette
{
    private static readonly string[] s_folderColors =
    [
        "#37474F", // Dark blue gray
        "#455A64",
        "#546E7A",
        "#607D8B",
        "#78909C"
    ];

    public static string GetColor(FileSystemItem item, int depth)
    {
        if (item.IsVirtual)
        {
            if (item.Name.Contains("Free", StringComparison.OrdinalIgnoreCase))
                return "#2E7D32"; // Green for free space
            return "#616161"; // Gray for system space
        }

        if (item.IsDirectory)
        {
            int idx = Math.Clamp(depth, 0, s_folderColors.Length - 1);
            return s_folderColors[idx];
        }

        return ExtensionSummary.GetColorForExtension(item.Extension);
    }
}
