using System;
using System.Globalization;
using System.Windows.Data;
using DiskAnalyzer.Core.Models;
using DiskAnalyzer.UI.Localization;

namespace DiskAnalyzer.UI;

public sealed class DriveDisplayTitleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DriveInfoModel drive)
            return string.Empty;

        string name = string.IsNullOrWhiteSpace(drive.Label)
            ? LocalizationService.Instance.Get("LocalDiskLabel")
            : drive.Label;
        return $"{name} ({drive.DriveLetter}) [{drive.FileSystemName} - {drive.TotalBytesFormatted}]";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
