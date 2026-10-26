using System.Drawing.Drawing2D;

namespace ErgoWattControl;

/// <summary>
/// Elevation profile of a route. With <see cref="ShowPreview"/> it shows the whole route as a narrow strip
/// plus an enlarged look-ahead of the next kilometres; otherwise only the whole route.
/// </summary>
public sealed class RouteProfile : Control
{
    private const double PreviewBehind = 200;
    private const double PreviewAhead = 2000;
    private const double MinPreviewElevationSpan = 20;

    private static readonly Color FillColor = Color.FromArgb(90, 140, 200, 110);
    private static readonly Color LineColor = Color.FromArgb(170, 220, 140);
    private static readonly Color MarkerColor = Color.Gold;
    private static readonly Color LabelColor = Color.FromArgb(150, 150, 150);

    private GpxRoute? _route;
    private double _position;
    private double _gradePercent;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowPreview { get; set; } = true;

    public RouteProfile()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(24, 24, 24);
    }

    public void SetRoute(GpxRoute? route)
    {
        _route = route;
        _position = 0;
        Invalidate();
    }

    public void SetPosition(double position, double gradePercent)
    {
        _position = position;
        _gradePercent = gradePercent;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_route is not { } route)
            return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var labelFont = new Font("Segoe UI", 14);
        using var titleFont = new Font("Segoe UI", 16, FontStyle.Bold);

        if (!ShowPreview)
        {
            var whole = new Rectangle(60, 8, Width - 75, Height - 40);
            DrawProfile(g, route, whole, 0, route.TotalDistance, route.MinElevation, route.MaxElevation, null, labelFont);
            using var brush = new SolidBrush(LabelColor);
            g.DrawString("0 km", labelFont, brush, whole.Left, whole.Bottom + 4);
            var endText = $"{route.TotalDistance / 1000:0.0} km";
            g.DrawString(endText, labelFont, brush, whole.Right - g.MeasureString(endText, labelFont).Width, whole.Bottom + 4);
            return;
        }

        var strip = new Rectangle(60, 8, Width - 75, (int)(Height * 0.26));
        DrawProfile(g, route, strip, 0, route.TotalDistance, route.MinElevation, route.MaxElevation, _position, null);

        var from = _position - PreviewBehind;
        var to = _position + PreviewAhead;
        var (lo, hi) = (double.MaxValue, double.MinValue);
        for (var d = Math.Max(0, from); d <= Math.Min(route.TotalDistance, to); d += GpxRoute.Step)
        {
            var ele = route.ElevationAt(d);
            lo = Math.Min(lo, ele);
            hi = Math.Max(hi, ele);
        }
        if (hi - lo < MinPreviewElevationSpan)
        {
            var mid = (hi + lo) / 2;
            (lo, hi) = (mid - MinPreviewElevationSpan / 2, mid + MinPreviewElevationSpan / 2);
        }
        var preview = new Rectangle(60, strip.Bottom + 14, Width - 75, Height - strip.Bottom - 44);
        DrawProfile(g, route, preview, from, to, lo, hi, _position, labelFont);

        using var labelBrush = new SolidBrush(LabelColor);
        g.DrawString("jetzt", labelFont, labelBrush, XFor(preview, from, to, _position) - 20, preview.Bottom + 4);
        var aheadText = "+2 km";
        g.DrawString(aheadText, labelFont, labelBrush, preview.Right - g.MeasureString(aheadText, labelFont).Width, preview.Bottom + 4);

        var arrow = _gradePercent >= 0.5 ? "↗" : _gradePercent <= -0.5 ? "↘" : "→";
        var info = $"{arrow} {_gradePercent:0.0} %    {_position / 1000:0.0} / {route.TotalDistance / 1000:0.0} km    {route.ElevationAt(_position):0} m";
        using var infoBrush = new SolidBrush(LineColor);
        g.DrawString(info, titleFont, infoBrush, preview.Left + 8, preview.Top + 4);
    }

    private static float XFor(Rectangle area, double from, double to, double distance) =>
        area.Left + (float)((distance - from) / (to - from)) * area.Width;

    private static void DrawProfile(Graphics g, GpxRoute route, Rectangle area, double from, double to,
        double lo, double hi, double? marker, Font? axisFont)
    {
        if (area.Width < 10 || area.Height < 10)
            return;
        var span = Math.Max(hi - lo, 1);
        float Y(double ele) => area.Bottom - (float)((ele - lo) / span) * area.Height;

        var start = Math.Max(0, from);
        var end = Math.Min(route.TotalDistance, to);
        var step = Math.Max(GpxRoute.Step, (end - start) / area.Width);
        var top = new List<PointF>();
        for (var d = start; d <= end; d += step)
            top.Add(new PointF(XFor(area, from, to, d), Y(route.ElevationAt(d))));
        top.Add(new PointF(XFor(area, from, to, end), Y(route.ElevationAt(end))));
        if (top.Count < 2)
            return;

        using var fill = new SolidBrush(FillColor);
        using var line = new Pen(LineColor, 2f);
        g.FillPolygon(fill, [.. top, new PointF(top[^1].X, area.Bottom), new PointF(top[0].X, area.Bottom)]);
        g.DrawLines(line, top.ToArray());

        if (axisFont is not null)
        {
            using var labelBrush = new SolidBrush(LabelColor);
            foreach (var ele in new[] { lo, hi })
            {
                var text = $"{ele:0}";
                var size = g.MeasureString(text, axisFont);
                g.DrawString(text, axisFont, labelBrush, area.Left - size.Width - 4, Y(ele) - size.Height / 2);
            }
        }

        if (marker is { } m)
        {
            var x = XFor(area, from, to, m);
            var y = Y(route.ElevationAt(m));
            using var markerPen = new Pen(Color.FromArgb(120, MarkerColor), 1.5f);
            using var markerBrush = new SolidBrush(MarkerColor);
            g.DrawLine(markerPen, x, area.Top, x, area.Bottom);
            g.FillEllipse(markerBrush, x - 6, y - 6, 12, 12);
        }
    }
}
