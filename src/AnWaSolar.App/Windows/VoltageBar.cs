using System.Windows;
using System.Windows.Media;

using AnWaSolar.Core.Calculation;

namespace AnWaSolar.Windows;

/// <summary>
/// Draws the voltage window of one MPPT on a scale from 0 V: the effective MPPT range as a band, the string Vmpp range
/// as a bar, the highest string Voc as a marker and the effective maximum DC voltage as a line. It only draws the
/// values of <see cref="VoltageWindow"/>; the checks are made by the calculation.
/// </summary>
public sealed class VoltageBar : FrameworkElement
{
    public static readonly DependencyProperty VoltageProperty = Register<VoltageWindow?>(nameof(Voltage));
    public static readonly DependencyProperty TrackBrushProperty = Register<Brush?>(nameof(TrackBrush));
    public static readonly DependencyProperty RangeBrushProperty = Register<Brush?>(nameof(RangeBrush));
    public static readonly DependencyProperty OkBrushProperty = Register<Brush?>(nameof(OkBrush));
    public static readonly DependencyProperty ErrorBrushProperty = Register<Brush?>(nameof(ErrorBrush));
    public static readonly DependencyProperty LimitBrushProperty = Register<Brush?>(nameof(LimitBrush));

    public VoltageBar()
    {
        Height = 30;
        SnapsToDevicePixels = true;
    }

    public VoltageWindow? Voltage { get => (VoltageWindow?)GetValue(VoltageProperty); set => SetValue(VoltageProperty, value); }

    public Brush? TrackBrush { get => (Brush?)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public Brush? RangeBrush { get => (Brush?)GetValue(RangeBrushProperty); set => SetValue(RangeBrushProperty, value); }

    public Brush? OkBrush { get => (Brush?)GetValue(OkBrushProperty); set => SetValue(OkBrushProperty, value); }

    public Brush? ErrorBrush { get => (Brush?)GetValue(ErrorBrushProperty); set => SetValue(ErrorBrushProperty, value); }

    public Brush? LimitBrush { get => (Brush?)GetValue(LimitBrushProperty); set => SetValue(LimitBrushProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        if (Voltage is not { ScaleMaxV: > 0 } v || ActualWidth <= 0)
        {
            return;
        }

        double width = ActualWidth;
        double X(double volt) => Math.Clamp(volt / v.ScaleMaxV, 0, 1) * width;
        const double TrackTop = 9;
        const double TrackHeight = 12;

        dc.DrawRoundedRectangle(TrackBrush, null, new Rect(0, TrackTop, width, TrackHeight), 6, 6);
        if (v.MpptUpperV > v.MpptLowerV)
        {
            dc.DrawRectangle(RangeBrush, null, new Rect(X(v.MpptLowerV), TrackTop, X(v.MpptUpperV) - X(v.MpptLowerV), TrackHeight));
        }

        Brush? vmppBrush = v.VmppOutsideMppt ? ErrorBrush : OkBrush;
        double vmppLeft = X(v.VmppMinV);
        dc.DrawRoundedRectangle(vmppBrush, null,
            new Rect(vmppLeft, TrackTop + 3, Math.Max(3, X(v.VmppMaxV) - vmppLeft), TrackHeight - 6), 3, 3);

        // Maximum DC voltage: a line over the full height.
        var limitPen = new Pen(LimitBrush, 2);
        dc.DrawLine(limitPen, new Point(X(v.MaxDcV), 2), new Point(X(v.MaxDcV), TrackTop + TrackHeight + 7));

        // Highest string Voc: a downward triangle above the track.
        Brush? vocBrush = v.VocExceedsMaxDc ? ErrorBrush : OkBrush;
        double x = X(v.VocMaxV);
        var triangle = new StreamGeometry();
        using (StreamGeometryContext g = triangle.Open())
        {
            g.BeginFigure(new Point(x - 6, 0), true, true);
            g.LineTo(new Point(x + 6, 0), true, false);
            g.LineTo(new Point(x, TrackTop - 1), true, false);
        }

        triangle.Freeze();
        dc.DrawGeometry(vocBrush, null, triangle);
        dc.DrawLine(new Pen(vocBrush, 2), new Point(x, TrackTop - 1), new Point(x, TrackTop + TrackHeight));
    }

    private static DependencyProperty Register<T>(string name) =>
        DependencyProperty.Register(name, typeof(T), typeof(VoltageBar),
            new FrameworkPropertyMetadata(default(T), FrameworkPropertyMetadataOptions.AffectsRender));
}
