namespace ErgoWattControl;

/// <summary>
/// "Training beenden" for a route, replay or program: the result as an aligned table (a MessageBox misaligns
/// tab-separated columns), the file name to keep or change, and in one dialog the choice: OK (keep), Verwerfen
/// (delete the recording) and – when the rider ends it himself – Weiterfahren (back to the ride, nothing ended).
/// </summary>
public sealed class SummaryForm : Form
{
    private readonly RecordingNameBox _fileName;

    /// <summary>The name the rider typed; null = keep the file name.</summary>
    public string? NewName => _fileName.CustomName;

    /// <param name="canResume">Ended by the rider during the ride: offers "Weiterfahren" (DialogResult.Cancel, also Esc and ✕).</param>
    public SummaryForm(string title, string heading, IReadOnlyList<(string Caption, string Value)> facts, string savedFile, bool canResume = false)
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
        HelpWindow.Attach(this, "beenden");

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        var header = new Label { Text = heading, AutoSize = true, Font = new Font("Segoe UI", 13, FontStyle.Bold), Margin = new Padding(0, 0, 0, 12) };
        layout.Controls.Add(header);
        layout.SetColumnSpan(header, 2);
        foreach (var (caption, value) in facts)
        {
            layout.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 3, 24, 3) });
            layout.Controls.Add(new Label { Text = value, AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), Margin = new Padding(0, 3, 0, 3) });
        }
        var hint = new Label
        {
            Text = canResume
                ? L.F("End_OkEndsTraining", Path.GetDirectoryName(savedFile))
                : L.F("End_SavedIn", Path.GetDirectoryName(savedFile)),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(620, 0),
            Margin = new Padding(0, 14, 0, 6),
        };
        layout.Controls.Add(hint);
        layout.SetColumnSpan(hint, 2);
        _fileName = new RecordingNameBox(Path.GetFileNameWithoutExtension(savedFile));
        layout.Controls.Add(new Label { Text = L.T("Pick_FileName"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 16, 2) });
        layout.Controls.Add(_fileName);

        var ok = new Button { Text = L.T("Button_Ok"), DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(90, 0) };
        var discard = new Button { Text = L.T("Button_Discard"), DialogResult = DialogResult.Abort, AutoSize = true, MinimumSize = new Size(90, 0) };
        var folder = new Button { Text = L.T("Button_OpenFolder"), AutoSize = true, Margin = new Padding(3, 3, 24, 3) };
        folder.Click += (_, _) =>
        {
            try { System.Diagnostics.Process.Start("explorer.exe", File.Exists(savedFile) ? $"/select,\"{savedFile}\"" : $"\"{Path.GetDirectoryName(savedFile)}\""); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.T("Caption_Folder"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 16, 0, 0) };
        buttons.Controls.Add(folder);
        if (canResume)
        {
            var resume = new Button { Text = L.T("Button_Resume"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(90, 0) };
            buttons.Controls.Add(resume);
            CancelButton = resume;
        }
        else
        {
            // Finished: Esc/✕ keep the recording like OK.
            CancelButton = ok;
        }
        buttons.Controls.AddRange([discard, ok]);
        layout.Controls.Add(buttons);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);
        // Focus on OK: Enter must close the window, not open Explorer via the first button.
        ActiveControl = ok;
        AcceptButton = ok;
    }
}
