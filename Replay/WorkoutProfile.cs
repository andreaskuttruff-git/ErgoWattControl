using System.Drawing.Drawing2D;

namespace ErgoWattControl;

/// <summary>
/// Power profile of a recorded training (step area) with optional original pulse. With <see cref="ShowPreview"/>
/// it shows the whole training as a strip plus an enlarged look-ahead around the current position.
/// </summary>
public sealed class WorkoutProfile : Control
{
    private const double PreviewBehind = 60;
    private const double PreviewAhead = 600;

    private static readonly Color PowerFill = Color.FromArgb(105, 90, 30);
    private static readonly Color PowerLine = Color.Gold;
    private static readonly Color PulseLine = Color.FromArgb(255, 110, 110);
    private static readonly Color MarkerColor = Color.White;
    private static readonly Color LabelColor = Color.FromArgb(150, 150, 150);

    private WorkoutRecording? _workout;
    private Func<double, int>? _wattAt;
    private double _position;
    private bool _showPulse;
    private string _info = "";

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowPreview { get; set; } = true;

    public WorkoutProfile()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(24, 24, 24);
    }

    /// <param name="wattAt">Power to draw at a point of the program (lets the ride view include the rider's offset).</param>
    public void SetWorkout(WorkoutRecording? workout, Func<double, int>? wattAt, bool showPulse)
    {
        _workout = workout;
        _wattAt = wattAt;
        _showPulse = showPulse && workout is { HasPulse: true };
        _position = 0;
        Invalidate();
    }

    public void SetPosition(double seconds, string info)
    {
        _position = seconds;
        _info = info;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_workout is not { } workout)
            return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var labelFont = new Font("Segoe UI", 14);
        using var infoFont = new Font("Segoe UI", 16, FontStyle.Bold);
        using var labelBrush = new SolidBrush(LabelColor);
        var wattAt = _wattAt ?? workout.WattAt;
        var top = Math.Max(100, (int)Math.Ceiling(Math.Max(workout.MaxWatt, wattAt(_position)) * 1.1 / 50) * 50);

        if (!ShowPreview)
        {
            var whole = new Rectangle(60, 8, Width - 120, Height - 40);
            Draw(g, workout, wattAt, whole, 0, workout.Duration, top, null, labelFont);
            g.DrawString("0:00", labelFont, labelBrush, whole.Left, whole.Bottom + 4);
            var end = Format(workout.Duration);
            g.DrawString(end, labelFont, labelBrush, whole.Right - g.MeasureString(end, labelFont).Width, whole.Bottom + 4);
            return;
        }

        var strip = new Rectangle(60, 8, Width - 120, (int)(Height * 0.26));
        Draw(g, workout, wattAt, strip, 0, workout.Duration, top, _position, null);

        var from = _position - PreviewBehind;
        var to = _position + PreviewAhead;
        var preview = new Rectangle(60, strip.Bottom + 14, Width - 120, Height - strip.Bottom - 44);
        Draw(g, workout, wattAt, preview, from, to, top, _position, labelFont);

        var x = XFor(preview, from, to, _position);
        g.DrawString("jetzt", labelFont, labelBrush, x - 20, preview.Bottom + 4);
        var ahead = $"+{PreviewAhead / 60:0} min";
        g.DrawString(ahead, labelFont, labelBrush, preview.Right - g.MeasureString(ahead, labelFont).Width, preview.Bottom + 4);
        using var infoBrush = new SolidBrush(PowerLine);
        g.DrawString(_info, infoFont, infoBrush, preview.Left + 8, preview.Top + 4);
    }

    private void Draw(Graphics g, WorkoutRecording workout, Func<double, int> wattAt, Rectangle area,
        double from, double to, int top, double? marker, Font? axisFont)
    {
        if (area.Width < 10 || area.Height < 10)
            return;
        float Y(double watt) => area.Bottom - (float)(Math.Clamp(watt, 0, top) / top) * area.Height;
        var start = Math.Max(0, from);
        var end = Math.Min(workout.Duration, to);
        if (end <= start)
            return;

        // Sample per pixel: shows the steps of the profile without walking the raw rows.
        var points = new List<PointF>();
        var x0 = XFor(area, from, to, start);
        var x1 = XFor(area, from, to, end);
        for (var x = x0; x <= x1; x += 1)
        {
            var t = from + (x - area.Left) / area.Width * (to - from);
            points.Add(new PointF(x, Y(wattAt(Math.Min(t, end)))));
        }
        if (points.Count < 2)
            return;

        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;
        using (var fill = new SolidBrush(PowerFill))
            g.FillPolygon(fill, [.. points, new PointF(points[^1].X, area.Bottom), new PointF(points[0].X, area.Bottom)]);
        using (var line = new Pen(PowerLine, 2f))
            g.DrawLines(line, points.ToArray());
        g.SmoothingMode = smoothing;

        if (axisFont is not null)
        {
            using var labelBrush = new SolidBrush(LabelColor);
            foreach (var w in new[] { 0, top / 2, top })
            {
                var text = $"{w}";
                var size = g.MeasureString(text, axisFont);
                g.DrawString(text, axisFont, labelBrush, area.Left - size.Width - 4, Y(w) - size.Height / 2);
            }
        }

        if (_showPulse)
            DrawPulse(g, workout, area, from, to, start, end, axisFont);

        if (marker is { } m)
        {
            var x = XFor(area, from, to, m);
            using var pen = new Pen(Color.FromArgb(160, MarkerColor), 1.5f);
            g.DrawLine(pen, x, area.Top, x, area.Bottom);
            using var brush = new SolidBrush(MarkerColor);
            g.FillEllipse(brush, x - 6, Y(wattAt(m)) - 6, 12, 12);
        }
    }

    /// <summary>Original pulse on its own scale (right axis); gaps where the recording had no reading.</summary>
    private static void DrawPulse(Graphics g, WorkoutRecording workout, Rectangle area, double from, double to,
        double start, double end, Font? axisFont)
    {
        const int lo = 60, hi = 180;
        float Y(int pulse) => area.Bottom - (float)(Math.Clamp(pulse, lo, hi) - lo) / (hi - lo) * area.Height;
        using var pen = new Pen(Color.FromArgb(200, PulseLine), 2f);
        PointF? prev = null;
        for (var x = XFor(area, from, to, start); x <= XFor(area, from, to, end); x += 2)
        {
            var t = from + (x - area.Left) / area.Width * (to - from);
            var pulse = workout.PulseAt(Math.Min(t, end));
            if (pulse <= 0)
            {
                prev = null;
                continue;
            }
            var point = new PointF(x, Y(pulse));
            if (prev is { } p)
                g.DrawLine(pen, p, point);
            prev = point;
        }
        if (axisFont is null)
            return;
        using var brush = new SolidBrush(PulseLine);
        foreach (var pulse in new[] { lo, (lo + hi) / 2, hi })
        {
            var size = g.MeasureString($"{pulse}", axisFont);
            g.DrawString($"{pulse}", axisFont, brush, area.Right + 4, Y(pulse) - size.Height / 2);
        }
    }

    private static float XFor(Rectangle area, double from, double to, double t) =>
        area.Left + (float)((t - from) / (to - from)) * area.Width;

    public static string Format(double seconds) => TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
}
