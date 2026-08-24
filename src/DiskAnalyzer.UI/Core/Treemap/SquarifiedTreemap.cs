using System;
using System.Collections.Generic;
using DiskAnalyzer.Core.Models;

namespace DiskAnalyzer.Core.Treemap;

/// <summary>
/// High-performance implementation of the Bruls, Huizing, van Wijk Squarified Treemap layout algorithm.
/// Generates optimally aspect-ratioed bounding rectangles for disk visualization.
/// </summary>
public static class SquarifiedTreemap
{
    private readonly record struct LayoutWorkItem(
        FileSystemItem Item,
        RectD Bounds,
        int Depth);

    private readonly record struct Element(
        FileSystemItem Item,
        double NormalizedArea);

    /// <summary>
    /// Generates a squarified treemap layout for the given file system hierarchy.
    /// </summary>
    /// <param name="root">Root file system item</param>
    /// <param name="bounds">Bounding rectangle in pixels</param>
    /// <param name="maxDepth">Maximum depth of hierarchy to render (null for unlimited)</param>
    /// <param name="minPixelSize">Minimum width and height in pixels required to render child rectangles</param>
    /// <returns>List of treemap nodes ready for rendering</returns>
    public static List<TreemapNode> ComputeLayout(
        FileSystemItem root,
        RectD bounds,
        int? maxDepth = null,
        double minPixelSize = 3.0)
    {
        var result = new List<TreemapNode>(5000);
        if (root == null || bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            return result;

        var stack = new Stack<LayoutWorkItem>();
        stack.Push(new LayoutWorkItem(root, bounds, 0));

        while (stack.Count > 0)
        {
            var work = stack.Pop();
            var item = work.Item;
            var currentBounds = work.Bounds;
            int depth = work.Depth;

            // If item has no children or reached max depth or bounds too small for children
            if (!item.HasChildren || (maxDepth.HasValue && depth >= maxDepth.Value) ||
                currentBounds.Width < minPixelSize || currentBounds.Height < minPixelSize)
            {
                result.Add(new TreemapNode(
                    item,
                    currentBounds,
                    depth,
                    ColorPalette.GetColor(item, depth)));
                continue;
            }

            // Collect valid non-zero children
            var validChildren = new List<FileSystemItem>(item.Children.Count);
            long totalChildSize = 0;

            foreach (var child in item.Children)
            {
                if (child.Size > 0)
                {
                    validChildren.Add(child);
                    totalChildSize += child.Size;
                }
            }

            if (validChildren.Count == 0 || totalChildSize == 0)
            {
                result.Add(new TreemapNode(
                    item,
                    currentBounds,
                    depth,
                    ColorPalette.GetColor(item, depth)));
                continue;
            }

            // Normalize child areas to fit currentBounds.Area
            double totalArea = currentBounds.Area;
            var elements = new List<Element>(validChildren.Count);

            foreach (var child in validChildren)
            {
                double normalizedArea = ((double)child.Size / totalChildSize) * totalArea;
                if (normalizedArea > 0.0001)
                {
                    elements.Add(new Element(child, normalizedArea));
                }
            }

            // Squarify the elements into currentBounds
            var laidOutChildren = LayoutElements(elements, currentBounds);

            // Enqueue children into stack for recursive rendering
            for (int i = laidOutChildren.Count - 1; i >= 0; i--)
            {
                var (childItem, childBounds) = laidOutChildren[i];

                if (childBounds.Width >= minPixelSize && childBounds.Height >= minPixelSize)
                {
                    if (childItem.IsDirectory && childItem.HasChildren && (!maxDepth.HasValue || depth + 1 < maxDepth.Value))
                    {
                        stack.Push(new LayoutWorkItem(childItem, childBounds, depth + 1));
                    }
                    else
                    {
                        result.Add(new TreemapNode(
                            childItem,
                            childBounds,
                            depth + 1,
                            ColorPalette.GetColor(childItem, depth + 1)));
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Squarifies a list of elements within a bounding rectangle.
    /// </summary>
    private static List<(FileSystemItem Item, RectD Bounds)> LayoutElements(
        List<Element> elements,
        RectD initialBounds)
    {
        var result = new List<(FileSystemItem Item, RectD Bounds)>(elements.Count);
        if (elements.Count == 0) return result;

        RectD remainingBounds = initialBounds;
        var currentRow = new List<Element>();
        double currentRowSum = 0;

        int index = 0;
        while (index < elements.Count)
        {
            var element = elements[index];
            double shortSide = Math.Min(remainingBounds.Width, remainingBounds.Height);

            if (shortSide <= 0.0001)
                break;

            if (currentRow.Count == 0)
            {
                currentRow.Add(element);
                currentRowSum = element.NormalizedArea;
                index++;
            }
            else
            {
                double currentWorst = WorstAspectRatio(currentRow, currentRowSum, shortSide);
                double newSum = currentRowSum + element.NormalizedArea;
                double candidateWorst = WorstAspectRatio(currentRow, element.NormalizedArea, newSum, shortSide);

                if (candidateWorst <= currentWorst)
                {
                    // Adding element improves or maintains aspect ratio
                    currentRow.Add(element);
                    currentRowSum = newSum;
                    index++;
                }
                else
                {
                    // Aspect ratio worsened; layout currentRow and start new row
                    remainingBounds = LayoutRow(currentRow, currentRowSum, remainingBounds, result);
                    currentRow.Clear();
                    currentRowSum = 0;
                }
            }
        }

        if (currentRow.Count > 0 && remainingBounds.Width > 0 && remainingBounds.Height > 0)
        {
            LayoutRow(currentRow, currentRowSum, remainingBounds, result);
        }

        return result;
    }

    private static double WorstAspectRatio(List<Element> row, double totalArea, double shortSide)
    {
        if (totalArea <= 0 || shortSide <= 0) return double.MaxValue;

        double maxArea = double.MinValue;
        double minArea = double.MaxValue;

        foreach (var el in row)
        {
            if (el.NormalizedArea > maxArea) maxArea = el.NormalizedArea;
            if (el.NormalizedArea < minArea) minArea = el.NormalizedArea;
        }

        double sideSquared = shortSide * shortSide;
        double totalSquared = totalArea * totalArea;

        return Math.Max(
            (sideSquared * maxArea) / totalSquared,
            totalSquared / (sideSquared * minArea));
    }

    private static double WorstAspectRatio(List<Element> row, double addedArea, double totalArea, double shortSide)
    {
        if (totalArea <= 0 || shortSide <= 0) return double.MaxValue;

        double maxArea = addedArea;
        double minArea = addedArea;

        foreach (var el in row)
        {
            if (el.NormalizedArea > maxArea) maxArea = el.NormalizedArea;
            if (el.NormalizedArea < minArea) minArea = el.NormalizedArea;
        }

        double sideSquared = shortSide * shortSide;
        double totalSquared = totalArea * totalArea;

        return Math.Max(
            (sideSquared * maxArea) / totalSquared,
            totalSquared / (sideSquared * minArea));
    }

    private static RectD LayoutRow(
        List<Element> row,
        double rowTotalArea,
        RectD bounds,
        List<(FileSystemItem Item, RectD Bounds)> result)
    {
        if (row.Count == 0 || rowTotalArea <= 0) return bounds;

        bool isHorizontal = bounds.Width >= bounds.Height;
        double side = isHorizontal ? bounds.Height : bounds.Width;
        double rowThickness = rowTotalArea / side;

        if (isHorizontal)
        {
            // Vertical strip (cut off width = rowThickness from left)
            double currentY = bounds.Y;
            foreach (var el in row)
            {
                double itemHeight = (el.NormalizedArea / rowTotalArea) * bounds.Height;
                result.Add((el.Item, new RectD(bounds.X, currentY, rowThickness, itemHeight)));
                currentY += itemHeight;
            }

            return new RectD(
                bounds.X + rowThickness,
                bounds.Y,
                Math.Max(0, bounds.Width - rowThickness),
                bounds.Height);
        }
        else
        {
            // Horizontal strip (cut off height = rowThickness from top)
            double currentX = bounds.X;
            foreach (var el in row)
            {
                double itemWidth = (el.NormalizedArea / rowTotalArea) * bounds.Width;
                result.Add((el.Item, new RectD(currentX, bounds.Y, itemWidth, rowThickness)));
                currentX += itemWidth;
            }

            return new RectD(
                bounds.X,
                bounds.Y + rowThickness,
                bounds.Width,
                Math.Max(0, bounds.Height - rowThickness));
        }
    }

    /// <summary>
    /// Hit-tests for the deepest treemap node containing the specified point.
    /// </summary>
    public static TreemapNode? HitTest(IEnumerable<TreemapNode> nodes, double x, double y)
    {
        TreemapNode? best = null;
        int maxDepth = -1;

        foreach (var node in nodes)
        {
            if (node.Bounds.Contains(x, y))
            {
                if (node.Depth > maxDepth)
                {
                    maxDepth = node.Depth;
                    best = node;
                }
            }
        }

        return best;
    }
}
