namespace ErgoWattControl;

/// <summary>
/// Rides a recorded training again: the recorded target power is set over time; the program clock only runs while
/// pedalling, and the rider can shift the whole profile up or down.
/// </summary>
public sealed class ReplaySession
{
    private const int WattStep = 5;
    private const int MaxOffset = 400;
    // Larger gaps (lost connection) are not counted as ridden time.
    private static readonly TimeSpan MaxStep = TimeSpan.FromSeconds(5);

    private readonly int _minWatt;
    private readonly int _maxWatt;
    private DateTime? _lastUpdate;
    private double _wattSum;
    private double _pulseSum;
    private int _wattSamples;
    private int _pulseSamples;

    public WorkoutRecording Workout { get; }
    /// <summary>Position in the recorded program.</summary>
    public TimeSpan Elapsed { get; private set; }
    /// <summary>Shift applied to the whole remaining profile, in W.</summary>
    public int Offset { get; private set; }
    public int TargetWatt { get; private set; }
    public bool Finished => Elapsed.TotalSeconds >= Workout.Duration;
    public double AverageWatt => _wattSamples == 0 ? 0 : _wattSum / _wattSamples;
    public double AveragePulse => _pulseSamples == 0 ? 0 : _pulseSum / _pulseSamples;

    public ReplaySession(WorkoutRecording workout, int minWatt, int maxWatt)
    {
        Workout = workout;
        _minWatt = minWatt;
        _maxWatt = maxWatt;
        TargetWatt = Compute();
    }

    /// <summary>Shifts the profile and returns the new target power.</summary>
    public int ShiftOffset(int delta)
    {
        Offset = Math.Clamp(Offset + delta, -MaxOffset, MaxOffset);
        TargetWatt = Compute();
        return TargetWatt;
    }

    /// <summary>Target power at a point of the program, with the rider's offset applied.</summary>
    public int WattAt(double seconds) => Math.Clamp(Workout.WattAt(seconds) + Offset, _minWatt, _maxWatt);

    /// <summary>Advances by one cockpit sample. Returns a new target power when it changed, otherwise null.</summary>
    public int? Update(ErgometerData d, DateTime now)
    {
        var dt = _lastUpdate is { } last && now - last <= MaxStep ? now - last : TimeSpan.Zero;
        _lastUpdate = now;
        if (d.Rpm > 0 && !Finished)
        {
            Elapsed = TimeSpan.FromSeconds(Math.Min(Workout.Duration, (Elapsed + dt).TotalSeconds));
            _wattSum += d.Watt;
            _wattSamples++;
            if (d.Pulse > 0)
            {
                _pulseSum += d.Pulse;
                _pulseSamples++;
            }
        }

        var watt = Compute();
        if (watt == TargetWatt)
            return null;
        TargetWatt = watt;
        return watt;
    }

    /// <summary>Time until the next change of the (shifted) profile and its value, or null if none follows.</summary>
    public (TimeSpan In, int Watt)? NextChange()
    {
        var position = Elapsed.TotalSeconds;
        while (Workout.NextChange(position) is { } change)
        {
            var watt = WattAt(change.Seconds);
            if (watt != TargetWatt)
                return (TimeSpan.FromSeconds(change.Seconds - Elapsed.TotalSeconds), watt);
            position = change.Seconds; // change hidden by clamping to the limits
        }
        return null;
    }

    private int Compute() => (int)Math.Round(WattAt(Elapsed.TotalSeconds) / (double)WattStep) * WattStep;
}
