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

    /// <summary>A gear table in a list: its id, shown under its name in the current language.</summary>
    public sealed record GearingItem(string Id)
    {
        public override string ToString() => RouteSession.GearingName(Id);
    }

    public RideSettingsPanel()
    {
        AutoSize = true;
        ColumnCount = 1;
        Margin = Padding.Empty;
        _gearing.Items.AddRange([.. RouteSession.Gearings.Select(g => new GearingItem(g))]);
        var weights = Row(Unit(L.T("Settings_Rider")), _rider, Unit(L.T("Settings_KgBike")), _bike, Unit(L.T("Settings_KgGrade")), _gradeFactor, Unit("%"));
        var gears = Row(Unit(L.T("Settings_Gearing")), _gearing, Unit(L.T("Settings_StartGear")), _startGear);
        var resistances = Row(Unit(L.T("RideValues_Rolling")), _rolling, Unit(L.T("RideValues_Drag")), _drag, Unit("m²"));
        Controls.Add(weights);
        Controls.Add(gears);
        Controls.Add(resistances);
        _powerLimitRow = Row(Unit(L.T("RideValues_PowerLimit")), _maxWatt, Unit("W"));
        Controls.Add(_powerLimitRow);
        _routeOnly = [weights, gears, resistances];
    }

    private readonly FlowLayoutPanel _powerLimitRow;

    /// <summary>The power limit shown (W).</summary>
    public int PowerLimit => (int)_maxWatt.Value;
    internal NumericUpDown PowerLimitBox => _maxWatt;

    /// <summary>Raised when the power limit is changed in the dialog or by <see cref="Show"/>.</summary>
    public event EventHandler? PowerLimitChanged
    {
        add => _maxWatt.ValueChanged += value;
        remove => _maxWatt.ValueChanged -= value;
    }

    /// <summary>Puts controls of the dialog (e.g. "Als Standard speichern") behind the "W" of the power limit, in the same row.</summary>
    public void AddBehindPowerLimit(params Control[] controls)
    {
        foreach (var c in controls)
            c.Anchor = AnchorStyles.Left;
        _powerLimitRow.Controls.AddRange(controls);
    }

    /// <summary>
    /// Shows <paramref name="ride"/>. The route values (weights, gearing, resistances) only matter when a route is
    /// simulated; when following a power profile only the power limit is shown.
    /// </summary>
    public void Show(RideValues ride, bool route)
    {
        _rider.Value = (decimal)Math.Clamp(ride.RiderWeightKg, 30, 250);
        _bike.Value = (decimal)Math.Clamp(ride.BikeWeightKg, 5, 40);
        _gradeFactor.Value = Math.Clamp(ride.GradeFactorPercent, 10, 100);
        _gearing.SelectedItem = new GearingItem(ride.Gearing == RouteSession.GearingRoad ? RouteSession.GearingRoad : RouteSession.GearingMtb);
        _startGear.Value = Math.Clamp(ride.StartGear, RouteSession.MinGear, RouteSession.MaxGear);
        _rolling.Value = Math.Clamp((decimal)ride.RollingResistance, _rolling.Minimum, _rolling.Maximum);
        _drag.Value = Math.Clamp((decimal)ride.DragAreaM2, _drag.Minimum, _drag.Maximum);
        _maxWatt.Value = Math.Clamp(ride.MaxWattLimit, ErgoController.MinWatt, ErgoController.AbsoluteMaxWatt);
        _route = route;
        foreach (var control in _routeOnly)
            control.Visible = route;
    }

    /// <summary>A copy of <paramref name="baseValues"/> with the values of this panel; hidden route values are not taken over.</summary>
    public RideValues Apply(RideValues baseValues)
    {
        var ride = baseValues.Copy();
        ride.MaxWattLimit = (int)_maxWatt.Value;
        if (!_route)
            return ride;
        ride.RiderWeightKg = (double)_rider.Value;
        ride.BikeWeightKg = (double)_bike.Value;
        ride.GradeFactorPercent = (int)_gradeFactor.Value;
        ride.Gearing = ((GearingItem)_gearing.SelectedItem!).Id;
        ride.StartGear = (int)_startGear.Value;
        ride.RollingResistance = (double)_rolling.Value;
        ride.DragAreaM2 = (double)_drag.Value;
        return ride;
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
