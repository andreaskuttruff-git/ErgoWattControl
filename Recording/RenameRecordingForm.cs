namespace ErgoWattControl;

/// <summary>Shown after "Stop" in free training: the file is already saved, the rider may give it another name.</summary>
public sealed class RenameRecordingForm : Form
{
    private readonly RecordingNameBox _fileName;

    /// <summary>The name the rider typed; null = keep the file as it is.</summary>
    public string? NewName => _fileName.CustomName;

    public RenameRecordingForm(string savedFile)
    {
        _fileName = new RecordingNameBox(Path.GetFileNameWithoutExtension(savedFile));
        Text = "Aufnahme gespeichert";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);
        Font = new Font("Segoe UI", 11);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        var folder = new Label { Text = $"Gespeichert in {Path.GetDirectoryName(savedFile)}", AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(620, 0), Margin = new Padding(0, 0, 0, 12) };
        layout.Controls.Add(folder);
        layout.SetColumnSpan(folder, 2);
        layout.Controls.Add(new Label { Text = "Dateiname:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 16, 2) });
        layout.Controls.Add(_fileName);

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(90, 0) };
        var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 16, 0, 0) };
        buttons.Controls.Add(ok);
        layout.Controls.Add(buttons);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);
        AcceptButton = ok;
        ActiveControl = _fileName.Input;
        Shown += (_, _) => _fileName.Input.SelectAll();
    }

    /// <summary>Asks for a new name and renames the file; returns the path the recording ends up at.</summary>
    public static string Ask(IWin32Window owner, string savedFile)
    {
        using var dialog = new RenameRecordingForm(savedFile);
        if (dialog.ShowDialog(owner) != DialogResult.OK || dialog.NewName is not { } name)
            return savedFile;
        try
        {
            var target = CsvRecorder.UniquePath(Path.Combine(Path.GetDirectoryName(savedFile)!, name + ".csv"));
            File.Move(savedFile, target);
            return target;
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"Umbenennen nicht möglich, die Datei behält ihren Namen:\n{ex.Message}", "Aufnahme", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return savedFile;
        }
    }
}
