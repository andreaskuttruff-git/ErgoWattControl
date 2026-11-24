using System.Media;

namespace ErgoWattControl;

/// <summary>
/// A message box in the app's dark look: Windows' own MessageBox ignores the dark colour mode and stays white.
/// Same calls as <see cref="MessageBox.Show(IWin32Window, string, string, MessageBoxButtons, MessageBoxIcon)"/>, and
/// like it: the first button is the default, Esc answers No/Cancel (or OK when that is the only button), and the
/// icon plays its Windows sound.
/// </summary>
public sealed class DarkMessageBox : Form
{
    private static readonly Color FooterColor = Color.FromArgb(44, 44, 44);
    /// <summary>Widest the text gets before it wraps, at 96 dpi.</summary>
    private const int MaxTextWidth = 560;

    private readonly Bitmap? _iconImage;
    private readonly SystemSound? _sound;

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        Show(null, text, caption, buttons, icon);

    public static DialogResult Show(IWin32Window? owner, string text, string caption,
        MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
    {
        using var box = new DarkMessageBox(text, caption, buttons, icon);
        if (owner is null)
        {
            // Without an owner it would not be in the taskbar and could end up behind other windows.
            box.StartPosition = FormStartPosition.CenterScreen;
            box.ShowInTaskbar = true;
        }
        return box.ShowDialog(owner);
    }

    internal DarkMessageBox(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        Text = caption;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 10);

        var body = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill, Padding = new Padding(20, 20, 24, 20) };
        if (Kind(icon) is { } kind)
        {
            // The text scales with the screen's DPI (font in points); the icon is taken in the matching size.
            var size = 32 * DeviceDpi / 96;
            using (var stockIcon = SystemIcons.GetStockIcon(kind.Icon, size))
                _iconImage = stockIcon.ToBitmap();
            _sound = kind.Sound;
            body.Controls.Add(new PictureBox
            {
                Image = _iconImage,
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(size, size),
                Margin = new Padding(0, 0, 14, 0),
            }, 0, 0);
        }
        body.Controls.Add(new Label
        {
            Text = Breakable(text),
            AutoSize = true,
            MaximumSize = new Size(MaxTextWidth * DeviceDpi / 96, 0),
            Margin = new Padding(0, 6, 0, 0),
        }, 1, 0);

        var footer = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = FooterColor,
            Padding = new Padding(16, 12, 16, 12),
            WrapContents = false,
        };
        var results = Results(buttons);
        // Right to left: the last button first, so they read in the usual order.
        foreach (var result in results.AsEnumerable().Reverse())
        {
            var button = new Button { Text = Caption(result), DialogResult = result, AutoSize = true, MinimumSize = new Size(90, 30), Margin = new Padding(8, 0, 0, 0) };
            footer.Controls.Add(button);
            if (result == results[0])
                AcceptButton = button;
            if (result is DialogResult.Cancel or DialogResult.No || results.Length == 1)
                CancelButton ??= button;
        }
        Controls.Add(body);
        Controls.Add(footer);
        MinimumSize = new Size(360, 0);
        Shown += (_, _) =>
        {
            (AcceptButton as Control)?.Focus();
            _sound?.Play();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _iconImage?.Dispose(); // a PictureBox doesn't dispose its image
        base.Dispose(disposing);
    }

    /// <summary>
    /// Lets the text wrap inside long paths and addresses too: a label only wraps at spaces, so an invisible break
    /// opportunity follows each \ and /.
    /// </summary>
    internal static string Breakable(string text) => text.Replace("\\", "\\​").Replace("/", "/​");

    internal static DialogResult[] Results(MessageBoxButtons buttons) => buttons switch
    {
        MessageBoxButtons.OKCancel => [DialogResult.OK, DialogResult.Cancel],
        MessageBoxButtons.YesNo => [DialogResult.Yes, DialogResult.No],
        MessageBoxButtons.YesNoCancel => [DialogResult.Yes, DialogResult.No, DialogResult.Cancel],
        MessageBoxButtons.RetryCancel => [DialogResult.Retry, DialogResult.Cancel],
        _ => [DialogResult.OK],
    };

    private static string Caption(DialogResult result) => result switch
    {
        DialogResult.Yes => L.T("Button_Yes"),
        DialogResult.No => L.T("Button_No"),
        DialogResult.Cancel => L.T("Button_Cancel"),
        DialogResult.Retry => L.T("Button_Retry"),
        _ => L.T("Button_Ok"),
    };

    /// <summary>The Windows icon and sound of a message kind; null for none.</summary>
    private static (StockIconId Icon, SystemSound Sound)? Kind(MessageBoxIcon icon) => icon switch
    {
        MessageBoxIcon.Question => (StockIconId.Help, SystemSounds.Question),
        MessageBoxIcon.Warning => (StockIconId.Warning, SystemSounds.Exclamation),
        MessageBoxIcon.Error => (StockIconId.Error, SystemSounds.Hand),
        MessageBoxIcon.Information => (StockIconId.Info, SystemSounds.Asterisk),
        _ => null,
    };
}
