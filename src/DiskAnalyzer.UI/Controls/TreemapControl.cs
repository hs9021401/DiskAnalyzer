using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DiskAnalyzer.Core.Models;
using DiskAnalyzer.Core.Treemap;
using DiskAnalyzer.UI.Helpers;
using DiskAnalyzer.UI.Localization;

namespace DiskAnalyzer.UI;

public class TreemapControl : FrameworkElement
{
    private List<TreemapNode> _nodes = [];
    private TreemapNode? _hoveredNode;
    private readonly ToolTip _richToolTip = new();
    private readonly Typeface _typeface = new("Segoe UI");
    private readonly LocalizationService _localization = LocalizationService.Instance;
    private static readonly ConcurrentDictionary<string, (Brush Fill, Pen Border)> s_brushPenCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Pen s_selectedPen;
    private static readonly Pen s_hoveredPen;
    private static readonly Pen s_defaultPen;
    private static readonly Brush s_highlightOverlayBrush;
    private static readonly Brush s_bgBrush;
    private static readonly Brush s_cushionOverlayBrush;

    static TreemapControl()
    {
        var selColor = Color.FromRgb(0, 198, 255); // Neon Cyan #00C6FF
        s_selectedPen = new Pen(new SolidColorBrush(selColor), 2.5);
        s_selectedPen.Freeze();

        var hovColor = Color.FromArgb(220, 255, 255, 255); // Bright white
        s_hoveredPen = new Pen(new SolidColorBrush(hovColor), 1.5);
        s_hoveredPen.Freeze();

        var defColor = Color.FromArgb(180, 15, 15, 22);
        s_defaultPen = new Pen(new SolidColorBrush(defColor), 0.75);
        s_defaultPen.Freeze();

        var highlightBrush = new SolidColorBrush(Color.FromArgb(50, 0, 198, 255));
        highlightBrush.Freeze();
        s_highlightOverlayBrush = highlightBrush;

        var bg = new SolidColorBrush(Color.FromRgb(24, 24, 36)); // #181824
        bg.Freeze();
        s_bgBrush = bg;

        // Cushion gradient overlay for rich 3D look
        var gradient = new LinearGradientBrush(
            Color.FromArgb(45, 255, 255, 255),
            Color.FromArgb(60, 0, 0, 0),
            new Point(0, 0),
            new Point(1, 1));
        gradient.Freeze();
        s_cushionOverlayBrush = gradient;
    }

