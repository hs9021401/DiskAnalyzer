using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DiskAnalyzer.UI;

public class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value is true;

        if (parameter is string paramStr && paramStr.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            b = !b;
        }

        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility v)
        {
            bool b = v == Visibility.Visible;
            if (parameter is string paramStr && paramStr.Equals("invert", StringComparison.OrdinalIgnoreCase))
            {
                b = !b;
            }
            return b;
        }
        return false;
    }
}

public class InvertBoolConverter : IValueConverter
{
    public static readonly InvertBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not true;
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public static readonly NullToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNull = value == null || (value is string s && string.IsNullOrEmpty(s));
        if (parameter is string paramStr && paramStr.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            return isNull ? Visibility.Visible : Visibility.Collapsed;
        }
        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
