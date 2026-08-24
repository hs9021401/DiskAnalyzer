using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DiskAnalyzer.UI;

public class ColorToBrushConverter : IValueConverter
{
    public static readonly ColorToBrushConverter Instance = new();
    private static readonly ConcurrentDictionary<string, SolidColorBrush> s_brushCache = new(StringComparer.OrdinalIgnoreCase);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            return GetBrush(hex);
        }
        return Brushes.Gray;
    }

    public static SolidColorBrush GetBrush(string hex)
    {
        return s_brushCache.GetOrAdd(hex, static key =>
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(key);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return Brushes.Gray;
            }
        });
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
