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
    private sealed record Selection(WorkoutRecording Workout, string Description, AppSettings? Settings = null, GpxRoute? Route = null);

    private static readonly Color DarkBack = Color.FromArgb(24, 24, 24);

    private readonly List<Entry> _entries = [];
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
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
    private readonly CheckBox _showPulse = new() { Text = "Puls vom Original während der Fahrt einblenden", AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(0, 6, 0, 0) };
    private readonly RecordingNameBox _fileName = new("");
    private readonly Button _start = new() { Text = "Start", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(100, 0), Enabled = false };
    private const string ProfileNote = "Die Zeit läuft nur, solange du trittst. + / − und das Mausrad verschieben das ganze Profil um ±5 W.";
    private const string RouteNote = "Die Aufnahme beginnt mit dem ersten Tritt und pausiert, solange du nicht trittst.";
    private readonly Label _note = new() { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(0, 8, 0, 0), MaximumSize = new Size(780, 0) };
    private string _failed = "";
    private readonly Func<string, string> _defaultFileName;
    private readonly AppSettings _current;
    private readonly Action<AppSettings> _saveAsDefault;
    private readonly bool _programs;

    /// <summary>The selected training as a replayable profile; set when the dialog ends with OK.</summary>
    public WorkoutRecording? Workout { get; private set; }

    /// <summary>The route of the selected route recording: ride it as a route instead of replaying the power.</summary>
    public GpxRoute? Route { get; private set; }

    /// <summary>Settings for this run: the current settings with the values from the dialog.</summary>
    public AppSettings RideSettings => _rideSettings.Apply(_current);

    /// <summary>File name (without .json) of the selected program, for the recording's name; null for a recording.</summary>
    public string? ProgramFile { get; private set; }

    /// <summary>File name the rider typed for the recording; null = use the configured pattern.</summary>
    public string? RecordingFileName => _fileName.CustomName;

    public bool ShowOriginalPulse => _showPulse.Checked;

    /// <summary>The training programs (*.json) of <paramref name="directory"/>.</summary>
    /// <param name="current">The current settings: file name pattern and the power limit shown.</param>
    /// <param name="saveAsDefault">Stores the values shown as the new defaults in settings.json.</param>
    public static ReplayPickerForm ForPrograms(string directory, AppSettings current, Action<AppSettings> saveAsDefault)
    {
        var form = new ReplayPickerForm("Programm wiederholen", programFile => AppSettings.BuildFileName(current.FileNameProgram, DateTime.Now, programFile),
            current, programs: true, showPulse: false, saveAsDefault);
        var failed = new List<string>();
        foreach (var file in Files(directory, "*.json").Order(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var program = TrainingProgram.Load(file);
                form._entries.Add(new Entry(program.Name, file, () => new Selection(program.ToWorkout(), program.Beschreibung)));
            }
            catch (Exception ex) when (IsReadError(ex))
            {
                failed.Add(Path.GetFileName(file));
            }
        }
        form.Fill(directory, "Programme (*.json)", failed);
        return form;
    }

    /// <summary>The recordings (*.json, older ones *.csv) of the recording folder, newest first.</summary>
    /// <param name="current">The current settings: file name patterns, and the ride values where a recording stored none.</param>
    /// <param name="saveAsDefault">Stores the ride values shown as the new defaults in settings.json.</param>
    public static ReplayPickerForm ForRecordings(AppSettings current, Action<AppSettings> saveAsDefault)
    {
        var form = new ReplayPickerForm("Training wiederholen", name => AppSettings.BuildFileName(current.FileNameTraining, DateTime.Now, name),
            current, programs: false, current.ReplayShowOriginalPulse, saveAsDefault);
        var directory = current.RecordingDirectory;
        foreach (var file in Files(directory, "*.json").Concat(Files(directory, "*.csv")).OrderByDescending(File.GetLastWriteTime))
            form._entries.Add(RecordingEntry(file));
        form.Fill(directory, "Aufzeichnungen", []);
        return form;
    }

    private static IEnumerable<string> Files(string directory, string pattern) =>
        Directory.Exists(directory) ? Directory.GetFiles(directory, pattern) : [];

    private static bool IsReadError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException or System.Text.Json.JsonException;

    private static Entry RecordingEntry(string file) => new(Path.GetFileNameWithoutExtension(file), file, () =>
    {
        var workout = WorkoutRecording.Load(file);
        var recording = Path.GetExtension(file).Equals(JsonRecorder.Extension, StringComparison.OrdinalIgnoreCase) ? RecordingFile.Load(file) : null;
        var route = recording?.Vorgabe is { Strecke: { } points } target ? GpxRoute.FromPoints(target.Name, target.Datei, points) : null;
        var description = $"Gespeichert am {File.GetLastWriteTime(file):dd.MM.yyyy} um {File.GetLastWriteTime(file):HH:mm} Uhr\r\n"
            + (workout.HasPulse ? $"Puls damals: Ø {workout.AveragePulse:0}" : "Keine Pulsdaten aufgezeichnet") + "\r\n"
            + (route is not null
                ? $"Wird wieder als Strecke gefahren: {route.Name}, {route.TotalDistance / 1000:0.0} km, {route.Ascent:0} Hm bergauf"
                : "Folgt dem aufgezeichneten Leistungsverlauf") + "\r\n"
            + $"Ordner: {Path.GetDirectoryName(file)}";
        return new Selection(workout, description, recording?.Einstellungen, route);
    });

    private ReplayPickerForm(string title, Func<string, string> defaultFileName, AppSettings current, bool programs, bool showPulse,
        Action<AppSettings> saveAsDefault)
    {
        _defaultFileName = defaultFileName;
        _current = current;
        _saveAsDefault = saveAsDefault;
        _programs = programs;
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1180, 800);
        Font = new Font("Segoe UI", 11);
        _showPulse.Checked = showPulse;
        _showPulse.Visible = !programs;
        _showPulse.CheckedChanged += (_, _) => { if (Workout is { } workout) _profile.SetWorkout(workout, null, _showPulse.Checked); };
        _list.SelectedIndexChanged += (_, _) => ShowSelected();
        _list.DoubleClick += (_, _) => { if (_start.Enabled) _start.PerformClick(); };
    }

    private void Fill(string directory, string what, List<string> failed)
    {
        var nameRow = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
        nameRow.Controls.Add(new Label { Text = "Dateiname:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 0) });
        nameRow.Controls.Add(_fileName);
        _failed = failed.Count > 0 ? $"\nNicht lesbar: {string.Join(", ", failed)}" : "";
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
            // Docked to the bottom in this order: caption above the values, the button below them.
            var saved = new Label { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.ForestGreen, Margin = new Padding(8, 6, 0, 0) };
            var saveAsDefault = new Button { Text = "Als Standard speichern", AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
            saveAsDefault.Click += (_, _) =>
            {
                _saveAsDefault(_rideSettings.Apply(_current));
                saved.Text = "✓ in den Einstellungen gespeichert";
            };
            _list.SelectedIndexChanged += (_, _) => saved.Text = "";
            var defaultsRow = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = false };
            defaultsRow.Controls.AddRange([saveAsDefault, saved]);
            new ToolTip().SetToolTip(saveAsDefault, "Übernimmt diese Werte in die settings.json – sie gelten dann auch für neue Strecken und im Einstellungsdialog.");
            details.Controls.Add(_rideSettingsCaption);
            details.Controls.Add(_rideSettings);
            details.Controls.Add(defaultsRow);
        }
        details.Controls.Add(_note);
        details.Controls.Add(nameRow);

        var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(100, 0) };
        AcceptButton = _start;
        CancelButton = cancel;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 48, Padding = new Padding(8) };
        buttons.Controls.AddRange([cancel, _start]);

        var listPanel = new Panel { Dock = DockStyle.Left, Width = 372, Padding = new Padding(12, 12, 0, 0) };
        listPanel.Controls.Add(_list);
        if (!_programs)
        {
            // Recordings may also lie elsewhere (an older folder, a USB stick).
            var other = new Button { Text = "Andere Datei …", Dock = DockStyle.Bottom, Height = 36 };
            other.Click += (_, _) => PickOtherFile(directory);
            listPanel.Controls.Add(other);
        }
        Controls.Add(details);
        Controls.Add(listPanel);
        Controls.Add(buttons);

        _list.Items.AddRange([.. _entries.Select(e => e.Title)]);
        if (_entries.Count == 0)
            _description.Text = $"Im Ordner {directory} liegen keine {what}.";
        else
            _list.SelectedIndex = 0;
        ActiveControl = _list;
    }

    private void PickOtherFile(string directory)
    {
        using var picker = new OpenFileDialog
        {
            Title = "Gespeichertes Training auswählen",
            InitialDirectory = Directory.Exists(directory) ? directory : null,
            Filter = "Aufzeichnungen (*.json, *.csv)|*.json;*.csv",
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
            _profile.SetWorkout(Workout, null, _showPulse.Checked);
            _routeProfile.SetRoute(Route);
            _note.Text = (Route is null ? ProfileNote : RouteNote) + _failed;
            _rideSettings.Show(selection.Settings ?? _current, route: Route is not null);
            _rideSettingsCaption.Text = _programs || selection.Settings is null
                ? "Einstellungen – für diesen Lauf änderbar"
                : "Einstellungen der Aufzeichnung – für diesen Lauf änderbar";
            _facts.Text = $"Dauer {WorkoutProfile.Format(Workout.Duration)}     Ø {Workout.AverageWatt:0} W     max. {Workout.MaxWatt} W"
                + (Workout.HasPulse ? $"     Puls Ø {Workout.AveragePulse:0}" : "");
            _description.Text = selection.Description.ReplaceLineEndings("\r\n");
            _showPulse.Enabled = Workout.HasPulse && Route is null;
            _start.Enabled = true;
        }
        catch (Exception ex) when (IsReadError(ex))
        {
            Workout = null;
            Route = null;
            _profile.SetWorkout(null, null, false);
            _routeProfile.SetRoute(null);
            _facts.Text = "Nicht lesbar";
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
