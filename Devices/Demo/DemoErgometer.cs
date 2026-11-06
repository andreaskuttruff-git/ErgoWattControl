namespace ErgoWattControl;

/// <summary>
/// A simulated ergometer for trying the app without the bike: a rider at about 75 RPM who holds the target power and
/// whose pulse follows it. Behaves like a Daum 8008 cockpit (800 W, its cadence window). Data every 1.3 s on a timer thread.
/// </summary>
public sealed class DemoErgometer : IErgometer
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(1300);

    private readonly Random _random = new(1);
    private readonly DateTime _start = DateTime.Now;
    private readonly object _gate = new();
    private System.Threading.Timer? _timer;
    private volatile int _targetWatt = 100;
    private double _pulse = 85;
    private double _energyKJ;
    private DateTime _last = DateTime.Now;

    public event Action<ErgometerData>? DataReceived;
    public event Action<ErgometerStatus>? StatusChanged;

    public string DisplayName => "Demo";
    public ErgometerCapabilities Capabilities { get; } =
        new(DaumCockpit.MaxWattForType(40), 5, RpmRange.For, "Demo (simulierter Fahrer, wie Cockpit 8008): max. 800 W");
    public bool IsConnected => _timer is not null;

    public void Start()
    {
        _timer = new System.Threading.Timer(_ => Tick(), null, Interval, Interval);
        StatusChanged?.Invoke(new ErgometerStatus("Demo-Modus (kein Cockpit)", true));
    }

    public void SetTargetPower(int watt) => _targetWatt = watt;

    private void Tick()
    {
        ErgometerData data;
        lock (_gate)
        {
            var now = DateTime.Now;
            var watt = _targetWatt;
            _energyKJ += watt * (now - _last).TotalSeconds / 1000;
            _last = now;
            var rpm = 75 + _random.Next(-4, 5);
            _pulse += (95 + watt * 0.3 - _pulse) * 0.05;
            data = new ErgometerData
            {
                Watt = watt,
                Rpm = rpm,
                Pedaling = true,
                SpeedKmh = rpm * 3 / 10,
                TimeSeconds = (int)(now - _start).TotalSeconds,
                Pulse = (int)_pulse,
                EnergyKJ = _energyKJ,
            };
        }
        DataReceived?.Invoke(data);
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
