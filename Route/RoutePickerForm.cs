namespace ErgoWattControl;

/// <summary>
/// Start dialog for "Strecke fahren": the GPX routes of the route folder; the selected one shows its elevation profile
/// with the steepest climb and its key figures. "Start" (or a double click) rides it directly.
/// </summary>
public sealed class RoutePickerForm : Form
{
    private const int PeakRpm = 60;

    private readonly AppSettings _settings;
    private readonly int _maxWatt;
    private readonly List<string> _files = [];
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
    private readonly RouteProfile _profile = new() { Dock = DockStyle.Fill, ShowPreview = false };
    private readonly TableLayoutPanel _facts = new() { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, Padding = new Padding(0, 10, 0, 0) };
    private readonly RecordingNameBox _fileName = new("");
    private readonly Button _start = new() { Text = "Start", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(100, 0), Enabled = false };

    /// <summary>The selected route; set when the dialog ends with OK.</summary>
    public GpxRoute? Route { get; private set; }

    /// <summary>File name the rider typed for the recording; null = use the configured pattern.</summary>
    public string? RecordingFileName => _fileName.CustomName;

    public RoutePickerForm(AppSettings settings, int maxWatt)
    {
        _settings = settings;
        _maxWatt = maxWatt;
        Text = "Strecke fahren";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1180, 680);
        Font = new Font("Segoe UI", 11);

        if (Directory.Exists(settings.GpxDirectory))
            _files.AddRange(Directory.GetFiles(settings.GpxDirectory, "*.gpx").Order(StringComparer.OrdinalIgnoreCase));
        _list.Items.AddRange([.. _files.Select(Path.GetFileNameWithoutExtension).Cast<object>()]);
        _list.SelectedIndexChanged += (_, _) => ShowSelected();
        _list.DoubleClick += (_, _) => { if (_start.Enabled) _start.PerformClick(); };

        var nameRow = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
        nameRow.Controls.Add(new Label { Text = "Dateiname:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 0) });
        nameRow.Controls.Add(_fileName);
        var note = new Label
        {
            Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(0, 8, 0, 0),
            Text = "Die Aufnahme beginnt mit dem ersten Tritt und pausiert, solange du nicht trittst.",
        };
        var graph = new Panel { Dock = DockStyle.Top, Height = 300 };
        graph.Controls.Add(_profile);
        var details = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 12, 0) };
        details.Controls.Add(_facts);
        details.Controls.Add(graph);
        details.Controls.Add(note);
        details.Controls.Add(nameRow);

        var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(100, 0) };
        AcceptButton = _start;
        CancelButton = cancel;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 48, Padding = new Padding(8) };
        buttons.Controls.AddRange([cancel, _start]);

        var other = new Button { Text = "Andere Datei …", Dock = DockStyle.Bottom, Height = 36 };
        other.Click += (_, _) => PickOtherFile();
        var listPanel = new Panel { Dock = DockStyle.Left, Width = 372, Padding = new Padding(12, 12, 0, 0) };
        listPanel.Controls.Add(_list);
        listPanel.Controls.Add(other);
        Controls.Add(details);
        Controls.Add(listPanel);
        Controls.Add(buttons);

        if (_files.Count == 0)
            Fact("", $"Im Ordner {settings.GpxDirectory} liegen keine GPX-Strecken.");
        else
            _list.SelectedIndex = 0;
        ActiveControl = _list;
    }

    private void PickOtherFile()
    {
        using var picker = new OpenFileDialog
        {
            Title = "Strecke auswählen",
            InitialDirectory = Directory.Exists(_settings.GpxDirectory) ? _settings.GpxDirectory : null,
            Filter = "GPX-Strecken (*.gpx)|*.gpx",
        };
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;
        var index = _files.FindIndex(f => string.Equals(f, picker.FileName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            _files.Insert(0, picker.FileName);
            _list.Items.Insert(0, Path.GetFileNameWithoutExtension(picker.FileName));
            index = 0;
        }
        _list.SelectedIndex = index;
    }

    private void Fact(string caption, string value, Color? color = null)
    {
        _facts.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 2, 16, 2) });
        _facts.Controls.Add(new Label { Text = value, AutoSize = true, MaximumSize = new Size(600, 0), Margin = new Padding(0, 2, 0, 2), ForeColor = color ?? SystemColors.ControlText });
    }

    private void ShowSelected()
    {
        if (_list.SelectedIndex < 0)
            return;
        var file = _files[_list.SelectedIndex];
        _facts.SuspendLayout();
        _facts.Controls.Clear();
        try
        {
            var route = GpxRoute.Load(file);
            Route = route;
            var peak = RouteSession.PeakPower(route, _settings, PeakRpm);
            var peakText = $"{peak.Watt:0} W  am steilsten Anstieg ({peak.GradePercent:0.0} %), " +
                $"im 1. Gang bei {PeakRpm} RPM ({peak.SpeedKmh:0} km/h) – schneller kostet mehr";
            var overLimit = peak.Watt > _maxWatt;
            Fact("Strecke:", $"{route.TotalDistance / 1000:0.0} km");
            Fact("Höhenmeter bergauf:", $"{route.Ascent:0} m   (Höhe {route.MinElevation:0} – {route.MaxElevation:0} m)");
            Fact("Max. Steigung:", $"{route.MaxGrade * 100:0.0} %" +
                (_settings.GradeFactorPercent < 100 ? $"   (gefahren mit {_settings.GradeFactorPercent} %: {peak.GradePercent:0.0} %)" : ""));
            Fact("Max. Leistung:", overLimit ? $"{peakText}\nÜber deiner Wattgrenze von {_maxWatt} W – dort wird auf {_maxWatt} W begrenzt." : peakText,
                overLimit ? DialogColors.Warning : null);
            Fact("Gewicht:", $"{_settings.RiderWeightKg:0} kg Fahrer + {_settings.BikeWeightKg:0} kg Rad");
            Fact("Einstellungen:", $"Steigungsfaktor {_settings.GradeFactorPercent} %,  {_settings.Gearing}-Übersetzung,  Startgang {_settings.StartGear}");
            _profile.SetRoute(route);
            _profile.SetHighlight(route.MaxGradeDistance,
                $"▲ {peak.GradePercent:0.0} %  ·  {peak.Watt:0} W  ·  km {route.MaxGradeDistance / 1000:0.0}",
                overLimit ? Color.OrangeRed : Color.Gold);
            _profile.Visible = true;
            _fileName.Reset(AppSettings.BuildFileName(_settings.FileNameRoute, DateTime.Now, route.Name));
            _start.Enabled = true;
        }
        catch (Exception ex) // any broken GPX: show why, keep the dialog usable
        {
            Route = null;
            _profile.Visible = false;
            Fact("Nicht lesbar:", string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message, DialogColors.Warning);
            _start.Enabled = false;
        }
        _facts.ResumeLayout();
    }
}
