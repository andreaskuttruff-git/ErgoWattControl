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

    /// <summary>Gradient classes (upper bound in %, fill colour); downhill counts as flat.</summary>
    // Opaque (pre-blended onto the dark background) so overlapping slices don't show as darker stripes.
    private static readonly (double UpTo, Color Color, string Label)[] GradeClasses =
    [
        (2, Opaque(150, 70, 120, 70), "< 2 %"),
        (4, Opaque(170, 140, 165, 55), "2–4"),
        (6, Opaque(180, 205, 175, 40), "4–6"),
        (8, Opaque(190, 230, 120, 30), "6–8"),
        (double.MaxValue, Opaque(200, 210, 50, 40), "> 8 %"),
    ];

    private static Color Opaque(int alpha, int r, int g, int b)
    {
        const int background = 24;
        int Mix(int c) => (c * alpha + background * (255 - alpha)) / 255;
        return Color.FromArgb(Mix(r), Mix(g), Mix(b));
    }

    private static Color GradeColor(double grade)
    {
        var percent = grade * 100;
        foreach (var (upTo, color, _) in GradeClasses)
        {
            if (percent < upTo)
                return color;
        }
        return GradeClasses[^1].Color;
    }
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

    private (double Distance, string Text, Color Color)? _highlight;

    /// <summary>Marks one point of the whole-route profile with a label (overview: the steepest climb).</summary>
    public void SetHighlight(double distance, string text, Color color)
    {
        _highlight = (distance, text, color);
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
            DrawLegend(g, whole, labelFont);
            if (_highlight is { } h)
                DrawHighlight(g, route, whole, h.Distance, h.Text, h.Color, labelFont);
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

    /// <summary>Gradient legend centred in the axis row below the plot, where it never covers the profile.</summary>
    private static void DrawLegend(Graphics g, Rectangle area, Font font)
    {
        const float swatch = 14, swatchGap = 4, itemGap = 16;
        var total = GradeClasses.Sum(c => swatch + swatchGap + g.MeasureString(c.Label, font).Width) + itemGap * (GradeClasses.Length - 1);
        var x = area.Left + (area.Width - total) / 2;
        var y = area.Bottom + 4;
        using var labelBrush = new SolidBrush(LabelColor);
        foreach (var (_, color, label) in GradeClasses)
        {
            using var brush = new SolidBrush(Color.FromArgb(255, color));
            g.FillRectangle(brush, x, y + 5, swatch, swatch);
            x += swatch + swatchGap;
            g.DrawString(label, font, labelBrush, x, y);
            x += g.MeasureString(label, font).Width + itemGap;
        }
    }

    private static void DrawHighlight(Graphics g, GpxRoute route, Rectangle area, double distance, string text, Color color, Font font)
    {
        var span = Math.Max(route.MaxElevation - route.MinElevation, 1);
        var x = XFor(area, 0, route.TotalDistance, distance);
        var y = area.Bottom - (float)((route.ElevationAt(distance) - route.MinElevation) / span) * area.Height;
        using var pen = new Pen(Color.FromArgb(160, color), 1.5f) { DashStyle = DashStyle.Dash };
        using var brush = new SolidBrush(color);
        g.DrawLine(pen, x, area.Top, x, area.Bottom);
        g.FillEllipse(brush, x - 6, y - 6, 12, 12);

        // Label next to the point, flipped to the left near the right edge and kept inside the plot.
        var size = g.MeasureString(text, font);
        var lx = x + 10 + size.Width <= area.Right ? x + 10 : x - 10 - size.Width;
        var ly = Math.Clamp(y - size.Height - 6, area.Top, area.Bottom - size.Height);
        using var back = new SolidBrush(Color.FromArgb(200, 24, 24, 24));
        g.FillRectangle(back, lx - 3, ly, size.Width + 6, size.Height);
        g.DrawString(text, font, brush, lx, ly);
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
        var distances = new List<double>();
        for (var d = start; d < end; d += step)
            distances.Add(d);
        distances.Add(end);
        if (distances.Count < 2)
            return;
        var top = distances.Select(d => new PointF(XFor(area, from, to, d), Y(route.ElevationAt(d)))).ToList();

        // Fill each slice in the colour of its gradient; without antialiasing so neighbouring slices leave no seams.
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;
        for (var i = 1; i < top.Count; i++)
        {
            using var fill = new SolidBrush(GradeColor(route.GradeBetween(distances[i - 1], distances[i])));
            g.FillPolygon(fill, [top[i - 1], top[i], new PointF(top[i].X + 1, area.Bottom), new PointF(top[i - 1].X, area.Bottom)]);
        }
        g.SmoothingMode = smoothing;

        using var line = new Pen(LineColor, 2f);
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
