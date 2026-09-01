using System.Windows;
using System.Windows.Controls;

namespace PinteMod.ControlCenter.Controls;

/// <summary>
/// Responsive two-column layout for long settings cards. Unlike UniformGrid,
/// a tall card does not force a matching blank area below its neighbour.
/// </summary>
public sealed class ResponsiveMasonryGrid : Panel
{
    private int _effectiveColumns = 1;

    public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(
        nameof(MinItemWidth), typeof(double), typeof(ResponsiveMasonryGrid),
        new FrameworkPropertyMetadata(240d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty MaxColumnsProperty = DependencyProperty.Register(
        nameof(MaxColumns), typeof(int), typeof(ResponsiveMasonryGrid),
        new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double MinItemWidth
    {
        get => (double)GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    public int MaxColumns
    {
        get => (int)GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var availableWidth = double.IsInfinity(availableSize.Width) || availableSize.Width <= 0
            ? Math.Max(MinItemWidth, 1d)
            : availableSize.Width;
        _effectiveColumns = Math.Clamp(
            (int)Math.Floor(availableWidth / Math.Max(MinItemWidth, 1d)),
            1,
            Math.Max(1, MaxColumns));

        var columnHeights = new double[_effectiveColumns];
        var itemWidth = availableWidth / _effectiveColumns;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(itemWidth, double.PositiveInfinity));
            columnHeights[IndexOfShortest(columnHeights)] += child.DesiredSize.Height;
        }

        return new Size(availableWidth, columnHeights.Max());
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Math.Max(1, _effectiveColumns);
        var columnHeights = new double[columns];
        var itemWidth = finalSize.Width / columns;
        foreach (UIElement child in InternalChildren)
        {
            var column = IndexOfShortest(columnHeights);
            child.Arrange(new Rect(column * itemWidth, columnHeights[column], itemWidth, child.DesiredSize.Height));
            columnHeights[column] += child.DesiredSize.Height;
        }

        return new Size(finalSize.Width, columnHeights.Max());
    }

    private static int IndexOfShortest(IReadOnlyList<double> heights)
    {
        var result = 0;
        for (var index = 1; index < heights.Count; index++)
        {
            if (heights[index] < heights[result]) result = index;
        }

        return result;
    }
}
