using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DiskAnalyzer.Core.Models;
using DiskAnalyzer.UI.Helpers;

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

        if (!string.IsNullOrWhiteSpace(CustomPath))
        {
            _titleText.Text = $"Folder Analysis: {CustomPath}";
            _totalText.Text = "Custom Folder";
            _usedText.Text = "Scanning active target";
            _freeText.Text = string.Empty;
            _progressBar.IsIndeterminate = false;
            _progressBar.Value = 100;
            return;
        }

        if (Drive == null)
        {
            _titleText.Text = "No Drive Selected";
            _totalText.Text = "-";
            _usedText.Text = "Used: -";
            _freeText.Text = "Free: -";
            _progressBar.Value = 0;
            return;
        }

        string driveName = string.IsNullOrWhiteSpace(Drive.Label) ? "Local Disk" : Drive.Label;
        _titleText.Text = $"{driveName} ({Drive.DriveLetter}) - {Drive.FileSystemName}";
        _totalText.Text = $"Total: {Drive.TotalBytesFormatted}";

        double pct = Drive.UsagePercentage;
        _usedText.Text = $"Used: {Drive.UsedBytesFormatted} ({pct:F1}%)";
        _freeText.Text = $"Free: {Drive.FreeBytesFormatted} ({(100.0 - pct):F1}%)";

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
