using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DiskAnalyzer.Core.Models;
using DiskAnalyzer.UI.Helpers;

namespace DiskAnalyzer.UI;

public class FileIconConverter : IValueConverter
{
    public static readonly FileIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileSystemItem item)
        {
            if (item.IsDirectory)
            {
                return IconHelper.GetFolderIcon();
            }
            return IconHelper.GetIconForExtension(item.Extension);
        }
        else if (value is string str)
        {
            if (str.EndsWith('\\') || str.EndsWith('/') || !str.Contains('.'))
            {
                return IconHelper.GetFolderIcon();
            }
            return IconHelper.GetIconForExtension(str);
        }
        else if (value is ExtensionSummary extSummary)
        {
            return IconHelper.GetIconForExtension(extSummary.Extension);
        }
        else if (value is DriveInfoModel)
        {
            return IconHelper.GetDriveIcon();
        }

        return IconHelper.GetFolderIcon();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
