using System;

namespace DiskAnalyzer.Core.Models;

/// <summary>
/// Double precision 2D rectangle for high-precision treemap rendering calculations.
/// </summary>
public readonly struct RectD : IEquatable<RectD>
{
    public double X { get; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }

    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double Area => Width * Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public RectD(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = Math.Max(0, width);
        Height = Math.Max(0, height);
    }

    public bool Contains(double px, double py)
    {
        return px >= X && px <= Right && py >= Y && py <= Bottom;
    }

    public bool Equals(RectD other)
    {
        return Math.Abs(X - other.X) < 0.0001 &&
               Math.Abs(Y - other.Y) < 0.0001 &&
               Math.Abs(Width - other.Width) < 0.0001 &&
               Math.Abs(Height - other.Height) < 0.0001;
    }

    public override bool Equals(object? obj) => obj is RectD other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public override string ToString() => $"[X={X:F1}, Y={Y:F1}, W={Width:F1}, H={Height:F1}]";

    public static bool operator ==(RectD left, RectD right) => left.Equals(right);
    public static bool operator !=(RectD left, RectD right) => !left.Equals(right);
}

/// <summary>
/// Represents a visual rectangle node in the squarified treemap.
/// </summary>
public class TreemapNode
{
    public RectD Bounds { get; set; }
    public FileSystemItem Item { get; set; }
    public int Depth { get; set; }
    public string ColorHex { get; set; } = "#757575";

    public TreemapNode(FileSystemItem item, RectD bounds, int depth = 0, string colorHex = "#757575")
    {
        Item = item;
        Bounds = bounds;
        Depth = depth;
        ColorHex = colorHex;
    }

    public override string ToString() => $"{Item.Name} ({Item.SizeFormatted}) {Bounds}";
}
