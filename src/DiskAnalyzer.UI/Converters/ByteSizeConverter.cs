using System;
using System.Globalization;
using System.Windows.Data;
using DiskAnalyzer.Core.Models;

namespace DiskAnalyzer.UI;

public class ByteSizeConverter : IValueConverter
{
    public static readonly ByteSizeConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long l)
            return FileSystemItem.FormatBytes(l);
        if (value is ulong ul)
            return FileSystemItem.FormatBytes((long)ul);
        if (value is int i)
            return FileSystemItem.FormatBytes(i);
        if (value is double d)
            return FileSystemItem.FormatBytes((long)d);
        if (value is float f)
            return FileSystemItem.FormatBytes((long)f);

        return "0 B";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
