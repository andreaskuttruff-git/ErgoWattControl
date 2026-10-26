using System.Drawing.Drawing2D;

namespace ErgoWattControl;

public sealed record TrendSeries(string Name, Color Color);

/// <summary>Rolling multi-line chart of the last 10 minutes on a shared axis.</summary>
public sealed class TrendGraph : Control
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan GapThreshold = TimeSpan.FromSeconds(5);

    private readonly List<(DateTime Time, int[] Values, (int Lo, int Hi)? Band)> _points = [];
    private readonly TrendSeries[] _series;
    private readonly Color? _bandColor;
    private readonly int _bottom;
    private readonly int _minTop;
    private readonly int _gridStep;

    /// <param name="bandColor">Fill colour of an optional target band drawn behind the lines (e.g. allowed RPM range).</param>
    public TrendGraph(TrendSeries[] series, int bottom, int minTop, int gridStep, Color? bandColor = null)
    {
        _series = series;
        _bandColor = bandColor;
        _bottom = bottom;
        _minTop = minTop;
        _gridStep = gridStep;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(24, 24, 24);
    }

    /// <summary>One value per series, in constructor order.</summary>
    public void Add(params int[] values) => Add(values, null);

    public void Add(int[] values, (int Lo, int Hi)? band)
    {
        var now = DateTime.Now;
        if (_points.Count > 0 && now - _points[^1].Time < SampleInterval)
            return;
        _points.Add((now, values, band));
        _points.RemoveAll(p => now - p.Time > Window);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var labelFont = new Font("Segoe UI", 14);
        using var titleFont = new Font("Segoe UI", 16, FontStyle.Bold);
        using var labelBrush = new SolidBrush(Color.FromArgb(150, 150, 150));
        using var gridPen = new Pen(Color.FromArgb(60, 60, 60));

        var plot = new Rectangle(60, 12, Width - 75, Height - 44);
        if (plot.Width < 10 || plot.Height < 10)
            return;

        var max = _points.Count == 0 ? 0 : _points.Max(p => Math.Max(p.Values.Max(), p.Band?.Hi ?? 0));
        var top = (int)Math.Ceiling(Math.Max(_minTop, max * 1.05) / _gridStep) * _gridStep;
        float Y(int v) => plot.Bottom - (Math.Clamp(v, _bottom, top) - _bottom) / (float)(top - _bottom) * plot.Height;

        for (var v = _bottom; v <= top; v += _gridStep)
        {
            var y = Y(v);
            g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            var text = v.ToString();
            var size = g.MeasureString(text, labelFont);
            g.DrawString(text, labelFont, labelBrush, plot.Left - size.Width - 4, y - size.Height / 2);
        }

        var minutes = (int)Window.TotalMinutes;
        for (var m = 0; m <= minutes; m += Math.Max(1, minutes / 5))
        {
            var x = plot.Right - m / (float)minutes * plot.Width;
            g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
            var text = m == 0 ? "jetzt" : $"-{m} min";
            var size = g.MeasureString(text, labelFont);
            g.DrawString(text, labelFont, labelBrush, Math.Clamp(x - size.Width / 2, 0, Width - size.Width), plot.Bottom + 4);
        }

        var legendX = plot.Left + 8f;
        foreach (var s in _series)
        {
            using var brush = new SolidBrush(s.Color);
            g.DrawString(s.Name, titleFont, brush, legendX, plot.Top + 4);
            legendX += g.MeasureString(s.Name, titleFont).Width + 20;
        }

        var now = DateTime.Now;
        float X(DateTime time) => plot.Right - (float)((now - time).TotalSeconds / Window.TotalSeconds) * plot.Width;

        if (_bandColor is { } bandColor)
            DrawBand(g, bandColor, X, Y);

        for (var i = 0; i < _series.Length; i++)
        {
            using var pen = new Pen(_series[i].Color, 3f) { LineJoin = LineJoin.Round };
            PointF? prev = null;
            DateTime prevTime = default;
            foreach (var (time, values, _) in _points)
            {
                var value = values[i];
                if (prev is not null && time - prevTime > GapThreshold)
                    prev = null;

                var point = new PointF(X(time), Y(value));
                if (prev is { } p)
                    g.DrawLine(pen, p, point);
                prev = point;
                prevTime = time;
            }
        }
    }

    /// <summary>Fills the band as one polygon per run of consecutive points (split at data gaps).</summary>
    private void DrawBand(Graphics g, Color color, Func<DateTime, float> x, Func<int, float> y)
    {
        using var fill = new SolidBrush(Color.FromArgb(55, color));
        var upper = new List<PointF>();
        var lower = new List<PointF>();

        void Flush()
        {
            if (upper.Count >= 2)
            {
                lower.Reverse();
                g.FillPolygon(fill, [.. upper, .. lower]);
            }
            upper.Clear();
            lower.Clear();
        }

        DateTime prevTime = default;
        foreach (var (time, _, band) in _points)
        {
            if (band is null || (upper.Count > 0 && time - prevTime > GapThreshold))
                Flush();
            if (band is { } b)
            {
                upper.Add(new PointF(x(time), y(b.Hi)));
                lower.Add(new PointF(x(time), y(b.Lo)));
                prevTime = time;
            }
        }
        Flush();
    }
}
