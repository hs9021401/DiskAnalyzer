using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DiskAnalyzer.Core.Models;
using DiskAnalyzer.UI.Helpers;
using DiskAnalyzer.UI.Localization;

namespace DiskAnalyzer.UI;

public class DriveUsageGauge : Control
{
    private ProgressBar? _progressBar;
    private TextBlock? _titleText;
    private TextBlock? _totalText;
    private TextBlock? _usedText;
    private TextBlock? _freeText;

    static DriveUsageGauge()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DriveUsageGauge), new FrameworkPropertyMetadata(typeof(DriveUsageGauge)));
    }

    public DriveUsageGauge()
    {
        LocalizationService.Instance.LanguageChanged += (_, _) =>
        {
            if (Dispatcher.CheckAccess())
            {
                UpdateDisplay();
            }
            else
            {
                _ = Dispatcher.InvokeAsync(UpdateDisplay);
            }
        };
    }

    #region Dependency Properties

    public static readonly DependencyProperty DriveProperty = DependencyProperty.Register(
        nameof(Drive),
        typeof(DriveInfoModel),
        typeof(DriveUsageGauge),
        new PropertyMetadata(null, OnDriveChanged));

    public DriveInfoModel? Drive
    {
        get => (DriveInfoModel?)GetValue(DriveProperty);
        set => SetValue(DriveProperty, value);
    }

    public static readonly DependencyProperty CustomPathProperty = DependencyProperty.Register(
        nameof(CustomPath),
        typeof(string),
        typeof(DriveUsageGauge),
        new PropertyMetadata(string.Empty, OnDriveChanged));

    public string CustomPath
    {
        get => (string)GetValue(CustomPathProperty);
        set => SetValue(CustomPathProperty, value);
    }

    #endregion

    private static void OnDriveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DriveUsageGauge gauge)
        {
            gauge.UpdateDisplay();
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _progressBar = GetTemplateChild("PART_ProgressBar") as ProgressBar;
        _titleText = GetTemplateChild("PART_TitleText") as TextBlock;
        _totalText = GetTemplateChild("PART_TotalText") as TextBlock;
        _usedText = GetTemplateChild("PART_UsedText") as TextBlock;
        _freeText = GetTemplateChild("PART_FreeText") as TextBlock;

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_progressBar == null || _titleText == null || _totalText == null || _usedText == null || _freeText == null)
            return;

        LocalizationService localization = LocalizationService.Instance;

        if (!string.IsNullOrWhiteSpace(CustomPath))
        {
            _titleText.Text = localization.Format("FolderAnalysisTitle", CustomPath);
            _totalText.Text = localization.Get("CustomFolderLabel");
            _usedText.Text = localization.Get("ScanningActiveTarget");
            _freeText.Text = string.Empty;
            _progressBar.IsIndeterminate = false;
            _progressBar.Value = 100;
            return;
        }

        if (Drive == null)
        {
            _titleText.Text = localization.Get("NoDriveSelected");
            _totalText.Text = "-";
            _usedText.Text = localization.Format("UsedFormat", "-", "-");
            _freeText.Text = localization.Format("FreeFormat", "-", "-");
            _progressBar.Value = 0;
            return;
        }

        string driveName = string.IsNullOrWhiteSpace(Drive.Label)
            ? localization.Get("LocalDiskLabel")
            : Drive.Label;
        _titleText.Text = $"{driveName} ({Drive.DriveLetter}) - {Drive.FileSystemName}";
        _totalText.Text = localization.Format("TotalFormat", Drive.TotalBytesFormatted);

        double pct = Drive.UsagePercentage;
        string usedPercent = pct.ToString("F1", localization.CurrentCulture);
        string freePercent = (100.0 - pct).ToString("F1", localization.CurrentCulture);
        _usedText.Text = localization.Format("UsedFormat", Drive.UsedBytesFormatted, usedPercent);
        _freeText.Text = localization.Format("FreeFormat", Drive.FreeBytesFormatted, freePercent);

        _progressBar.Value = Math.Clamp(pct, 0, 100);

        // Highlight bar color if drive is nearly full (> 90%)
        if (pct > 90.0)
        {
            var warningBrush = new LinearGradientBrush(
                Color.FromRgb(255, 82, 82),
                Color.FromRgb(255, 140, 0),
                new Point(0, 0),
                new Point(1, 0));
            _progressBar.Foreground = warningBrush;
        }
        else
        {
            var normalBrush = new LinearGradientBrush(
                Color.FromRgb(0, 114, 255),
                Color.FromRgb(0, 198, 255),
                new Point(0, 0),
                new Point(1, 0));
            _progressBar.Foreground = normalBrush;
        }
    }
}
