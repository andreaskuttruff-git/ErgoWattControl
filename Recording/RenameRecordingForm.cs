namespace ErgoWattControl;

/// <summary>
/// Shown after "Stop" in free training: the file is already saved, the rider may give it another name or discard it.
/// When closing the app during a recording it also offers "Weiterfahren", so a stray click on ✕ doesn't end the training.
/// </summary>
public sealed class RenameRecordingForm : Form
{
    private readonly RecordingNameBox _fileName;

    /// <summary>The name the rider typed; null = keep the file as it is.</summary>
    public string? NewName => _fileName.CustomName;

    /// <param name="closingApp">Called from ✕ while recording: adds "Weiterfahren" (DialogResult.Cancel, also the window's ✕ and Esc).</param>
    public RenameRecordingForm(string savedFile, bool closingApp = false)
    {
        _fileName = new RecordingNameBox(Path.GetFileNameWithoutExtension(savedFile));
        Text = closingApp ? "Training beenden" : "Aufnahme gespeichert";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);
        Font = new Font("Segoe UI", 11);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        var folder = new Label
        {
            Text = !closingApp ? $"Gespeichert in {Path.GetDirectoryName(savedFile)}"
                : File.Exists(savedFile) ? $"Die Aufnahme läuft noch. OK speichert sie in {Path.GetDirectoryName(savedFile)} und beendet die App."
                : "Noch nichts aufgezeichnet – die Aufnahme beginnt mit dem ersten Tritt. OK beendet die App.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(620, 0),
            Margin = new Padding(0, 0, 0, 12),
        };
        layout.Controls.Add(folder);
        layout.SetColumnSpan(folder, 2);
        layout.Controls.Add(new Label { Text = "Dateiname:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 16, 2) });
        layout.Controls.Add(_fileName);

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(90, 0) };
        var discard = new Button { Text = "Verwerfen", DialogResult = DialogResult.Abort, AutoSize = true, MinimumSize = new Size(90, 0) };
        var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 16, 0, 0) };
        if (closingApp)
        {
            var resume = new Button { Text = "Weiterfahren", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(90, 0) };
            buttons.Controls.Add(resume);
            CancelButton = resume;
        }
        buttons.Controls.AddRange([discard, ok]);
        layout.Controls.Add(buttons);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);
        AcceptButton = ok;
        ActiveControl = _fileName.Input;
        Shown += (_, _) => _fileName.Input.SelectAll();
    }

    /// <summary>After "Stop": asks for a new name; returns the path the recording ends up at, or null if it was discarded.</summary>
    public static string? Ask(IWin32Window owner, string savedFile)
    {
        using var dialog = new RenameRecordingForm(savedFile);
        var result = dialog.ShowDialog(owner);
        return Apply(owner, savedFile, result, dialog.NewName);
    }

    /// <summary>Carries out the choice: Abort deletes the file, OK with a new name renames it, anything else keeps it.</summary>
    public static string? Apply(IWin32Window owner, string savedFile, DialogResult result, string? newName)
    {
        if (result == DialogResult.Abort)
        {
            try
            {
                File.Delete(savedFile);
                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, $"Löschen nicht möglich, die Datei bleibt erhalten:\n{ex.Message}", "Aufnahme", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return savedFile;
            }
        }
        if (result != DialogResult.OK || newName is not { } name)
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
