namespace ErgoWattControl;

/// <summary>Stands in for the cockpit in demo mode: a rider at about 75 RPM whose pulse follows the power.</summary>
public sealed class DemoRider
{
    private readonly Random _random = new(1);
    private readonly DateTime _start = DateTime.Now;
    private double _pulse = 85;
    private double _energyKJ;
    private DateTime _last = DateTime.Now;

    public RunData Next(int targetWatt)
    {
        var now = DateTime.Now;
        _energyKJ += targetWatt * (now - _last).TotalSeconds / 1000;
        _last = now;
        var rpm = 75 + _random.Next(-4, 5);
        _pulse += (95 + targetWatt * 0.3 - _pulse) * 0.05;
        return new RunData
        {
            Program = 0,
            Pedaling = true,
            Watt = targetWatt,
            Rpm = rpm,
            SpeedKmh = rpm * 3 / 10,
            TimeSeconds = (int)(DateTime.Now - _start).TotalSeconds,
            Pulse = (int)_pulse,
            EnergyKJ = _energyKJ,
        };
    }
}
