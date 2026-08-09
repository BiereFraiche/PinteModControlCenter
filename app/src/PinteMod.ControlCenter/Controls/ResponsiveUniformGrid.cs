using System.Windows;
using System.Windows.Controls.Primitives;

namespace PinteMod.ControlCenter.Controls;

public sealed class ResponsiveUniformGrid : UniformGrid
{
    public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(
        nameof(MinItemWidth), typeof(double), typeof(ResponsiveUniformGrid),
        new FrameworkPropertyMetadata(240d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty MaxColumnsProperty = DependencyProperty.Register(
        nameof(MaxColumns), typeof(int), typeof(ResponsiveUniformGrid),
        new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsMeasure));

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

    protected override Size MeasureOverride(Size constraint)
    {
        if (!double.IsInfinity(constraint.Width) && constraint.Width > 0 && MinItemWidth > 0)
        {
            Rows = 0;
            Columns = Math.Clamp(
                (int)Math.Floor(constraint.Width / MinItemWidth),
                1,
                Math.Max(1, MaxColumns));
        }

        return base.MeasureOverride(constraint);
    }
}
