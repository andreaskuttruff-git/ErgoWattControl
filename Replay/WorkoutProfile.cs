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
    // The pulse recorded back then is green so it can't be mistaken for today's (red) when a recording is repeated.
    private static readonly Color OriginalPulseLine = Color.FromArgb(110, 210, 120);
    private static readonly Color MarkerColor = Color.White;
    private static readonly Color LabelColor = Color.FromArgb(150, 150, 150);

    private WorkoutRecording? _workout;
    private Func<double, int>? _wattAt;
    private double _position;
    private bool _showPulse;
    private string _info = "";
    private readonly List<(double Seconds, int Pulse)> _livePulse = [];

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
        _livePulse.Clear();
        Invalidate();
    }

    /// <summary>The rider's pulse during this run, drawn into the overview of the whole training.</summary>
    public void AddPulse(double seconds, int pulse)
    {
        if (pulse > 0)
            _livePulse.Add((seconds, pulse));
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

        // The overview gets most of the height: it also carries the rider's pulse over the whole training.
        var strip = new Rectangle(60, 8, Width - 120, (int)(Height * 0.45));
        Draw(g, workout, wattAt, strip, 0, workout.Duration, top, _position, null);
        DrawLivePulse(g, strip, workout.Duration, labelFont);

        var from = _position - PreviewBehind;
        var to = _position + PreviewAhead;
        // Only the time labels go below the preview, so it reaches down to just above them.
        var labelHeight = (int)Math.Ceiling(g.MeasureString("jetzt", labelFont).Height);
        var preview = new Rectangle(60, strip.Bottom + 12, Width - 120, Height - strip.Bottom - 12 - labelHeight);
        // The marker line starts below the announcement so it doesn't strike through the text.
        var infoBottom = preview.Top + 4 + (string.IsNullOrEmpty(_info) ? 0 : g.MeasureString(_info, infoFont).Height);
        // The pulse (then and now) only goes into the overview; the look-ahead stays with the power alone.
        Draw(g, workout, wattAt, preview, from, to, top, _position, labelFont, infoBottom, withPulse: false);

        var x = XFor(preview, from, to, _position);
        g.DrawString("jetzt", labelFont, labelBrush, x - 20, preview.Bottom);
        var ahead = $"+{PreviewAhead / 60:0} min";
        g.DrawString(ahead, labelFont, labelBrush, preview.Right - g.MeasureString(ahead, labelFont).Width, preview.Bottom);
        using var infoBrush = new SolidBrush(PowerLine);
        g.DrawString(_info, infoFont, infoBrush, preview.Left + 8, preview.Top + 4);
    }

    private void Draw(Graphics g, WorkoutRecording workout, Func<double, int> wattAt, Rectangle area,
        double from, double to, int top, double? marker, Font? axisFont, float? markerTop = null, bool withPulse = true)
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

        if (_showPulse && withPulse)
            DrawPulse(g, workout, area, from, to, start, end, axisFont);

        if (marker is { } m)
        {
            var x = XFor(area, from, to, m);
            using var pen = new Pen(Color.FromArgb(160, MarkerColor), 1.5f);
            g.DrawLine(pen, x, Math.Max(area.Top, markerTop ?? area.Top), x, area.Bottom);
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
        using var pen = new Pen(Color.FromArgb(200, OriginalPulseLine), 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
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
        using var brush = new SolidBrush(OriginalPulseLine);
        foreach (var pulse in new[] { lo, (lo + hi) / 2, hi })
        {
            var size = g.MeasureString($"{pulse}", axisFont);
            g.DrawString($"{pulse}", axisFont, brush, area.Right + 4, Y(pulse) - size.Height / 2);
        }
    }

    /// <summary>Pulse of the current run (gaps over 5 s stay open), on the same scale as the original pulse.</summary>
    private void DrawLivePulse(Graphics g, Rectangle area, double duration, Font axisFont)
    {
        if ((_livePulse.Count == 0 && !_showPulse) || duration <= 0)
            return;
        const int lo = 60, hi = 180;
        float Y(int pulse) => area.Bottom - (float)(Math.Clamp(pulse, lo, hi) - lo) / (hi - lo) * area.Height;
        using var pen = new Pen(PulseLine, 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        PointF? prev = null;
        var prevSeconds = double.NegativeInfinity;
        foreach (var (seconds, pulse) in _livePulse)
        {
            var point = new PointF(XFor(area, 0, duration, Math.Min(seconds, duration)), Y(pulse));
            if (prev is { } p && seconds - prevSeconds <= 5)
                g.DrawLine(pen, p, point);
            prev = point;
            prevSeconds = seconds;
        }
        // One scale for both curves: red once today's pulse is there, green while only the recorded one is.
        using var brush = new SolidBrush(_livePulse.Count > 0 ? PulseLine : OriginalPulseLine);
        foreach (var pulse in new[] { lo, (lo + hi) / 2, hi })
        {
            var size = g.MeasureString($"{pulse}", axisFont);
            g.DrawString($"{pulse}", axisFont, brush, area.Right + 4, Math.Clamp(Y(pulse) - size.Height / 2, area.Top - 4, area.Bottom - size.Height + 4));
        }
    }

    private static float XFor(Rectangle area, double from, double to, double t) =>
        area.Left + (float)((t - from) / (to - from)) * area.Width;

    public static string Format(double seconds) => TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
}
