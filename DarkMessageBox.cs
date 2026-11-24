namespace ErgoWattControl;

/// <summary>
/// A message box in the app's dark look: Windows' own MessageBox ignores the dark colour mode and stays white.
/// Same calls as <see cref="MessageBox.Show(IWin32Window, string, string, MessageBoxButtons, MessageBoxIcon)"/>.
/// </summary>
public sealed class DarkMessageBox : Form
{
    private static readonly Color FooterColor = Color.FromArgb(44, 44, 44);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        Show(null, text, caption, buttons, icon);

    public static DialogResult Show(IWin32Window? owner, string text, string caption,
        MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
    {
        using var box = new DarkMessageBox(text, caption, buttons, icon);
        if (owner is null)
            box.StartPosition = FormStartPosition.CenterScreen;
        return box.ShowDialog(owner);
    }

    private DarkMessageBox(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
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
        if (StockIcon(icon) is { } stock)
        {
            body.Controls.Add(new PictureBox
            {
                Image = SystemIcons.GetStockIcon(stock, 32).ToBitmap(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(32, 32),
                Margin = new Padding(0, 0, 14, 0),
            }, 0, 0);
        }
        body.Controls.Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(0, 6, 0, 0) }, 1, 0);

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
        Shown += (_, _) => (AcceptButton as Control)?.Focus();
    }

    private static DialogResult[] Results(MessageBoxButtons buttons) => buttons switch
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

    private static StockIconId? StockIcon(MessageBoxIcon icon) => icon switch
    {
        MessageBoxIcon.Question => StockIconId.Help,
        MessageBoxIcon.Warning => StockIconId.Warning,
        MessageBoxIcon.Error => StockIconId.Error,
        MessageBoxIcon.Information => StockIconId.Info,
        _ => null,
    };
}
