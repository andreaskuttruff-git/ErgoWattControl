namespace ErgoWattControl;

/// <summary>Summary of a loaded GPX route with its elevation profile; the rider confirms with "Start".</summary>
public sealed class RouteOverviewForm : Form
{
    public RouteOverviewForm(GpxRoute route, AppSettings settings)
    {
        Text = $"Strecke fahren – {route.Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 470);
        Font = new Font("Segoe UI", 11);

        var facts = new Label
        {
            Dock = DockStyle.Top,
            Height = 130,
            Padding = new Padding(12, 10, 12, 0),
            Text =
                $"Strecke:\t\t{route.TotalDistance / 1000:0.0} km\n" +
                $"Höhenmeter bergauf:\t{route.Ascent:0} m   (Höhe {route.MinElevation:0} – {route.MaxElevation:0} m)\n" +
                $"Max. Steigung:\t\t{route.MaxGrade * 100:0.0} %\n" +
                $"Gewicht:\t\t{settings.RiderWeightKg:0} kg Fahrer + {settings.BikeWeightKg:0} kg Rad   " +
                $"Steigungsfaktor {settings.GradeFactorPercent} %   {settings.Gearing}, Startgang {settings.StartGear}\n" +
                "Die Aufnahme beginnt mit dem ersten Tritt und pausiert, solange du nicht trittst.",
        };
        var profile = new RouteProfile { Dock = DockStyle.Fill, ShowPreview = false, Margin = new Padding(12) };
        profile.SetRoute(route);

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
