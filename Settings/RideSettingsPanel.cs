namespace ErgoWattControl;

/// <summary>
/// The ride parameters of one run (weights, gearing, grade factor, resistances, power limit), editable in a start
/// dialog. Changes apply to that run only; settings.json stays as it is.
/// </summary>
public sealed class RideSettingsPanel : TableLayoutPanel
{
    private readonly NumericUpDown _rider = new() { Minimum = 30, Maximum = 250, Width = 70 };
    private readonly NumericUpDown _bike = new() { Minimum = 5, Maximum = 40, Width = 60 };
    private readonly NumericUpDown _gradeFactor = new() { Minimum = 10, Maximum = 100, Increment = 10, Width = 60 };
    private readonly ComboBox _gearing = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly NumericUpDown _startGear = new() { Minimum = RouteSession.MinGear, Maximum = RouteSession.MaxGear, Width = 55 };
    private readonly NumericUpDown _rolling = new() { Minimum = 0.001m, Maximum = 0.05m, Increment = 0.001m, DecimalPlaces = 3, Width = 70 };
    private readonly NumericUpDown _drag = new() { Minimum = 0.1m, Maximum = 1.5m, Increment = 0.05m, DecimalPlaces = 2, Width = 65 };
    private readonly NumericUpDown _maxWatt = new() { Minimum = ErgoController.MinWatt, Maximum = ErgoController.AbsoluteMaxWatt, Increment = 25, Width = 70 };
    private readonly Control[] _routeOnly;
    private bool _route;

    public RideSettingsPanel()
    {
        AutoSize = true;
        ColumnCount = 1;
        Margin = Padding.Empty;
        _gearing.Items.AddRange([RouteSession.GearingMtb, RouteSession.GearingRoad]);
        var weights = Row(Unit("Fahrer"), _rider, Unit("kg   Rad"), _bike, Unit("kg   Steigung"), _gradeFactor, Unit("%"));
        var gears = Row(Unit("Übersetzung"), _gearing, Unit("   Startgang"), _startGear);
        var resistances = Row(Unit("Rollwiderstand"), _rolling, Unit("   Luftwiderstand (cw·A)"), _drag, Unit("m²"));
        Controls.Add(weights);
        Controls.Add(gears);
        Controls.Add(resistances);
        Controls.Add(Row(Unit("Wattgrenze"), _maxWatt, Unit("W")));
        _routeOnly = [weights, gears, resistances];
    }

    /// <summary>
    /// Shows the values of <paramref name="settings"/>. The route values (weights, gearing, resistances) only matter when
    /// a route is simulated; when following a power profile only the power limit is shown.
    /// </summary>
    public void Show(AppSettings settings, bool route)
    {
        _rider.Value = (decimal)Math.Clamp(settings.RiderWeightKg, 30, 250);
        _bike.Value = (decimal)Math.Clamp(settings.BikeWeightKg, 5, 40);
        _gradeFactor.Value = Math.Clamp(settings.GradeFactorPercent, 10, 100);
        _gearing.SelectedItem = settings.Gearing == RouteSession.GearingRoad ? RouteSession.GearingRoad : RouteSession.GearingMtb;
        _startGear.Value = Math.Clamp(settings.StartGear, RouteSession.MinGear, RouteSession.MaxGear);
        _rolling.Value = Math.Clamp((decimal)settings.RollingResistance, _rolling.Minimum, _rolling.Maximum);
        _drag.Value = Math.Clamp((decimal)settings.DragAreaM2, _drag.Minimum, _drag.Maximum);
        _maxWatt.Value = Math.Clamp(settings.MaxWattLimit, ErgoController.MinWatt, ErgoController.AbsoluteMaxWatt);
        _route = route;
        foreach (var control in _routeOnly)
            control.Visible = route;
    }

    /// <summary>A copy of <paramref name="baseSettings"/> with the values of this panel; hidden route values are not taken over.</summary>
    public AppSettings Apply(AppSettings baseSettings)
    {
        var settings = baseSettings.Snapshot();
        settings.MaxWattLimit = (int)_maxWatt.Value;
        if (!_route)
            return settings;
        settings.RiderWeightKg = (double)_rider.Value;
        settings.BikeWeightKg = (double)_bike.Value;
        settings.GradeFactorPercent = (int)_gradeFactor.Value;
        settings.Gearing = (string)_gearing.SelectedItem!;
        settings.StartGear = (int)_startGear.Value;
        settings.RollingResistance = (double)_rolling.Value;
        settings.DragAreaM2 = (double)_drag.Value;
        return settings;
    }

    private static Label Unit(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 3, 4, 3) };

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        foreach (var c in controls)
            c.Anchor = AnchorStyles.Left;
        row.Controls.AddRange(controls);
        return row;
    }
}
