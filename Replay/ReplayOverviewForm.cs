namespace ErgoWattControl;

/// <summary>Summary of a recorded training before replaying it; the rider confirms with "Start".</summary>
public sealed class ReplayOverviewForm : Form
{
    private readonly CheckBox _showPulse = new() { Text = "Puls vom Original während der Fahrt einblenden", AutoSize = true, Margin = new Padding(0, 8, 0, 0) };

    public bool ShowOriginalPulse => _showPulse.Checked;

    private readonly RecordingNameBox _fileName;

    /// <summary>File name the rider typed for the recording; null = use the configured pattern.</summary>
    public string? RecordingFileName => _fileName.CustomName;

    public ReplayOverviewForm(WorkoutRecording workout, bool showPulse, string defaultFileName)
    {
        _fileName = new RecordingNameBox(defaultFileName);
        Text = $"Training wiederholen – {workout.Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(860, 560);
        Font = new Font("Segoe UI", 11);

        var facts = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12, 10, 12, 6) };
        void Fact(string caption, string value)
        {
            facts.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 2, 16, 2) });
            facts.Controls.Add(new Label { Text = value, AutoSize = true, Margin = new Padding(0, 2, 0, 2) });
        }
        Fact("Dauer:", WorkoutProfile.Format(workout.Duration));
        Fact("Leistung:", $"Ø {workout.AverageWatt:0} W,  max. {workout.MaxWatt} W");
        Fact("Puls damals:", workout.HasPulse ? $"Ø {workout.AveragePulse:0}" : "keine Pulsdaten aufgezeichnet");
        facts.Controls.Add(new Label { Text = "Dateiname:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 16, 2) });
        facts.Controls.Add(_fileName);
        var note = new Label
        {
            Text = "Die Zeit läuft nur, solange du trittst. + / − und das Mausrad verschieben das ganze Profil um ±5 W.",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0),
        };
        facts.Controls.Add(note);
        facts.SetColumnSpan(note, 2);
        _showPulse.Checked = showPulse && workout.HasPulse;
        _showPulse.Enabled = workout.HasPulse;
        facts.Controls.Add(_showPulse);
        facts.SetColumnSpan(_showPulse, 2);

        var profile = new WorkoutProfile { Dock = DockStyle.Fill, ShowPreview = false };
        profile.SetWorkout(workout, null, _showPulse.Checked);
        _showPulse.CheckedChanged += (_, _) => profile.SetWorkout(workout, null, _showPulse.Checked);

        var start = new Button { Text = "Start", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(100, 0) };
        var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(100, 0) };
        AcceptButton = start;
        CancelButton = cancel;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 48, Padding = new Padding(8) };
        buttons.Controls.AddRange([cancel, start]);

        var profilePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 6, 12, 0) };
        profilePanel.Controls.Add(profile);
        Controls.Add(profilePanel);
        Controls.Add(buttons);
        Controls.Add(facts);
        ActiveControl = start;
    }
}
