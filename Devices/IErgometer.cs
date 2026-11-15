namespace ErgoWattControl;

/// <summary>
/// An ergometer the app can control: it accepts a target power and reports live data. Each kind of device (the Daum
/// cockpit over RS232, the demo rider, later e.g. a Bluetooth trainer) implements this; the app talks to it only
/// through <see cref="ErgoController"/>. Implementations keep their own connection alive and may raise the events on
/// any thread.
/// <para>
/// The contract every device keeps (checked for each one by Tests\ErgometerContractTests.cs):
/// creating it touches no hardware – only <see cref="Start"/> connects, and it is called once;
/// <see cref="SetTargetPower"/> may come at any time, also before or between connections, and the device applies the
/// latest value as soon as it is connected; <see cref="StatusChanged"/> reports Connected = true once it is ready to
/// ride, and only then <see cref="IsConnected"/> is true; <see cref="Capabilities"/> is always valid (MaxWatt at least
/// <see cref="ErgoController.MinWatt"/>, WattStep at least 1); <see cref="IDisposable.Dispose"/> may be called without
/// Start and more than once, returns within a few seconds and no events follow afterwards.
/// Event handlers must not wait synchronously for another thread (e.g. Control.Invoke): a device may raise its events
/// while holding a lock that Dispose takes as well. The app's handlers only queue the reading (BeginInvoke).
/// </para>
/// </summary>
public interface IErgometer : IDisposable
{
    /// <summary>Short name for status texts, e.g. "Daum (COM1)".</summary>
    string DisplayName { get; }

    /// <summary>What the device can do; may change once connected (e.g. after the Daum cockpit type is known).</summary>
    ErgometerCapabilities Capabilities { get; }

    bool IsConnected { get; }

    event Action<ErgometerData>? DataReceived;
    event Action<ErgometerStatus>? StatusChanged;

    /// <summary>Starts connecting (and reconnecting after a loss) in the background.</summary>
    void Start();

    /// <summary>Power the brake should hold, already clamped to <see cref="Capabilities"/> by the controller.</summary>
    void SetTargetPower(int watt);
}

/// <summary>One reading of the device. Values a device doesn't measure are null; the app then works them out itself.</summary>
public sealed record ErgometerData
{
    public int Watt { get; init; }
    public int Rpm { get; init; }
    /// <summary>Heart rate; 0 = no reading.</summary>
    public int Pulse { get; init; }
    public bool Pedaling { get; init; }
    public double? SpeedKmh { get; init; }
    /// <summary>Running totals as the device counts them (may restart from 0 after a device reset).</summary>
    public double? DistanceKm { get; init; }
    public double? EnergyKJ { get; init; }
    public int? TimeSeconds { get; init; }
}

/// <param name="Text">Short status for the status line.</param>
/// <param name="Connected">Whether the device is ready to ride.</param>
/// <param name="Detail">Technical details (tooltip), null when there are none.</param>
public sealed record ErgometerStatus(string Text, bool Connected, string? Detail = null);

/// <param name="MaxWatt">Highest power the device accepts.</param>
/// <param name="WattStep">Power resolution in W.</param>
/// <param name="CadenceRange">Cadence window in which the brake can hold a power; null if the device has no such limit.</param>
/// <param name="Info">One line about the device for the settings, e.g. "Cockpit 8008: max. 800 W".</param>
public sealed record ErgometerCapabilities(int MaxWatt, int WattStep, Func<int, (int Min, int Max)>? CadenceRange, string Info);
