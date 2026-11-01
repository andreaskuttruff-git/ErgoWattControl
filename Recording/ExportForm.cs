namespace ErgoWattControl;

/// <summary>
/// "Aufzeichnung exportieren": pick one or more recordings, tick the formats (FIT for Strava/Garmin Connect/
/// intervals.icu/TrainingPeaks, CSV for Excel) and a target folder. Format and folder are remembered.
/// </summary>
public sealed class ExportForm : Form
{
    private readonly AppSettings _settings;
    private readonly List<string> _files = [];
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true, SelectionMode = SelectionMode.MultiExtended };
    private readonly CheckBox _fit = new() { Text = "FIT – für Strava, Garmin Connect, intervals.icu, TrainingPeaks", AutoSize = true };
    private readonly CheckBox _csv = new() { Text = "CSV – für Excel (Semikolon, Dezimalkomma)", AutoSize = true };
    private readonly TextBox _folder = new() { Width = 400 };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(600, 0), Margin = new Padding(0, 12, 0, 0) };
    private readonly Button _export = new() { Text = "Exportieren", AutoSize = true, MinimumSize = new Size(110, 0) };

    public ExportForm(AppSettings settings)
    {
        _settings = settings;
        Text = "Aufzeichnung exportieren";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1120, 560);
        Font = new Font("Segoe UI", 11);
        HelpWindow.Attach(this, "export");

        if (Directory.Exists(settings.RecordingDirectory))
            _files.AddRange(Directory.GetFiles(settings.RecordingDirectory, "*" + JsonRecorder.Extension).OrderByDescending(File.GetLastWriteTime));
        _list.Items.AddRange([.. _files.Select(Path.GetFileNameWithoutExtension).Cast<object>()]);
        _list.SelectedIndexChanged += (_, _) => UpdateButton();
        _fit.Checked = settings.ExportFit;
        _csv.Checked = settings.ExportCsv;
        _fit.CheckedChanged += (_, _) => UpdateButton();
        _csv.CheckedChanged += (_, _) => UpdateButton();
        _folder.Text = settings.ExportDirectory ?? Path.Combine(settings.RecordingDirectory, "Export");

        var browse = new Button { Text = "Durchsuchen …", AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { InitialDirectory = Directory.Exists(_folder.Text) ? _folder.Text : settings.RecordingDirectory, UseDescriptionForTitle = true, Description = "Zielordner für den Export" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                _folder.Text = dialog.SelectedPath;
        };
        var open = new Button { Text = "Ordner öffnen", AutoSize = true };
        open.Click += (_, _) => OpenFolder();
        var folderRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        folderRow.Controls.AddRange([_folder, browse, open]);
        foreach (Control c in folderRow.Controls)
            c.Anchor = AnchorStyles.Left;

        var right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16, 12, 12, 0) };
        right.Controls.Add(new Label { Text = "Formate", AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold) });
        right.Controls.Add(_fit);
        right.Controls.Add(_csv);
        right.Controls.Add(new Label
        {
            AutoSize = true, MaximumSize = new Size(600, 0), Margin = new Padding(0, 6, 0, 0), ForeColor = SystemColors.GrayText,
            Text = "FIT: Eine Strecke wird als virtuelle Fahrt mit Karte exportiert, alle anderen Aufzeichnungen als Indoor-Training. " +
                   "Die Datei lässt sich z. B. bei Strava unter „Hochladen → Datei“ importieren.",
        });
        right.Controls.Add(new Label { Text = "Zielordner", AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), Margin = new Padding(0, 16, 0, 4) });
        right.Controls.Add(folderRow);
        right.Controls.Add(_status);

        var close = new Button { Text = "Schließen", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(100, 0) };
        CancelButton = close;
        AcceptButton = _export;
        _export.Click += (_, _) => Export();
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 48, Padding = new Padding(8) };
        buttons.Controls.AddRange([close, _export]);

        var hint = new Label { Dock = DockStyle.Bottom, AutoSize = true, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 6, 0, 0), Text = "Mehrere mit Strg- oder Umschalt-Klick" };
        var listPanel = new Panel { Dock = DockStyle.Left, Width = 400, Padding = new Padding(12, 12, 0, 0) };
        listPanel.Controls.Add(_list);
        listPanel.Controls.Add(hint);
        Controls.Add(right);
        Controls.Add(listPanel);
        Controls.Add(buttons);

        if (_files.Count == 0)
            _status.Text = $"Im Ordner {settings.RecordingDirectory} liegen keine Aufzeichnungen (*.json).";
        else
            _list.SelectedIndex = 0;
        UpdateButton();
        ActiveControl = _list;
    }

    private void UpdateButton() => _export.Enabled = _list.SelectedIndices.Count > 0 && (_fit.Checked || _csv.Checked);

    private void Export()
    {
        var folder = _folder.Text.Trim();
        var written = new List<string>();
        var failed = new List<string>();
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _status.ForeColor = DialogColors.Warning;
            _status.Text = $"Zielordner nicht verwendbar: {ex.Message}";
            return;
        }
        foreach (int index in _list.SelectedIndices)
        {
            var source = _files[index];
            var name = Path.GetFileNameWithoutExtension(source);
            try
            {
                var recording = RecordingFile.Load(source);
                if (_fit.Checked)
                {
                    RecordingExport.WriteFit(recording, Path.Combine(folder, name + ".fit"));
                    written.Add(name + ".fit");
                }
                if (_csv.Checked)
                {
                    RecordingExport.WriteCsv(recording, Path.Combine(folder, name + ".csv"));
                    written.Add(name + ".csv");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
            {
                failed.Add($"{name}: {ex.Message}");
            }
        }

        _settings.ExportFit = _fit.Checked;
        _settings.ExportCsv = _csv.Checked;
        _settings.ExportDirectory = folder;
        try { _settings.Save(); } catch (IOException) { } catch (UnauthorizedAccessException) { }

        _status.ForeColor = failed.Count > 0 ? DialogColors.Warning : DialogColors.Ok;
        _status.Text = (written.Count > 0 ? $"✓ {written.Count} Datei(en) geschrieben: {string.Join(", ", written)}" : "")
            + (failed.Count > 0 ? $"\nNicht exportiert: {string.Join("; ", failed)}" : "");
    }

    private void OpenFolder()
    {
        var folder = _folder.Text.Trim();
        try
        {
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _status.ForeColor = DialogColors.Warning;
            _status.Text = $"Ordner nicht zu öffnen: {ex.Message}";
        }
    }
}
