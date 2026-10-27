namespace ErgoWattControl;

/// <summary>Result of a ridden route as an aligned table (a MessageBox misaligns tab-separated columns).</summary>
public sealed class SummaryForm : Form
{
    public SummaryForm(string title, string heading, IReadOnlyList<(string Caption, string Value)> facts, string savedFile)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);
        Font = new Font("Segoe UI", 11);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        var header = new Label { Text = heading, AutoSize = true, Font = new Font("Segoe UI", 13, FontStyle.Bold), Margin = new Padding(0, 0, 0, 12) };
        layout.Controls.Add(header);
        layout.SetColumnSpan(header, 2);
        foreach (var (caption, value) in facts)
        {
            layout.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 3, 24, 3) });
            layout.Controls.Add(new Label { Text = value, AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), Margin = new Padding(0, 3, 0, 3) });
        }
        var saved = new Label { Text = $"Gespeichert: {Path.GetFileName(savedFile)}", AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 14, 0, 0) };
        layout.Controls.Add(saved);
        layout.SetColumnSpan(saved, 2);

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(90, 0) };
        var folder = new Button { Text = "Ordner öffnen", AutoSize = true };
        folder.Click += (_, _) =>
        {
            try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{savedFile}\""); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Ordner", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 16, 0, 0) };
        buttons.Controls.AddRange([folder, ok]);
        layout.Controls.Add(buttons);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);
        // Focus on OK: Enter must close the window, not open Explorer via the first button.
        ActiveControl = ok;
        AcceptButton = ok;
        CancelButton = ok;
    }
}
