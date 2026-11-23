namespace ErgoWattControl;

/// <summary>
/// In dark mode Windows draws a disabled button without a visible frame (64 grey on the 32 background) and with
/// fairly light text, so "Start" without a selection looks like a bare grey patch next to "Abbrechen". Windows paints
/// these buttons itself (no Paint event), so this draws over them right after each WM_PAINT: the same face with a
/// dimmed but clearly visible frame and a much darker label.
/// </summary>
public static class DisabledButtonFrame
{
    internal static readonly Color Face = Color.FromArgb(51, 51, 51);
    internal static readonly Color Frame = Color.FromArgb(110, 110, 110);
    internal static readonly Color Text = Color.FromArgb(95, 95, 95);

    /// <summary>All buttons in this window (also ones added later).</summary>
    public static void Apply(Control root)
    {
        foreach (Control child in root.Controls)
            Apply(child);
        if (root is Button button)
        {
            if (button.IsHandleCreated)
                new Painter(button);
            else
                button.HandleCreated += (_, _) => new Painter(button);
        }
        root.ControlAdded += (_, e) => { if (e.Control is { } added) Apply(added); };
    }

    private sealed class Painter : NativeWindow
    {
        private const int WM_PAINT = 0x000F;
        private const int WM_NCDESTROY = 0x0082;
        private readonly Button _button;

        public Painter(Button button)
        {
            _button = button;
            AssignHandle(button.Handle);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_NCDESTROY)
                ReleaseHandle();
            else if (m.Msg == WM_PAINT && !_button.Enabled && !_button.IsDisposed)
                Draw();
        }

        private void Draw()
        {
            using var g = _button.CreateGraphics();
            var r = _button.ClientRectangle;
            // Rounded like the enabled buttons Windows draws (radius about 4 px, smoothed); the background shows
            // in the corners.
            using (var background = new SolidBrush(_button.Parent?.BackColor ?? Color.FromArgb(32, 32, 32)))
                g.FillRectangle(background, r);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var shape = Rounded(new RectangleF(0.5f, 0.5f, r.Width - 1, r.Height - 1), 4))
            using (var face = new SolidBrush(Face))
            using (var pen = new Pen(Frame))
            {
                g.FillPath(face, shape);
                g.DrawPath(pen, shape);
            }
            TextRenderer.DrawText(g, _button.Text, _button.Font, r, Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        private static System.Drawing.Drawing2D.GraphicsPath Rounded(RectangleF r, float radius)
        {
            var d = radius * 2;
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
