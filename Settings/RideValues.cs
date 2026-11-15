namespace ErgoWattControl;

/// <summary>
/// The values a ride is simulated with – weights, gearing, gradient, resistances and the power limit – apart from the
/// app's other settings (port, folders, file names, platforms). Stored in settings.json under "Ride", with every
/// recording, and changeable for one run in a start dialog without touching settings.json.
/// </summary>
public sealed class RideValues
{
    public double RiderWeightKg { get; set; } = 135;
    public double BikeWeightKg { get; set; } = 11;
    /// <summary>Percentage of the real gradient that is ridden (100 = original, 50 = half as steep).</summary>
    public int GradeFactorPercent { get; set; } = 100;
    /// <summary>Gear table for route riding: "Rennrad" (daum table, 42:24 – 53:12) or "MTB" (moderately lower gears).</summary>
    public string Gearing { get => _gearing; set => _gearing = value == "Rennrad" ? RouteSession.GearingRoad : value; }
    private string _gearing = RouteSession.GearingMtb;
    public int StartGear { get; set; } = 8;
    /// <summary>Physics constants, only editable in settings.json and the start dialogs: MTB tyres on mixed surface, upright rider.</summary>
    public double RollingResistance { get; set; } = 0.010;
    public double DragAreaM2 { get; set; } = 0.55;
    /// <summary>Upper power limit in W; the cockpit type may allow less (8008/8080: 800 W, others 400 W).</summary>
    public int MaxWattLimit { get; set; } = ErgoController.AbsoluteMaxWatt;

    public double TotalMassKg => RiderWeightKg + BikeWeightKg;

    /// <summary>An independent copy (changing it leaves this one as it is).</summary>
    public RideValues Copy() => (RideValues)MemberwiseClone();
}
