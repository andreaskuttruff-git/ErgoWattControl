namespace ErgoWattControl;

/// <summary>
/// Start dialog for replaying: a list of training programs or recordings; the selected one shows its power profile,
/// key figures and description. "Start" (or a double click) replays it directly, so this is the only dialog before
/// the ride. The profile is drawn from the data (a recording optionally with the original pulse). The settings that
/// matter for the run are editable for it: the power limit, and for a recorded route – ridden as a route again – also
/// the ride values; a recording shows the values it was ridden with.
/// </summary>
public sealed class ReplayPickerForm : Form
{
    private sealed record Entry(string Title, string Path, Func<Selection> Load);

    /// <param name="Settings">Settings stored with a recording; null = none stored (programs, older CSV recordings).</param>
    /// <param name="Route">The route of a route recording, to ride it again.</param>
    private sealed record Selection(WorkoutRecording Workout, string Description, RideValues? Ride = null, GpxRoute? Route = null);

    private static readonly Color DarkBack = Color.FromArgb(24, 24, 24);

    private readonly List<Entry> _entries = [];
    private readonly SteadyListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
    private readonly WorkoutProfile _profile = new() { Dock = DockStyle.Fill, ShowPreview = false, Visible = false };
    private readonly RouteProfile _routeProfile = new() { Dock = DockStyle.Fill, ShowPreview = false, Visible = false };
    private readonly RideSettingsPanel _rideSettings = new() { Dock = DockStyle.Bottom, Padding = new Padding(0, 6, 0, 0) };
    private readonly Label _rideSettingsCaption = new() { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(0, 8, 0, 0), Font = new Font("Segoe UI", 11, FontStyle.Bold) };
    private readonly Label _facts = new() { Dock = DockStyle.Top, AutoSize = false, Height = 30, Padding = new Padding(0, 8, 0, 0), Font = new Font("Segoe UI", 11, FontStyle.Bold) };
    private readonly TextBox _description = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, WordWrap = true, ScrollBars = ScrollBars.Vertical,
        BorderStyle = BorderStyle.None, BackColor = SystemColors.Control, TabStop = false,
    };
    private readonly CheckBox _showPulse = new() { Text = L.T("Pick_ShowPulse"), AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(0, 6, 0, 0) };
    private readonly RecordingNameBox _fileName = new("");
    private readonly Button _start = new() { Text = L.T("Button_Start"), DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(100, 0), Enabled = false };
    private static string ProfileNote => L.T("Pick_ProfileNote");
    private static string RouteNote => L.T("Pick_RouteNote");
    private readonly Label _note = new() { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(0, 8, 0, 0), MaximumSize = new Size(780, 0) };
    private string _failed = "";
    private readonly Func<string, string> _defaultFileName;
    private readonly AppSettings _current;
    private readonly Action<RideValues> _saveAsDefault;
    private readonly bool _programs;

    /// <summary>The selected training as a replayable profile; set when the dialog ends with OK.</summary>
    public WorkoutRecording? Workout { get; private set; }

    /// <summary>The route of the selected route recording: ride it as a route instead of replaying the power.</summary>
    public GpxRoute? Route { get; private set; }

    /// <summary>Ride values for this run: the saved ones with the changes made in the dialog.</summary>
    public RideValues RideValues => _rideSettings.Apply(_current.Ride);

    /// <summary>File name (without .json) of the selected program, for the recording's name; null for a recording.</summary>
    public string? ProgramFile { get; private set; }

    /// <summary>File name the rider typed for the recording; null = use the configured pattern.</summary>
    public string? RecordingFileName => _fileName.CustomName;

    public bool ShowOriginalPulse => _showPulse.Checked;

    private const int OffsetStep = 5;
    private readonly NumericUpDown _offset = new() { Minimum = -1000, Maximum = 1000, Increment = OffsetStep, Width = 70, TextAlign = HorizontalAlignment.Right };
    private Control[] _offsetControls = [];

    /// <summary>
    /// Shift of the whole power profile for this run („Leistungsoffset“), in 5-W steps: the profile's lowest point
    /// stays at 0 W or above, its highest at the power limit or below. 0 for a route (it rides by gear).
    /// </summary>
    public int PowerOffset => Route is null ? (int)_offset.Value : 0;

    /// <summary>For the tests: the offset box and the power limit box of the dialog.</summary>
    internal NumericUpDown OffsetBox => _offset;
    internal NumericUpDown PowerLimitBox => _rideSettings.PowerLimitBox;

    /// <summary>For the tests: selects an entry like a click in the list.</summary>
    internal void Select(int index) => _list.SelectedIndex = index;

    /// <summary>What the offset may be for the selected profile and the power limit shown.</summary>
    internal static (int Min, int Max) OffsetRange(WorkoutRecording workout, int powerLimit)
    {
        var min = -(workout.MinWatt / OffsetStep * OffsetStep);
        var max = (int)Math.Floor((powerLimit - workout.MaxWatt) / (double)OffsetStep) * OffsetStep;
        return (min, Math.Max(min, max));
    }

    private void UpdateOffsetRange()
    {
        if (Workout is not { } workout)
            return;
        var (min, max) = OffsetRange(workout, _rideSettings.PowerLimit);
        _offset.Minimum = Math.Min(min, _offset.Minimum);
        _offset.Maximum = max;
        _offset.Minimum = min;
    }

    /// <summary>The preview of the profile, shifted by the offset chosen.</summary>
    private void ShowProfile()
    {
        if (Workout is not { } workout || Route is not null)
            return;
        var offset = PowerOffset;
        var limit = _rideSettings.PowerLimit;
        _profile.SetWorkout(workout, offset == 0 ? null : s => Math.Clamp(workout.WattAt(s) + offset, 0, limit), _showPulse.Checked);
    }

    /// <summary>The training programs (*.json) of <paramref name="directory"/>.</summary>
    /// <param name="current">The current settings: file name pattern and the power limit shown.</param>
    /// <param name="saveAsDefault">Stores the values shown as the new defaults in settings.json.</param>
    public static ReplayPickerForm ForPrograms(string directory, AppSettings current, Action<RideValues> saveAsDefault) =>
        ForPrograms(directory, null, current, saveAsDefault);

    /// <summary>
    /// The shipped programs of <paramref name="directory"/> and, listed first and marked with ★, the rider's own ones of
    /// <paramref name="customDirectory"/>. A faulty own program is named with the reason, so it can be fixed.
    /// </summary>
    public static ReplayPickerForm ForPrograms(string directory, string? customDirectory, AppSettings current, Action<RideValues> saveAsDefault)
    {
        var form = new ReplayPickerForm(L.T("Pick_ProgramsTitle"), programFile => AppSettings.BuildFileName(current.FileNameProgram, DateTime.Now, programFile),
            current, programs: true, showPulse: false, saveAsDefault);
        form._customDirectory = customDirectory;
        var failed = new List<string>();
        if (customDirectory is not null)
        {
            // Deleted while the app runs? Created again (with schema and notes) – or quietly skipped if that fails.
            ProgramFiles.EnsureCustomFolder(customDirectory);
            foreach (var file in ProgramFiles.ProgramsIn(customDirectory).Order(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var program = TrainingProgram.Load(file);
                    var text = program.Description.Text;
                    var description = (string.IsNullOrWhiteSpace(text) ? "" : text + "\n\n") + L.F("Pick_CustomProgram", file);
                    form._entries.Add(new Entry("★ " + program.DisplayName, file, () => new Selection(program.ToWorkout(), description)));
                }
                catch (Exception ex) when (IsReadError(ex))
                {
                    failed.Add($"{Path.GetFileName(file)} – {ex.Message}");
                }
            }
        }
        foreach (var file in ProgramFiles.ProgramsIn(directory).Order(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var program = TrainingProgram.Load(file);
                form._entries.Add(new Entry(program.DisplayName, file, () => new Selection(program.ToWorkout(), program.Description.Text)));
            }
            catch (Exception ex) when (IsReadError(ex))
            {
                failed.Add(Path.GetFileName(file));
            }
        }
        form.Fill(directory, L.T("Pick_Programs"), failed);
        return form;
    }

    /// <summary>Folder of the rider's own programs (programs dialog only); a button opens it.</summary>
    private string? _customDirectory;

    /// <summary>The recordings (*.json, older ones *.csv) of the recording folder, newest first.</summary>
    /// <param name="current">The current settings: file name patterns, and the ride values where a recording stored none.</param>
    /// <param name="saveAsDefault">Stores the ride values shown as the new defaults in settings.json.</param>
    public static ReplayPickerForm ForRecordings(AppSettings current, Action<RideValues> saveAsDefault)
    {
        var form = new ReplayPickerForm(L.T("Pick_RecordingsTitle"), name => AppSettings.BuildFileName(current.FileNameTraining, DateTime.Now, name),
            current, programs: false, current.ReplayShowOriginalPulse, saveAsDefault);
        var directory = current.RecordingDirectory;
        // Also recordings that had to go to a fallback folder (chosen folder not writable).
        foreach (var file in current.RecordingFiles("*.json", "*.csv", "*.fit"))
            form._entries.Add(RecordingEntry(file));
        form.Fill(directory, L.T("Pick_Recordings"), []);
        return form;
    }

    private static IEnumerable<string> Files(string directory, string pattern) =>
        Directory.Exists(directory) ? Directory.GetFiles(directory, pattern) : [];

    private static bool IsReadError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException or System.Text.Json.JsonException;

    private static Entry RecordingEntry(string file) => FitImport.IsFit(file) ? FitEntry(file) : new(Path.GetFileNameWithoutExtension(file), file, () =>
    {
        var workout = WorkoutRecording.Load(file);
        var recording = Path.GetExtension(file).Equals(JsonRecorder.Extension, StringComparison.OrdinalIgnoreCase) ? RecordingFile.Load(file) : null;
        var route = recording?.Target is { Route: { } points } target ? GpxRoute.FromPoints(target.Name, target.File, points) : null;
        if (route is not null && recording is not null)
        {
            // The pulse of the recorded ride by distance, for the green curve while riding the route again.
            int distance = recording.ColumnIndex(RecordingFile.Col.Distance), pulse = recording.ColumnIndex(RecordingFile.Col.Pulse);
            if (distance >= 0 && pulse >= 0)
                route.WithOriginalPulse(recording.Values.Where(v => v.Length > Math.Max(distance, pulse)).Select(v => (v[distance] * 1000, (int)v[pulse])));
        }
        var description = L.F("Pick_SavedAt", File.GetLastWriteTime(file)) + "\r\n"
            + (workout.HasPulse ? L.F("Pick_PulseThen", workout.AveragePulse) : L.T("Pick_NoPulseRecorded")) + "\r\n"
            + (route is not null
                ? L.F("Pick_AsRoute", route.Name, route.TotalDistance / 1000, route.Ascent)
                : L.T("Pick_FollowsPower")) + "\r\n"
            + L.F("Pick_Folder", Path.GetDirectoryName(file));
        return new Selection(workout, description, recording?.Settings?.Ride, route);
    });

    /// <summary>
    /// A FIT activity from another device or app: with power data its power profile is ridden again (with the pulse of
    /// that day), without power but with GPS and elevation it is ridden as a route.
    /// </summary>
    private static Entry FitEntry(string file) => new(FitImport.DisplayName(file), file, () =>
    {
        var fit = FitImport.Load(file);
        var when = L.F("Pick_FitRide", fit.Start, fit.Samples.Count) + "\r\n";
        if (fit.HasPower)
        {
            var workout = fit.ToWorkout();
            return new Selection(workout, when
                + (workout.HasPulse ? L.F("Pick_PulseThen", workout.AveragePulse) : L.T("Pick_NoPulse")) + "\r\n"
                + L.T("Pick_FollowsPower") + (fit.HasTrack ? L.T("Pick_FitAlsoTrack") : "") + "\r\n"
                + L.F("Pick_Folder", Path.GetDirectoryName(file)));
        }
        // No power (e.g. a ride without power meter): the track as a route; the pulse of that day comes along.
        var route = fit.ToRoute();
        var pulse = WorkoutRecording.FromRows(fit.Name, fit.Samples.Select(s => (s.Seconds, 0, s.Pulse ?? 0)), file);
        return new Selection(pulse, when
            + (pulse.HasPulse ? L.F("Pick_PulseThen", pulse.AveragePulse) : L.T("Pick_NoPulse")) + "\r\n"
            + L.F("Pick_FitNoPower", route.TotalDistance / 1000, route.Ascent) + "\r\n"
            + L.F("Pick_Folder", Path.GetDirectoryName(file)), Route: route);
    });

    private ReplayPickerForm(string title, Func<string, string> defaultFileName, AppSettings current, bool programs, bool showPulse,
        Action<RideValues> saveAsDefault)
    {
        _defaultFileName = defaultFileName;
        _current = current;
        _saveAsDefault = saveAsDefault;
        _programs = programs;
        HelpWindow.Attach(this, programs ? "programm" : "training");
        DisabledButtonFrame.Apply(this);
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1180, 800);
        Font = new Font("Segoe UI", 11);
        _showPulse.Checked = showPulse;
        _showPulse.Visible = !programs;
        _showPulse.CheckedChanged += (_, _) =>
        {
            if (Route is { } route)
                _routeProfile.SetRoute(route, _showPulse.Checked);
            else
                ShowProfile();
        };
        _offset.ValueChanged += (_, _) =>
        {
            // Typed values go to the 5-W steps.
            var stepped = Math.Clamp(Math.Round(_offset.Value / OffsetStep) * OffsetStep, _offset.Minimum, _offset.Maximum);
            if (stepped != _offset.Value)
                _offset.Value = stepped;
            else
                ShowProfile();
        };
        _rideSettings.PowerLimitChanged += (_, _) =>
        {
            UpdateOffsetRange();
            ShowProfile();
        };
        ListBoxCapture.OnSelectionSettled(_list, ShowSelected);
        ListBoxCapture.ReleaseStuckCapture(_list);
        _list.DoubleClick += (_, _) => { if (_start.Enabled) _start.PerformClick(); };
    }

    private void Fill(string directory, string what, List<string> failed)
    {
        var nameRow = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
        nameRow.Controls.Add(new Label { Text = L.T("Pick_FileName"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 0) });
        nameRow.Controls.Add(_fileName);
        _failed = failed.Count > 0 ? $"\n{L.T("Pick_NotReadable")}\n  {string.Join("\n  ", failed)}" : "";
        _note.Text = ProfileNote + _failed;
        var graph = new Panel { Dock = DockStyle.Top, Height = 300, BackColor = DarkBack };
        graph.Controls.Add(_profile);
        graph.Controls.Add(_routeProfile);
        var details = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 12, 0) };
        details.Controls.Add(_description);
        details.Controls.Add(_facts);
        details.Controls.Add(graph);
        details.Controls.Add(_showPulse);
        {
            // Docked to the bottom in this order: caption above the values; the button sits behind the "W" of the power limit.
            var saved = new Label { AutoSize = true, ForeColor = DialogColors.Ok, Margin = new Padding(8, 0, 0, 0) };
            var saveAsDefault = new Button { Text = L.T("Pick_SaveAsDefault"), AutoSize = true, Margin = new Padding(16, 0, 0, 0) };
            saveAsDefault.Click += (_, _) =>
            {
                _saveAsDefault(_rideSettings.Apply(_current.Ride));
                saved.Text = L.T("Pick_SavedAsDefault");
            };
            _list.SelectedIndexChanged += (_, _) => saved.Text = "";
            // The power offset: for this run only, so after "Standard" (which does not save it).
            var offsetLabel = new Label { Text = L.T("Pick_PowerOffset"), AutoSize = true, Margin = new Padding(24, 3, 4, 3) };
            var offsetUnit = new Label { Text = "W", AutoSize = true, Margin = new Padding(0, 3, 4, 3) };
            _offsetControls = [offsetLabel, _offset, offsetUnit];
            _rideSettings.AddBehindPowerLimit(saveAsDefault, offsetLabel, _offset, offsetUnit, saved);
            var tips = new ToolTip();
            tips.SetToolTip(saveAsDefault, L.T("Pick_SaveAsDefaultTip"));
            tips.SetToolTip(_offset, L.T("Pick_PowerOffsetTip"));
            tips.SetToolTip(offsetLabel, L.T("Pick_PowerOffsetTip"));
            details.Controls.Add(_rideSettingsCaption);
            details.Controls.Add(_rideSettings);
        }
        details.Controls.Add(_note);
        details.Controls.Add(nameRow);

        var cancel = new Button { Text = L.T("Button_Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(100, 0) };
        AcceptButton = _start;
        CancelButton = cancel;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 60, Padding = new Padding(8, 8, 8, 20) };
        buttons.Controls.AddRange([cancel, _start]);

        var listPanel = new Panel { Dock = DockStyle.Left, Width = 372, Padding = new Padding(12, 12, 0, 0) };
        listPanel.Controls.Add(_list);
        if (!_programs)
        {
            // Recordings may also lie elsewhere (an older folder, a USB stick).
            var other = new Button { Text = L.T("Pick_OtherFolder"), Dock = DockStyle.Bottom, Height = 36 };
            other.Click += (_, _) => PickOtherFile(directory);
            listPanel.Controls.Add(other);
        }
        else if (_customDirectory is { } custom)
        {
            // Own programs: open their folder (with the schema and an example to copy from).
            var own = new Button { Text = L.T("Pick_OpenCustomFolder"), Dock = DockStyle.Bottom, Height = 36 };
            own.Click += (_, _) =>
            {
                try
                {
                    Directory.CreateDirectory(custom);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(custom) { UseShellExecute = true });
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
                {
                    _note.Text = L.F("Pick_FolderNotOpened", ex.Message);
                }
            };
            new ToolTip().SetToolTip(own, L.F("Pick_CustomFolderTip", custom));
            listPanel.Controls.Add(own);
        }
        Controls.Add(details);
        Controls.Add(listPanel);
        Controls.Add(buttons);

        _list.Items.AddRange([.. _entries.Select(e => e.Title)]);
        // Until something is selected (or if nothing can be): the current settings.
        _rideSettings.Show(_current.Ride, route: false);
        _rideSettingsCaption.Text = L.T("Pick_RunSettings");
        if (_entries.Count == 0)
            _description.Text = L.F("Pick_NothingInFolder", directory, what);
        else
            _list.SelectedIndex = 0;
        ActiveControl = _list;
    }

    private void PickOtherFile(string directory)
    {
        using var picker = new OpenFileDialog
        {
            Title = L.T("Pick_ChooseTraining"),
            InitialDirectory = Directory.Exists(directory) ? directory : null,
            Filter = L.T("Pick_TrainingFilter"),
        };
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;
        var index = _entries.FindIndex(e => string.Equals(e.Path, picker.FileName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            _entries.Insert(0, RecordingEntry(picker.FileName));
            _list.Items.Insert(0, _entries[0].Title);
            index = 0;
        }
        _list.SelectedIndex = index;
    }

    private void ShowSelected()
    {
        if (_list.SelectedIndex < 0)
            return;
        var entry = _entries[_list.SelectedIndex];
        try
        {
            var selection = entry.Load();
            Workout = selection.Workout;
            Route = selection.Route;
            // A route recording is drawn as the route it will ride again.
            _routeProfile.Visible = Route is not null;
            _profile.Visible = Route is null;
            _routeProfile.SetRoute(Route, _showPulse.Checked);
            _note.Text = (Route is null ? ProfileNote : RouteNote) + _failed;
            _rideSettings.Show(selection.Ride ?? _current.Ride, route: Route is not null);
            // A route rides by gear: no power offset there.
            foreach (var control in _offsetControls)
                control.Visible = Route is null;
            UpdateOffsetRange();
            _profile.SetWorkout(Workout, null, _showPulse.Checked);
            ShowProfile();
            _rideSettingsCaption.Text = _programs || selection.Ride is null
                ? L.T("Pick_RunSettings")
                : L.T("Pick_RecordingSettings");
            _facts.Text = (Workout.MaxWatt == 0 && Route is { } r
                    ? L.F("Pick_FactsRoute", WorkoutProfile.Format(Workout.Duration), r.TotalDistance / 1000, r.Ascent)
                    : L.F("Pick_FactsPower", WorkoutProfile.Format(Workout.Duration), Workout.AverageWatt, Workout.MaxWatt))
                + (Workout.HasPulse ? L.F("Pick_FactsPulse", Workout.AveragePulse) : "");
            _description.Text = selection.Description.ReplaceLineEndings("\r\n");
            // Routes: the pulse of that ride by distance (FIT activity or route recording).
            _showPulse.Enabled = Route is null ? Workout.HasPulse : Route.OriginalPulse is not null;
            _start.Enabled = true;
        }
        catch (Exception ex) when (IsReadError(ex))
        {
            Workout = null;
            Route = null;
            _profile.SetWorkout(null, null, false);
            _routeProfile.SetRoute(null);
            _facts.Text = L.T("Pick_NotReadableShort");
            _description.Text = ex.Message;
            _start.Enabled = false;
        }
        var name = Path.GetFileNameWithoutExtension(entry.Path);
        ProgramFile = _programs ? name : null;
        _fileName.Reset(Route is not null
            ? AppSettings.BuildFileName(_current.FileNameRoute, DateTime.Now, Route.Name)
            : _defaultFileName(name));
    }
}
