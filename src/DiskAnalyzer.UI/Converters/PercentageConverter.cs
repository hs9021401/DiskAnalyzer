using System;
using System.Globalization;
using System.Windows.Data;

namespace DiskAnalyzer.UI;

public class PercentageConverter : IValueConverter
{
    public static readonly PercentageConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return "0.0%";

        double doubleVal = 0.0;
        if (value is double d)
            doubleVal = d;
        else if (value is float f)
            doubleVal = f;
        else if (value is decimal m)
            doubleVal = (double)m;
        else if (double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
            doubleVal = parsed;

        // If parameter is specified as "fraction" or value <= 1.0 (and parameter says fraction), scale by 100
        if (parameter is string paramStr && paramStr.Equals("fraction", StringComparison.OrdinalIgnoreCase))
        {
            doubleVal *= 100.0;
        }

        int decimals = 1;
        if (parameter is string pStr && int.TryParse(pStr, out int customDecimals))
        {
            decimals = customDecimals;
        }

        return $"{doubleVal.ToString($"F{decimals}", CultureInfo.InvariantCulture)}%";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
