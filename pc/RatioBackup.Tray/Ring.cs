using System;
using System.Windows;
using System.Windows.Media;

namespace RatioBackup.Tray;

/// <summary>Fortschritts- bzw. Speicherring: Spur + bis zu zwei Segmente (Anteile 0–1).</summary>
public sealed class Ring : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = Reg(nameof(Value), 0.0);
    public static readonly DependencyProperty Value2Property = Reg(nameof(Value2), 0.0);
    public static readonly DependencyProperty ThicknessProperty = Reg(nameof(Thickness), 14.0);
    public static readonly DependencyProperty TrackProperty = RegBrush(nameof(Track));
    public static readonly DependencyProperty FillProperty = RegBrush(nameof(Fill));
    public static readonly DependencyProperty Fill2Property = RegBrush(nameof(Fill2));

    private static DependencyProperty Reg(string n, double d) =>
        DependencyProperty.Register(n, typeof(double), typeof(Ring), new FrameworkPropertyMetadata(d, FrameworkPropertyMetadataOptions.AffectsRender));
    private static DependencyProperty RegBrush(string n) =>
        DependencyProperty.Register(n, typeof(Brush), typeof(Ring), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Value2 { get => (double)GetValue(Value2Property); set => SetValue(Value2Property, value); }
    public double Thickness { get => (double)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }
    public Brush Track { get => (Brush)GetValue(TrackProperty); set => SetValue(TrackProperty, value); }
    public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public Brush Fill2 { get => (Brush)GetValue(Fill2Property); set => SetValue(Fill2Property, value); }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        var r = (size - Thickness) / 2;
        dc.DrawEllipse(null, new Pen(Track, Thickness), c, r, r);
        var v1 = Math.Clamp(Value, 0, 1);
        var v2 = Math.Clamp(Value2, 0, 1 - v1);
        Arc(dc, c, r, 0, v1, Fill);
        Arc(dc, c, r, v1, v2, Fill2);
    }

    private void Arc(DrawingContext dc, Point c, double r, double from, double len, Brush brush)
    {
        if (len <= 0.0005) return;
        var pen = new Pen(brush, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (len >= 0.9995) { dc.DrawEllipse(null, pen, c, r, r); return; }
        Point P(double f) => new(c.X + r * Math.Sin(f * 2 * Math.PI), c.Y - r * Math.Cos(f * 2 * Math.PI));
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(P(from), false, false);
            ctx.ArcTo(P(from + len), new Size(r, r), 0, len > 0.5, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        dc.DrawGeometry(null, pen, g);
    }
}
