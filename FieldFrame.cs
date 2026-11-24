using System.Drawing.Drawing2D;

namespace ErgoWattControl;

/// <summary>
/// A rounded frame around a text, number or list field, like the buttons have in the dark mode. Windows draws these
/// fields square and keeps redrawing their border, so the field loses its own border and sits inside this panel,
/// which draws the frame: grey, light while the field has the focus, dark while it is disabled. The panel takes the
/// field's place in the layout (size, margin, dock, anchor); the field itself stays as it is, so code using it
/// doesn't change. Use <see cref="Outer"/> where the layout is changed later (width, visibility).
/// </summary>
public sealed class FieldFrame : Panel
{
    /// <summary>Radius of the buttons in the dark mode, at 96 dpi.</summary>
    private const int Radius = 4;
    /// <summary>Space between the frame line and the field, at 96 dpi: keeps text and cursor off the line.</summary>
    private const int Inset = 3;
    /// <summary>The same for lists: the square selection bar must stay clear of the line and its rounded corners.</summary>
    private const int ListInset = 6;
    private static readonly Color BorderColor = Color.FromArgb(155, 155, 155);
    private static readonly Color FocusColor = Color.FromArgb(235, 235, 235);
    private static readonly Color DisabledColor = Color.FromArgb(80, 80, 80);

    public Control Field { get; }

    private FieldFrame(Control field)
    {
        Field = field;
        DoubleBuffered = true;
        ResizeRedraw = true;
        var inset = (field is ListBox ? ListInset : Inset) * DeviceDpi / 96;

        // The panel takes over the field's place; the field fills the panel inside the frame.
        Margin = field.Margin;
        Anchor = field.Anchor;
        Dock = field.Dock;
        Padding = new Padding(inset);
        RemoveBorder(field);
        Size = new Size(field.Width, field.Height + 2 * inset);
        field.Margin = Padding.Empty;
        field.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        field.Dock = DockStyle.Fill;
        Controls.Add(field);

        // Enter/Leave, not GotFocus: in a number field the focus is on its inner text part.
        field.Enter += (_, _) => Invalidate();
        field.Leave += (_, _) => Invalidate();
        field.EnabledChanged += (_, _) => Invalidate();
        field.BackColorChanged += (_, _) => Invalidate();
        // A click on the frame's edge goes to the field, as if it had hit it.
        MouseDown += (_, _) => field.Focus();
    }

    /// <summary>Puts <paramref name="field"/> into a rounded frame, if it is a field that gets one; returns what goes into the layout.</summary>
    public static Control Wrap(Control field) =>
        field.Parent is FieldFrame frame ? frame
        : Framable(field) ? new FieldFrame(field)
        : field;

    /// <summary>The frame of <paramref name="field"/>, or the field itself if it has none: what to size, move or hide.</summary>
    public static Control Outer(Control field) => field.Parent as FieldFrame ?? field;

    /// <summary>Text, number and list fields with a border. Drop-downs can't drop their border, so they stay square.</summary>
    internal static bool Framable(Control c) => c switch
    {
        TextBoxBase t => t.BorderStyle != BorderStyle.None,
        UpDownBase u => u.BorderStyle != BorderStyle.None,
        ListBox l => l.BorderStyle != BorderStyle.None,
        _ => false,
    };

    private static void RemoveBorder(Control field)
    {
        switch (field)
        {
            case TextBoxBase t: t.BorderStyle = BorderStyle.None; break;
            case UpDownBase u: u.BorderStyle = BorderStyle.None; break;
            case ListBox l: l.BorderStyle = BorderStyle.None; break;
        }
    }

    internal Color FrameColor => !Field.Enabled ? DisabledColor : Field.ContainsFocus ? FocusColor : BorderColor;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var radius = Radius * DeviceDpi / 96f;
        using var shape = RoundedRectangle(new RectangleF(0.5f, 0.5f, Width - 2, Height - 2), radius);
        using (var inside = new SolidBrush(Field.BackColor))
            g.FillPath(inside, shape);
        using var pen = new Pen(FrameColor);
        g.DrawPath(pen, shape);
    }

    internal static GraphicsPath RoundedRectangle(RectangleF r, float radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
