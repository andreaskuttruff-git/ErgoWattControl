namespace ErgoWattControl;

/// <summary>
/// The app's single entry to the ergometer, independent of the kind of device: keeps the target power, applies the
/// user's power limit and the device's limits and step, and passes the device's data and status on. The device can be
/// swapped at any time (settings changed); the target power carries over. Events come on the device's thread.
/// </summary>
public sealed class ErgoController : IDisposable
{
    public const int MinWatt = 25;
    /// <summary>Highest limit a user can set; the device may allow less.</summary>
    public const int AbsoluteMaxWatt = 800;

    private IErgometer? _device;
    private volatile int _targetWatt = 100;
    private volatile int _userLimitWatt = AbsoluteMaxWatt;

    public event Action<ErgometerData>? DataReceived;
    /// <summary>Short status text, whether connected, and the technical details (null when there are none).</summary>
    public event Action<ErgometerStatus>? StatusChanged;

    public IErgometer? Device => _device;
    public int TargetWatt => _targetWatt;
    /// <summary>What the device can do; before a device is set, the safe defaults of an unknown Daum cockpit.</summary>
    public ErgometerCapabilities Capabilities => _device?.Capabilities ?? Unknown;
    /// <summary>Effective upper limit: the lower of what the device allows and the user's limit.</summary>
    public int MaxWatt => Math.Min(Capabilities.MaxWatt, _userLimitWatt);
    public bool IsConnected => _device?.IsConnected ?? false;

    private static readonly ErgometerCapabilities Unknown = new(400, 5, null, "Kein Gerät");

    /// <summary>Connects to <paramref name="device"/> (replacing and closing the previous one) and sends it the current target.</summary>
    public void Use(IErgometer device)
    {
        var old = _device;
        if (old is not null)
        {
            old.DataReceived -= OnData;
            old.StatusChanged -= OnStatus;
        }
        _device = device;
        device.DataReceived += OnData;
        device.StatusChanged += OnStatus;
        old?.Dispose();
        device.Start();
        SetTargetWatt(_targetWatt);
    }

    /// <summary>Sets the user's power limit; a target above it is lowered immediately.</summary>
    public void SetUserLimit(int watt)
    {
        _userLimitWatt = Math.Clamp(watt, MinWatt, AbsoluteMaxWatt);
        if (_targetWatt > MaxWatt)
            SetTargetWatt(MaxWatt);
    }

    /// <summary>Clamps to the allowed range in the device's steps and returns the value actually requested.</summary>
    public int SetTargetWatt(int watt)
    {
        var step = Math.Max(1, Capabilities.WattStep);
        watt = Math.Clamp((int)Math.Round(watt / (double)step) * step, MinWatt, MaxWatt);
        _targetWatt = watt;
        _device?.SetTargetPower(watt);
        return watt;
    }

    private void OnData(ErgometerData data) => DataReceived?.Invoke(data);

    private void OnStatus(ErgometerStatus status)
    {
        // Once connected the device may know a lower limit (e.g. the Daum cockpit type): keep the target within it.
        if (status.Connected && _targetWatt > MaxWatt)
            SetTargetWatt(MaxWatt);
        StatusChanged?.Invoke(status);
    }

    public void Dispose()
    {
        _device?.Dispose();
        _device = null;
    }
}
