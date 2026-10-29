namespace ErgoWattControl;

/// <summary>Summary of a loaded GPX route with its elevation profile; the rider confirms with "Start".</summary>
public sealed class RouteOverviewForm : Form
{
    private const int PeakRpm = 60;

    public RouteOverviewForm(GpxRoute route, AppSettings settings, int maxWatt)
    {
        Text = $"Strecke fahren – {route.Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(860, 560);
        Font = new Font("Segoe UI", 11);

        var peak = RouteSession.PeakPower(route, settings, PeakRpm);
        var peakText = $"{peak.Watt:0} W  am steilsten Anstieg ({peak.GradePercent:0.0} %), " +
            $"im 1. Gang bei {PeakRpm} RPM ({peak.SpeedKmh:0} km/h) – schneller kostet mehr";
        var overLimit = peak.Watt > maxWatt;

        var facts = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12, 10, 12, 6) };
        void Fact(string caption, string value, Color? color = null)
        {
            facts.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 2, 16, 2) });
            facts.Controls.Add(new Label { Text = value, AutoSize = true, Margin = new Padding(0, 2, 0, 2), ForeColor = color ?? SystemColors.ControlText });
        }
        Fact("Strecke:", $"{route.TotalDistance / 1000:0.0} km");
        Fact("Höhenmeter bergauf:", $"{route.Ascent:0} m   (Höhe {route.MinElevation:0} – {route.MaxElevation:0} m)");
        Fact("Max. Steigung:", $"{route.MaxGrade * 100:0.0} %" +
            (settings.GradeFactorPercent < 100 ? $"   (gefahren mit {settings.GradeFactorPercent} %: {peak.GradePercent:0.0} %)" : ""));
        Fact("Max. Leistung:", overLimit ? $"{peakText}\nÜber deiner Wattgrenze von {maxWatt} W – dort wird auf {maxWatt} W begrenzt." : peakText,
            overLimit ? Color.Firebrick : null);
        Fact("Gewicht:", $"{settings.RiderWeightKg:0} kg Fahrer + {settings.BikeWeightKg:0} kg Rad");
        Fact("Einstellungen:", $"Steigungsfaktor {settings.GradeFactorPercent} %,  {settings.Gearing}-Übersetzung,  Startgang {settings.StartGear}");
        var note = new Label
        {
            Text = "Die Aufnahme beginnt mit dem ersten Tritt und pausiert, solange du nicht trittst.",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0),
        };
        facts.Controls.Add(note);
        facts.SetColumnSpan(note, 2);
        var profile = new RouteProfile { Dock = DockStyle.Fill, ShowPreview = false, Margin = new Padding(12) };
        profile.SetRoute(route);
        profile.SetHighlight(route.MaxGradeDistance,
            $"▲ {peak.GradePercent:0.0} %  ·  {peak.Watt:0} W  ·  km {route.MaxGradeDistance / 1000:0.0}",
            overLimit ? Color.OrangeRed : Color.Gold);

        var start = new Button { Text = "Start", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(100, 0) };
        var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(100, 0) };
        AcceptButton = start;
        CancelButton = cancel;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 48, Padding = new Padding(8) };
        buttons.Controls.AddRange([cancel, start]);

        var profilePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 0) };
        profilePanel.Controls.Add(profile);
        Controls.Add(profilePanel);
        Controls.Add(buttons);
        Controls.Add(facts);
    }
}
