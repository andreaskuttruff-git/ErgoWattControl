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
    // Preview of the selected recording: power (and pulse) over time with duration and distance.
    private readonly WorkoutProfile _profile = new() { ShowPreview = false, Size = new Size(660, 190), Margin = new Padding(0, 4, 0, 0) };
    private readonly Label _facts = new() { AutoSize = true, MaximumSize = new Size(660, 0), Margin = new Padding(0, 6, 0, 0) };
    private readonly Button _export = new() { Text = "Exportieren", AutoSize = true, MinimumSize = new Size(110, 0) };

    private readonly Label _uploadCaption = new() { Text = "Danach hochladen bei:", AutoSize = true, Margin = new Padding(3, 9, 6, 3) };
    private readonly CheckBox _garmin = new() { Text = "Garmin Connect", AutoSize = true, Margin = new Padding(3, 8, 6, 3) };
    private readonly CheckBox _strava = new() { Text = "Strava", AutoSize = true, Margin = new Padding(3, 8, 24, 3) };
    private readonly Button _delete = new() { Text = "Löschen", AutoSize = true, MinimumSize = new Size(100, 0) };

    public ExportForm(AppSettings settings)
    {
        _settings = settings;
        Text = "Aufzeichnung exportieren";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1300, 680);
        Font = new Font("Segoe UI", 11);
        HelpWindow.Attach(this, "export");

        if (Directory.Exists(settings.RecordingDirectory))
            _files.AddRange(Directory.GetFiles(settings.RecordingDirectory, "*" + JsonRecorder.Extension).OrderByDescending(File.GetLastWriteTime));
        _list.Items.AddRange([.. _files.Select(Path.GetFileNameWithoutExtension).Cast<object>()]);
        _list.SelectedIndexChanged += (_, _) =>
        {
            UpdateButton();
            ShowPreview();
        };
        // Del removes the selected recordings (to the recycle bin, after asking).
        _list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                DeleteSelected();
            }
        };
        _delete.Click += (_, _) => DeleteSelected();
        // Full file name on hover, for names wider than the list.
        var tip = new ToolTip { InitialDelay = 400 };
        var tipIndex = -1;
        _list.MouseMove += (_, e) =>
        {
            var index = _list.IndexFromPoint(e.Location);
            if (index == tipIndex)
                return;
            tipIndex = index;
            tip.SetToolTip(_list, index >= 0 ? Path.GetFileName(_files[index]) : "");
        };
        _list.MouseLeave += (_, _) => tipIndex = -1;
        // The target folder is often longer than its box: the whole path on hover, kept up to date while typing.
        _folder.TextChanged += (_, _) => tip.SetToolTip(_folder, _folder.Text);
        FormClosed += (_, _) => tip.Dispose();
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
        right.Controls.Add(_profile);
        right.Controls.Add(_facts);
        right.Controls.Add(new Label { Text = "", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
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
        _garmin.Checked = settings.ExportOpenGarmin;
        _strava.Checked = settings.ExportOpenStrava;
        // Uploads take the FIT files only.
        // Only the platforms switched on in the settings are offered; with none, the whole "Danach hochladen bei" is hidden.
        _garmin.Visible = settings.GarminEnabled;
        _strava.Visible = settings.StravaEnabled;
        _uploadCaption.Visible = settings.GarminEnabled || settings.StravaEnabled;
        void EnableUpload() => _uploadCaption.Enabled = _garmin.Enabled = _strava.Enabled = _fit.Checked;
        _fit.CheckedChanged += (_, _) => EnableUpload();
        EnableUpload();
        var uploadTip = new ToolTip();
        FormClosed += (_, _) => uploadTip.Dispose();
        uploadTip.SetToolTip(_garmin, "Öffnet nach dem Export Garmin Connect in einem eigenen Fenster;\ndie FIT-Dateien werden auf der Import-Seite eingesetzt.\nBeim ersten Mal selbst anmelden – mit „Daten speichern“ geht es danach direkt zur Import-Seite.");
        uploadTip.SetToolTip(_strava, "Öffnet nach dem Export Strava in einem eigenen Fenster;\ndie FIT-Dateien werden auf der Upload-Seite eingesetzt.\nBeim ersten Mal selbst anmelden – mit „Eingeloggt bleiben“ geht es danach direkt zur Upload-Seite.");
        buttons.Controls.AddRange([close, _export, _strava, _garmin, _uploadCaption]);

        // List with the delete button below it.
        _delete.Margin = new Padding(0, 8, 0, 0);
        var listPanel = new TableLayoutPanel { Dock = DockStyle.Left, Width = 560, Padding = new Padding(12, 12, 0, 0), ColumnCount = 1, RowCount = 2 };
        listPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        listPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _list.Margin = Padding.Empty;
        listPanel.Controls.Add(_list, 0, 0);
        listPanel.Controls.Add(_delete, 0, 1);
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

    /// <summary>The item clicked last (with several selected: the one with the focus frame).</summary>
    private void ShowPreview()
    {
        const int LB_GETCARETINDEX = 0x019F;
        var caret = _list.IsHandleCreated ? (int)SendMessage(_list.Handle, LB_GETCARETINDEX, 0, 0) : -1;
        var index = _list.SelectedIndices.Count == 0 ? -1
            : _list.SelectedIndices.Contains(caret) ? caret : _list.SelectedIndices[0];
        if (index < 0)
        {
            _profile.SetWorkout(null, null, false);
            _facts.Text = "";
            return;
        }
        var file = _files[index];
        try
        {
            var workout = WorkoutRecording.Load(file);
            var recording = RecordingFile.Load(file);
            _profile.SetWorkout(workout, null, showPulse: true);
            var distanceColumn = Array.IndexOf(recording.Spalten, "Distanz_km");
            var km = distanceColumn >= 0 && recording.Werte.Count > 0 ? recording.Werte[^1][distanceColumn] : 0;
            _facts.Text = $"{recording.Art} vom {recording.Start:dd.MM.yyyy HH:mm} Uhr" + Environment.NewLine
                + $"Dauer {WorkoutProfile.Format(workout.Duration)}     Strecke {km:0.0} km     Ø {workout.AverageWatt:0} W     max. {workout.MaxWatt} W"
                + (workout.HasPulse ? $"     Puls Ø {workout.AveragePulse:0}" : "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException or System.Text.Json.JsonException)
        {
            _profile.SetWorkout(null, null, false);
            _facts.Text = $"Nicht lesbar: {ex.Message}";
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private void UpdateButton()
    {
        _export.Enabled = _list.SelectedIndices.Count > 0 && (_fit.Checked || _csv.Checked);
        _delete.Enabled = _list.SelectedIndices.Count > 0;
    }

    /// <summary>Moves the selected recordings to the recycle bin after a confirmation, so a slip can be undone there.</summary>
    /// <summary>
    /// Own confirmation instead of MessageBox: that one wraps the long file names. Grows with the longest name
    /// (up to the screen width); "Nein" is the default so a stray Enter deletes nothing.
    /// </summary>
    private bool ConfirmDelete(List<string> names)
    {
        var listing = string.Join(Environment.NewLine, names.Take(10)) + (names.Count > 10 ? $"{Environment.NewLine}… und {names.Count - 10} weitere" : "");
        using var dialog = new Form
        {
            Text = "Aufzeichnung löschen",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterParent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(20, 16, 20, 12),
            Font = Font,
        };
        var maxWidth = Screen.FromControl(this).WorkingArea.Width - 120;
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Text = names.Count == 1 ? "Diese Aufzeichnung in den Papierkorb verschieben?" : $"Diese {names.Count} Aufzeichnungen in den Papierkorb verschieben?",
        });
        layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(maxWidth, 0), Margin = new Padding(0, 10, 0, 0), Text = listing });
        layout.Controls.Add(new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 10, 0, 0), Text = "Aus dem Papierkorb lassen sie sich wiederherstellen." });
        var yes = new Button { Text = "Löschen", DialogResult = DialogResult.Yes, AutoSize = true, MinimumSize = new Size(100, 0) };
        var no = new Button { Text = "Abbrechen", DialogResult = DialogResult.No, AutoSize = true, MinimumSize = new Size(100, 0) };
        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 16, 0, 0) };
        row.Controls.AddRange([no, yes]);
        layout.Controls.Add(row);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = no;
        dialog.CancelButton = no;
        dialog.Shown += (_, _) => no.Focus();
        return dialog.ShowDialog(this) == DialogResult.Yes;
    }

    private void DeleteSelected()
    {
        var indices = _list.SelectedIndices.Cast<int>().OrderBy(i => i).ToList();
        if (indices.Count == 0)
            return;
        var names = indices.Select(i => Path.GetFileNameWithoutExtension(_files[i])).ToList();
        if (!ConfirmDelete(names))
            return;

        var failed = new List<string>();
        var deleted = 0;
        // From the end so the remaining indices stay valid.
        foreach (var index in Enumerable.Reverse(indices))
        {
            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(_files[index],
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                _files.RemoveAt(index);
                _list.Items.RemoveAt(index);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                failed.Add($"{Path.GetFileNameWithoutExtension(_files[index])}: {ex.Message}");
            }
        }

        _list.ClearSelected();
        if (_list.Items.Count > 0)
            _list.SelectedIndex = Math.Min(indices[0], _list.Items.Count - 1);
        else
            ShowPreview();
        UpdateButton();
        _status.ForeColor = failed.Count > 0 ? DialogColors.Warning : DialogColors.Ok;
        _status.Text = (deleted > 0 ? $"✓ {deleted} Aufzeichnung(en) in den Papierkorb verschoben" : "")
            + (failed.Count > 0 ? $"\nNicht gelöscht: {string.Join("; ", failed)}" : "");
    }

    private void Export()
    {
        var folder = _folder.Text.Trim();
        var written = new List<string>();
        var fitFiles = new List<string>();
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
                    fitFiles.Add(Path.Combine(folder, name + ".fit"));
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
        _settings.ExportOpenGarmin = _garmin.Checked;
        _settings.ExportOpenStrava = _strava.Checked;
        try { _settings.Save(); } catch (IOException) { } catch (UnauthorizedAccessException) { }

        _status.ForeColor = failed.Count > 0 ? DialogColors.Warning : DialogColors.Ok;
        _status.Text = (written.Count > 0 ? $"✓ {written.Count} Datei(en) geschrieben: {string.Join(", ", written)}" : "")
            + (failed.Count > 0 ? $"\nNicht exportiert: {string.Join("; ", failed)}" : "");
        // On to uploading: each ticked platform in its browser window, modal over this dialog, one after the other.
        // The export dialog stays open (closed only with "Schließen"); with export errors there is no upload, so
        // the message can be read.
        if (!_fit.Checked || fitFiles.Count == 0 || failed.Count > 0)
            return;
        if (_settings.GarminEnabled && _garmin.Checked)
            UploadWindow.Upload(UploadService.Garmin, _settings, fitFiles, this);
        if (_settings.StravaEnabled && _strava.Checked)
            UploadWindow.Upload(UploadService.Strava, _settings, fitFiles, this);
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
