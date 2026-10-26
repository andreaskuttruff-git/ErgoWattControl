namespace ErgoWattControl;

/// <summary>
/// Rides a GPX route virtually: cadence × gear gives a virtual speed that advances the position;
/// gradient, rider weight and speed give the power the cockpit must brake with.
/// </summary>
public sealed class RouteSession
{
    public const int MinGear = 1;
    public const int MaxGear = 28;

    private const double Gravity = 9.81;
    private const double AirDensity = 1.2;
    // The cockpit needs about this long to settle on a new power, so we look this far ahead and send at most this often.
    private static readonly TimeSpan ResponseTime = TimeSpan.FromSeconds(2);
    // Larger gaps (lost connection) are not extrapolated.
    private static readonly TimeSpan MaxStep = TimeSpan.FromSeconds(5);
    private const int WattStep = 5;

    private readonly double _mass;
    private readonly double _crr;
    private readonly double _cda;
    private readonly double _gradeFactor;
    private readonly string _gearing;
    private readonly int _minWatt;
    private readonly int _maxWatt;

    private DateTime? _lastUpdate;
    private DateTime _lastSend = DateTime.MinValue;
    private double _windowSum;
    private int _windowCount;
    private double _wattSum;
    private double _pulseSum;
    private int _wattSamples;
    private int _pulseSamples;

    public GpxRoute Route { get; }
    public int Gear { get; private set; }
    public double Position { get; private set; }
    public double SpeedKmh { get; private set; }
    /// <summary>Gradient in % as ridden (grade factor applied).</summary>
    public double GradePercent { get; private set; }
    public TimeSpan MovingTime { get; private set; }
    public int TargetWatt { get; private set; }
    public bool Finished => Position >= Route.TotalDistance;
    public double AverageWatt => _wattSamples == 0 ? 0 : _wattSum / _wattSamples;
    public double AveragePulse => _pulseSamples == 0 ? 0 : _pulseSum / _pulseSamples;

    public RouteSession(GpxRoute route, AppSettings settings, int minWatt, int maxWatt)
    {
        Route = route;
        _mass = settings.RiderWeightKg + settings.BikeWeightKg;
        _crr = settings.RollingResistance;
        _cda = settings.DragAreaM2;
        _gradeFactor = Math.Clamp(settings.GradeFactorPercent, 0, 100) / 100.0;
        _gearing = settings.Gearing;
        _minWatt = minWatt;
        _maxWatt = maxWatt;
        Gear = Math.Clamp(settings.StartGear, MinGear, MaxGear);
        GradePercent = route.GradeBetween(0, 20) * _gradeFactor * 100;
        TargetWatt = RoundToStep(PowerFor(GradePercent / 100, StandingReferenceSpeed()));
    }

    public const string GearingRoad = "Rennrad";
    public const string GearingMtb = "MTB";

    /// <summary>
    /// Distance per crank revolution. Road: the gear table in the 8008 TRS manual (p. 15), 3.7–9.3 m.
    /// MTB: same 28 linear steps but moderately lower, 2.0–7.4 m (≈ 32:42 – 32:11 on 29"), so steep climbs
    /// can be ridden at low cadence without exceeding the brake's power limit.
    /// </summary>
    public static double MetersPerRevolution(string gearing, int gear)
    {
        var (lowest, highest) = gearing == GearingRoad ? (3.675, 9.276) : (2.0, 7.4);
        return lowest + (gear - MinGear) * (highest - lowest) / (MaxGear - MinGear);
    }

    private double MetersPerRevolution() => MetersPerRevolution(_gearing, Gear);

    public void Shift(int delta) => Gear = Math.Clamp(Gear + delta, MinGear, MaxGear);

    /// <summary>Advances the ride by one cockpit sample. Returns a new target power when it should be sent, otherwise null.</summary>
    public int? Update(RunData d, DateTime now)
    {
        var dt = _lastUpdate is { } last && now - last <= MaxStep ? now - last : TimeSpan.Zero;
        _lastUpdate = now;

        var v = d.Rpm / 60.0 * MetersPerRevolution();
        SpeedKmh = v * 3.6;
        if (d.Rpm > 0 && !Finished)
        {
            Position = Math.Min(Route.TotalDistance, Position + v * dt.TotalSeconds);
            MovingTime += dt;
            _wattSum += d.Watt;
            _wattSamples++;
            if (d.Pulse > 0)
            {
                _pulseSum += d.Pulse;
                _pulseSamples++;
            }
        }

        // Grade where the rider will be when the cockpit has settled; standing still, prepare for setting off.
        var speedForPower = d.Rpm > 0 ? v : StandingReferenceSpeed();
        var lookahead = speedForPower * ResponseTime.TotalSeconds;
        var grade = Route.GradeBetween(Position, Position + Math.Max(lookahead, GpxRoute.Step)) * _gradeFactor;
        GradePercent = grade * 100;
        _windowSum += PowerFor(grade, speedForPower);
        _windowCount++;

        if (now - _lastSend < ResponseTime)
            return null;
        var average = _windowSum / _windowCount;
        _windowSum = 0;
        _windowCount = 0;
        _lastSend = now;

        // Hysteresis: small changes accumulate until they amount to a full cockpit step, without flickering at a boundary.
        var clamped = Math.Clamp(average, _minWatt, _maxWatt);
        if (Math.Abs(clamped - TargetWatt) < WattStep)
            return null;
        TargetWatt = RoundToStep(clamped);
        return TargetWatt;
    }

    private double StandingReferenceSpeed() => 60 / 60.0 * MetersPerRevolution();

    private double PowerFor(double grade, double v)
    {
        var angle = Math.Atan(grade);
        var force = _mass * Gravity * (Math.Sin(angle) + _crr * Math.Cos(angle)) + 0.5 * AirDensity * _cda * v * v;
        return force * v;
    }

    private int RoundToStep(double watt) =>
        Math.Clamp((int)Math.Round(watt / WattStep) * WattStep, _minWatt, _maxWatt);
}
