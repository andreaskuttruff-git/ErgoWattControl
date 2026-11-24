namespace ErgoWattControl;

/// <summary>
/// "Aufzeichnung exportieren": pick one or more recordings, tick the formats (FIT for Strava/Garmin Connect/
/// intervals.icu/TrainingPeaks, CSV for Excel) and a target folder. Format and folder are remembered.
/// </summary>
public sealed class ExportForm : Form
{
    private readonly AppSettings _settings;
    private readonly List<string> _files = [];
    private readonly SteadyListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true, SelectionMode = SelectionMode.MultiExtended };
    private readonly CheckBox _fit = new() { Text = L.T("Export_Fit"), AutoSize = true };
    private readonly CheckBox _csv = new() { Text = L.T("Export_Csv"), AutoSize = true };
    private readonly TextBox _folder = new() { Width = 400 };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(600, 0), Margin = new Padding(0, 12, 0, 0) };
    // Preview of the selected recording: power (and pulse) over time with duration and distance.
    private readonly WorkoutProfile _profile = new() { ShowPreview = false, Size = new Size(640, 190), Margin = new Padding(0, 4, 0, 0) };
    private readonly Label _facts = new() { AutoSize = true, MaximumSize = new Size(640, 0), Margin = new Padding(0, 6, 0, 0) };
    private readonly Button _export = new() { Text = L.T("Export_Button"), AutoSize = true, MinimumSize = new Size(110, 0) };

    private readonly Label _uploadCaption = new() { Text = L.T("Export_UploadTo"), AutoSize = true, Margin = new Padding(3, 9, 6, 3) };
    private readonly CheckBox _garmin = new() { Text = "Garmin Connect", AutoSize = true, Margin = new Padding(3, 8, 6, 3) };
    private readonly CheckBox _strava = new() { Text = "Strava", AutoSize = true, Margin = new Padding(3, 8, 24, 3) };
    private readonly Button _delete = new() { Text = L.T("Button_Delete"), AutoSize = true, MinimumSize = new Size(100, 0) };

    public ExportForm(AppSettings settings)
    {
        _settings = settings;
        Text = L.T("Export_Title");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        // Fits the rider's 1280-px monitor (dialogs open centred on the ride view), also with longer texts.
        ClientSize = new Size(1250, 680);
        Font = new Font("Segoe UI", 11);
        HelpWindow.Attach(this, "export");
        DisabledButtonFrame.Apply(this);

        // Also recordings that had to go to a fallback folder (chosen folder not writable), marked as such.
        _files.AddRange(settings.RecordingFiles("*" + JsonRecorder.Extension));
        _list.Items.AddRange([.. _files.Select(f => settings.InFallbackFolder(f)
            ? L.F("Export_FallbackMark", Path.GetFileNameWithoutExtension(f)) : Path.GetFileNameWithoutExtension(f)).Cast<object>()]);
        _list.SelectedIndexChanged += (_, _) => UpdateButton();
        ListBoxCapture.OnSelectionSettled(_list, ShowPreview);
        ListBoxCapture.ReleaseStuckCapture(_list);
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
            // A recording in a fallback folder shows where it lies.
            tip.SetToolTip(_list, index < 0 ? "" : settings.InFallbackFolder(_files[index]) ? _files[index] : Path.GetFileName(_files[index]));
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

        var browse = new Button { Text = L.T("Button_BrowseSpaced"), AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { InitialDirectory = Directory.Exists(_folder.Text) ? _folder.Text : settings.RecordingDirectory, UseDescriptionForTitle = true, Description = L.T("Export_TargetDialog") };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                _folder.Text = dialog.SelectedPath;
        };
        var open = new Button { Text = L.T("Button_OpenFolder"), AutoSize = true };
        open.Click += (_, _) => OpenFolder();
        var folderRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        folderRow.Controls.AddRange([FieldFrame.Wrap(_folder), browse, open]);
        foreach (Control c in folderRow.Controls)
            c.Anchor = AnchorStyles.Left;

        // Right padding so "Ordner öffnen" ends in line with "Schließen" in the button row below.
        var right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16, 12, 16, 0) };
        // The folder box takes what the two buttons leave, so they are never cut off (larger fonts, longer texts).
        right.Layout += (_, _) =>
        {
            var folder = FieldFrame.Outer(_folder);
            var width = right.ClientSize.Width - right.Padding.Horizontal - folder.Margin.Horizontal
                - browse.Width - browse.Margin.Horizontal - open.Width - open.Margin.Horizontal;
            if (width > 100 && folder.Width != width)
                folder.Width = width;
        };
        right.Controls.Add(_profile);
        right.Controls.Add(_facts);
        right.Controls.Add(new Label { Text = "", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
        right.Controls.Add(new Label { Text = L.T("Export_Formats"), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold) });
        right.Controls.Add(_fit);
        right.Controls.Add(_csv);
        right.Controls.Add(new Label
        {
            AutoSize = true, MaximumSize = new Size(600, 0), Margin = new Padding(0, 6, 0, 0), ForeColor = SystemColors.GrayText,
            Text = L.T("Export_FitNote"),
        });
        right.Controls.Add(new Label { Text = L.T("Export_TargetFolder"), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), Margin = new Padding(0, 16, 0, 4) });
        right.Controls.Add(folderRow);
        right.Controls.Add(_status);

        var close = new Button { Text = L.T("Button_Close"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(100, 0) };
        CancelButton = close;
        AcceptButton = _export;
        _export.Click += (_, _) => Export();
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 60, Padding = new Padding(8, 8, 8, 20) };
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
        uploadTip.SetToolTip(_garmin, L.T("Export_GarminTip"));
        uploadTip.SetToolTip(_strava, L.T("Export_StravaTip"));
        buttons.Controls.AddRange([close, _export, _strava, _garmin, _uploadCaption]);

        // List with the delete button below it.
        _delete.Margin = new Padding(0, 8, 0, 0);
        var listPanel = new TableLayoutPanel { Dock = DockStyle.Left, Width = 530, Padding = new Padding(12, 12, 0, 0), ColumnCount = 1, RowCount = 2 };
        listPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        listPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _list.Margin = Padding.Empty;
        listPanel.Controls.Add(FieldFrame.Wrap(_list), 0, 0);
        listPanel.Controls.Add(_delete, 0, 1);
        Controls.Add(right);
        Controls.Add(listPanel);
        Controls.Add(buttons);

        if (_files.Count == 0)
            _status.Text = L.F("Export_NoRecordings", settings.RecordingDirectory);
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
            var distanceColumn = recording.ColumnIndex(RecordingFile.Col.Distance);
            var km = distanceColumn >= 0 && recording.Values.Count > 0 ? recording.Values[^1][distanceColumn] : 0;
            _facts.Text = L.F("Export_Facts", KindName(recording.Kind), recording.Start) + Environment.NewLine
                + L.F("Export_FactsLine", WorkoutProfile.Format(workout.Duration), km, workout.AverageWatt, workout.MaxWatt)
                + (workout.HasPulse ? L.F("Pick_FactsPulse", workout.AveragePulse) : "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException or System.Text.Json.JsonException)
        {
            _profile.SetWorkout(null, null, false);
            _facts.Text = L.F("Export_NotReadable", ex.Message);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private static string KindName(string kind) => kind switch
    {
        RecordingFile.Kinds.Route => L.T("Kind_Route"),
        RecordingFile.Kinds.Program => L.T("Kind_Program"),
        _ => L.T("Kind_Training"),
    };

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
        var listing = string.Join(Environment.NewLine, names.Take(10)) + (names.Count > 10 ? Environment.NewLine + L.F("Delete_More", names.Count - 10) : "");
        using var dialog = new Form
        {
            Text = L.T("Delete_Title"),
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
            Text = names.Count == 1 ? L.T("Delete_One") : L.F("Delete_Many", names.Count),
        });
        layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(maxWidth, 0), Margin = new Padding(0, 10, 0, 0), Text = listing });
        layout.Controls.Add(new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 10, 0, 0), Text = L.T("Delete_Restore") });
        var yes = new Button { Text = L.T("Button_Delete"), DialogResult = DialogResult.Yes, AutoSize = true, MinimumSize = new Size(100, 0) };
        var no = new Button { Text = L.T("Button_Cancel"), DialogResult = DialogResult.No, AutoSize = true, MinimumSize = new Size(100, 0) };
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
        _status.Text = (deleted > 0 ? L.F("Delete_Done", deleted) : "")
            + (failed.Count > 0 ? L.F("Delete_Failed", string.Join("; ", failed)) : "");
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
            _status.Text = L.F("Export_TargetUnusable", ex.Message);
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
            // Any failure (also the FIT SDK's own FitException) is reported for this recording – with the exception
            // type, since some come without a message – and logged in full, so the others still get exported.
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                AppPaths.Log($"Export {source} → {folder}", ex);
                var reason = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : $"{ex.Message} ({ex.GetType().Name})";
                failed.Add($"{name}: {reason}");
            }
        }

        _settings.ExportFit = _fit.Checked;
        _settings.ExportCsv = _csv.Checked;
        _settings.ExportDirectory = folder;
        _settings.ExportOpenGarmin = _garmin.Checked;
        _settings.ExportOpenStrava = _strava.Checked;
        try { _settings.Save(); } catch (IOException) { } catch (UnauthorizedAccessException) { }

        _status.ForeColor = failed.Count > 0 ? DialogColors.Warning : DialogColors.Ok;
        _status.Text = (written.Count > 0 ? L.F("Export_Written", written.Count, string.Join(", ", written)) : "")
            + (failed.Count > 0 ? L.F("Export_Failed", string.Join("; ", failed)) : "");
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
            _status.Text = L.F("Pick_FolderNotOpened", ex.Message);
        }
    }
}