    public TreemapControl()
    {
        ClipToBounds = true;
        Focusable = true;

        _localization.LanguageChanged += (_, _) =>
        {
            void RefreshToolTip()
            {
                if (_hoveredNode != null)
                {
                    UpdateToolTipContent(_hoveredNode.Item);
                }
            }

            if (Dispatcher.CheckAccess())
            {
                RefreshToolTip();
            }
            else
            {
                _ = Dispatcher.InvokeAsync(RefreshToolTip);
            }
        };

        SetupRichToolTip();
        ToolTip = _richToolTip;

        Loaded += (_, _) => { if (IsVisible) RecomputeLayout(); };
        SizeChanged += (_, _) => { if (IsVisible) RecomputeLayout(); };
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                RecomputeLayout();
            }
            else
            {
                _nodes = [];
                _hoveredNode = null;
                InvalidateVisual();
            }
        };
    }

    #region Dependency Properties

    public static readonly DependencyProperty RootItemProperty = DependencyProperty.Register(
        nameof(RootItem),
        typeof(FileSystemItem),
        typeof(TreemapControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnRootItemChanged));

    public FileSystemItem? RootItem
    {
        get => (FileSystemItem?)GetValue(RootItemProperty);
        set => SetValue(RootItemProperty, value);
    }

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem),
        typeof(FileSystemItem),
        typeof(TreemapControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSelectedItemChanged));

    public FileSystemItem? SelectedItem
    {
        get => (FileSystemItem?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly DependencyProperty ItemClickedCommandProperty = DependencyProperty.Register(
        nameof(ItemClickedCommand),
        typeof(ICommand),
        typeof(TreemapControl),
        new PropertyMetadata(null));

    public ICommand? ItemClickedCommand
    {
        get => (ICommand?)GetValue(ItemClickedCommandProperty);
        set => SetValue(ItemClickedCommandProperty, value);
    }

    public static readonly DependencyProperty ItemDoubleClickedCommandProperty = DependencyProperty.Register(
        nameof(ItemDoubleClickedCommand),
        typeof(ICommand),
        typeof(TreemapControl),
        new PropertyMetadata(null));

    public ICommand? ItemDoubleClickedCommand
    {
        get => (ICommand?)GetValue(ItemDoubleClickedCommandProperty);
        set => SetValue(ItemDoubleClickedCommandProperty, value);
    }

    #endregion

    #region Events

    public event EventHandler<FileSystemItem>? ItemClicked;
    public event EventHandler<FileSystemItem>? ItemDoubleClicked;

    #endregion

    private static void OnRootItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TreemapControl control && control.IsVisible)
        {
            control.RecomputeLayout();
        }
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TreemapControl control && control.IsVisible)
        {
            control.InvalidateVisual();
        }
    }

    public void RecomputeLayout()
    {
        if (!IsVisible || ActualWidth <= 0 || ActualHeight <= 0 || RootItem == null)
        {
            _nodes = [];
            InvalidateVisual();
            return;
        }

        var bounds = new RectD(0, 0, ActualWidth, ActualHeight);
        _nodes = SquarifiedTreemap.ComputeLayout(RootItem, bounds, maxDepth: 4, minPixelSize: 3.5);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (!IsVisible || ActualWidth <= 0 || ActualHeight <= 0)
            return;


        var entireRect = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(s_bgBrush, null, entireRect);

        if (RootItem == null || _nodes.Count == 0)
        {
            var noDataText = new FormattedText(
                _localization.Get("NoTreemapDataMessage"),
                _localization.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                14,
                new SolidColorBrush(Color.FromRgb(160, 160, 184)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            var pt = new Point(
                Math.Max(0, (ActualWidth - noDataText.Width) / 2),
                Math.Max(0, (ActualHeight - noDataText.Height) / 2));
            dc.DrawText(noDataText, pt);
            return;
        }

        TreemapNode? selectedNode = null;
        TreemapNode? hoveredNode = _hoveredNode;

        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // Render base tiles
        foreach (var node in _nodes)
        {
            var b = node.Bounds;
            var wpfRect = new Rect(b.X, b.Y, b.Width, b.Height);

            if (wpfRect.Width <= 0 || wpfRect.Height <= 0)
                continue;

            if (node.Item == SelectedItem)
            {
                selectedNode = node;
            }

            var (fillBrush, borderPen) = GetCachedBrushAndPen(node.ColorHex);

            // Draw tile fill & subtle dark border
            dc.DrawRectangle(fillBrush, borderPen, wpfRect);

            // Cushion gradient overlay for depth
            if (wpfRect.Width > 6 && wpfRect.Height > 6)
            {
                dc.DrawRectangle(s_cushionOverlayBrush, null, wpfRect);
            }

            // Draw text labels for sufficiently large rectangles
            if (wpfRect.Width > 55 && wpfRect.Height > 24)
            {
                dc.PushClip(new RectangleGeometry(new Rect(wpfRect.X + 2, wpfRect.Y + 2, Math.Max(0, wpfRect.Width - 4), Math.Max(0, wpfRect.Height - 4))));

                string label = node.Item.Name;
                var text = new FormattedText(
                    label,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    _typeface,
                    11,
                    Brushes.White,
                    dpi);

                text.MaxTextWidth = Math.Max(1, wpfRect.Width - 6);
                text.MaxLineCount = 1;
                text.Trimming = TextTrimming.CharacterEllipsis;

                dc.DrawText(text, new Point(wpfRect.X + 4, wpfRect.Y + 3));

                if (wpfRect.Height > 40)
                {
                    var sizeText = new FormattedText(
                        node.Item.SizeFormatted,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        _typeface,
                        10,
                        new SolidColorBrush(Color.FromArgb(220, 220, 220, 240)),
                        dpi);

                    sizeText.MaxTextWidth = Math.Max(1, wpfRect.Width - 6);
                    sizeText.MaxLineCount = 1;
                    sizeText.Trimming = TextTrimming.CharacterEllipsis;

                    dc.DrawText(sizeText, new Point(wpfRect.X + 4, wpfRect.Y + 18));
                }

                dc.Pop(); // Pop clip
            }
        }

        // Draw hover highlight
        if (hoveredNode != null && hoveredNode != selectedNode)
        {
            var hb = hoveredNode.Bounds;
            var hRect = new Rect(hb.X, hb.Y, hb.Width, hb.Height);
            dc.DrawRectangle(null, s_hoveredPen, hRect);
        }

        // Draw selected highlight (with glow)
        if (selectedNode != null)
        {
            var sb = selectedNode.Bounds;
            var sRect = new Rect(sb.X, sb.Y, sb.Width, sb.Height);
            dc.DrawRectangle(s_highlightOverlayBrush, s_selectedPen, sRect);
        }
    }

    private static (Brush Fill, Pen Border) GetCachedBrushAndPen(string colorHex)
    {
        return s_brushPenCache.GetOrAdd(colorHex, static hex =>
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return (brush, s_defaultPen);
            }
            catch
            {
                var fallback = new SolidColorBrush(Color.FromRgb(100, 100, 100));
                fallback.Freeze();
                return (fallback, s_defaultPen);
            }
        });
    }

    #region Mouse Interactions

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pos = e.GetPosition(this);
        var hit = SquarifiedTreemap.HitTest(_nodes, pos.X, pos.Y);

        if (hit != _hoveredNode)
        {
            _hoveredNode = hit;
            InvalidateVisual();

            if (hit != null)
            {
                UpdateToolTipContent(hit.Item);
                _richToolTip.IsOpen = true;
            }
            else
            {
                _richToolTip.IsOpen = false;
            }
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveredNode != null)
        {
            _hoveredNode = null;
            _richToolTip.IsOpen = false;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();

        var pos = e.GetPosition(this);
        var hit = SquarifiedTreemap.HitTest(_nodes, pos.X, pos.Y);

        if (hit != null)
        {
            SelectedItem = hit.Item;
            ItemClicked?.Invoke(this, hit.Item);
            ItemClickedCommand?.Execute(hit.Item);

            if (e.ClickCount == 2)
            {
                if (hit.Item.IsDirectory)
                {
                    ItemDoubleClicked?.Invoke(this, hit.Item);
                    ItemDoubleClickedCommand?.Execute(hit.Item);
                }
                else
                {
                    DiskAnalyzer.Core.Native.ShellOperations.Open(hit.Item.GetFullPath());
                }
            }

            InvalidateVisual();
        }
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        var pos = e.GetPosition(this);
        var hit = SquarifiedTreemap.HitTest(_nodes, pos.X, pos.Y);

        if (hit != null)
        {
            SelectedItem = hit.Item;
            ItemClicked?.Invoke(this, hit.Item);
            ItemClickedCommand?.Execute(hit.Item);
            InvalidateVisual();
        }
    }

    #endregion

    #region ToolTip Styling

    private void SetupRichToolTip()
    {
        _richToolTip.Background = new SolidColorBrush(Color.FromArgb(245, 26, 26, 38)); // #1A1A26
        _richToolTip.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 198, 255)); // Cyan border
        _richToolTip.BorderThickness = new Thickness(1);
        _richToolTip.Padding = new Thickness(10);
        _richToolTip.HasDropShadow = true;
    }

    private void UpdateToolTipContent(FileSystemItem item)
    {
        var grid = new Grid { MinWidth = 260 };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header: Name & Type Icon
        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

        var icon = new Image
        {
            Source = item.IsDirectory ? IconHelper.GetFolderIcon() : IconHelper.GetIconForExtension(item.Extension),
            Width = 18,
            Height = 18,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        headerPanel.Children.Add(icon);

        var titleText = new TextBlock
        {
            Text = item.Name,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 360,
            VerticalAlignment = VerticalAlignment.Center
        };
        headerPanel.Children.Add(titleText);

        Grid.SetRow(headerPanel, 0);
        grid.Children.Add(headerPanel);

        // Path
        var pathText = new TextBlock
        {
            Text = item.GetFullPath(),
            FontSize = 10.5,
            Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 184)),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 380,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(pathText, 1);
        grid.Children.Add(pathText);

        // Separator
        var sep = new Rectangle
        {
            Height = 1,
            Fill = new SolidColorBrush(Color.FromRgb(45, 45, 65)),
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(sep, 2);
        grid.Children.Add(sep);

        // Metrics Grid
        var statsGrid = new Grid();
        statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int row = 0;
        void AddStat(string label, string val)
        {
            statsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 165)),
                FontSize = 11,
                Margin = new Thickness(0, 1, 0, 1)
            };
            Grid.SetRow(lbl, row);
            Grid.SetColumn(lbl, 0);
            statsGrid.Children.Add(lbl);

            var valBlock = new TextBlock
            {
                Text = val,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Margin = new Thickness(0, 1, 0, 1)
            };
            Grid.SetRow(valBlock, row);
            Grid.SetColumn(valBlock, 1);
            statsGrid.Children.Add(valBlock);

            row++;
        }

        AddStat(
            _localization.Get("SizeHeader") + ":",
            $"{item.SizeFormatted} ({item.Size.ToString("N0", _localization.CurrentCulture)} {_localization.Get("BytesLabel")})");
        AddStat(
            _localization.Get("AllocatedHeader") + ":",
            $"{item.AllocatedSizeFormatted} ({item.AllocatedSize.ToString("N0", _localization.CurrentCulture)} {_localization.Get("BytesLabel")})");
        if (item.Percentage > 0)
        {
            AddStat(
                _localization.Get("TreeParentPercentHeader") + ":",
                item.Percentage.ToString("F1", _localization.CurrentCulture) + "%");
        }
        if (item.IsDirectory)
        {
            AddStat(
                _localization.Get("FilesHeader") + " / " + _localization.Get("FoldersHeader") + ":",
                _localization.Format(
                    "TreemapFilesFoldersValueFormat",
                    item.FileCount.ToString("N0", _localization.CurrentCulture),
                    item.FolderCount.ToString("N0", _localization.CurrentCulture)));
        }
        if (item.LastModified.HasValue)
        {
            AddStat(
                _localization.Get("LastModifiedHeader") + ":",
                item.LastModified.Value.ToString("yyyy-MM-dd HH:mm:ss", _localization.CurrentCulture));
        }
        if (!string.IsNullOrEmpty(item.Extension))
        {
            AddStat(string.Empty, _localization.Format("TreemapTypeFormat", item.Extension.ToUpperInvariant()));
        }

        Grid.SetRow(statsGrid, 3);
        grid.Children.Add(statsGrid);

        _richToolTip.Content = grid;
    }

    #endregion
}
