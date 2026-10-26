using System.Collections.Concurrent;

namespace ErgoWattControl;

/// <summary>
/// Owns the cockpit connection on a background thread: connects automatically, keeps retrying while the
/// cockpit is off or unplugged, polls live data and restores free training + target power after a cockpit reset.
/// Events are raised on that background thread.
/// </summary>
public sealed class ErgoController : IDisposable
{
    public const int MinWatt = 25;
    /// <summary>Highest limit a user can set; the cockpit type may allow less.</summary>
    public const int AbsoluteMaxWatt = 800;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(400);

    private readonly ConcurrentQueue<int> _wattRequests = new();
    private readonly ManualResetEventSlim _wake = new();
    private Thread? _thread;
    private volatile bool _running;
    private volatile bool _portChanged;
    private volatile string? _portName;
    private volatile int _targetWatt = 100;
    private string? _lastStatus;

    public event Action<RunData>? DataReceived;
    /// <summary>Short status text, whether connected, and the full technical error message (null when there is none).</summary>
    public event Action<string, bool, string?>? StatusChanged;

    private volatile int _hardwareMaxWatt = DaumCockpit.MaxWattForType(null);
    private volatile int _userLimitWatt = AbsoluteMaxWatt;

    public int TargetWatt => _targetWatt;
    /// <summary>Effective upper limit: the lower of what the cockpit type allows and the user's limit.</summary>
    public int MaxWatt => Math.Min(_hardwareMaxWatt, _userLimitWatt);
    public int HardwareMaxWatt => _hardwareMaxWatt;
    /// <summary>Cockpit type from Get_Version; null until known or if the cockpit does not answer the query.</summary>
    public byte? CockpitType { get; private set; }
    public bool IsConnected { get; private set; }
    public byte CockpitAddress { get; private set; }
    public string? PortName => _portName;

    public void Start(string? portName)
    {
        _portName = portName;
        _running = true;
        _thread = new Thread(Run) { IsBackground = true, Name = "Cockpit" };
        _thread.Start();
    }

    public void ChangePort(string? portName)
    {
        if (portName == _portName)
            return;
        _portName = portName;
        _portChanged = true;
        _wake.Set();
    }

    /// <summary>Sets the user's power limit; a target above it is lowered immediately.</summary>
    public void SetUserLimit(int watt)
    {
        _userLimitWatt = Math.Clamp(watt, MinWatt, AbsoluteMaxWatt);
        if (_targetWatt > MaxWatt)
            SetTargetWatt(MaxWatt);
    }

    /// <summary>Demo mode has no cockpit to ask, so it pretends to be the given type.</summary>
    public void AssumeCockpitType(byte type)
    {
        CockpitType = type;
        _hardwareMaxWatt = DaumCockpit.MaxWattForType(type);
    }

    /// <summary>Clamps to the allowed range in 5 W steps and returns the value actually requested.</summary>
    public int SetTargetWatt(int watt)
    {
        watt = Math.Clamp((int)Math.Round(watt / 5.0) * 5, MinWatt, MaxWatt);
        _targetWatt = watt;
        _wattRequests.Enqueue(watt);
        return watt;
    }

    /// <summary>Checks whether a cockpit answers on <paramref name="portName"/> without disturbing the running connection.</summary>
    public string TestPort(string portName)
    {
        if (IsConnected && portName == _portName)
            return $"OK – Cockpit verbunden (Adresse {CockpitAddress})";
        try
        {
            using var cockpit = new DaumCockpit(portName);
            var address = cockpit.QueryAddress();
            var version = cockpit.CheckCockpit();
            return $"OK – Cockpit gefunden (Adresse {address}, Version {version})";
        }
        catch (UnauthorizedAccessException)
        {
            return portName == _portName
                ? "Port wird gerade von der App für Verbindungsversuche benutzt."
                : "Port ist belegt (läuft noch ein anderes Programm?)";
        }
        catch (Exception ex)
        {
            return $"Fehler: {Describe(ex)}";
        }
    }

    private void Run()
    {
        while (_running)
        {
            _portChanged = false;
            _wake.Reset();
            var port = _portName;

            if (string.IsNullOrEmpty(port))
            {
                Report("Kein COM-Port eingestellt (☰ → Einstellungen)", false);
                _wake.Wait(RetryDelay);
                continue;
            }

            DaumCockpit? cockpit = null;
            try
            {
                Report($"Verbinde mit {port} …", false);
                cockpit = new DaumCockpit(port);
                cockpit.QueryAddress();
                QueryCockpitType(cockpit);
                InitializeCockpit(cockpit);
                CockpitAddress = cockpit.Address;
                IsConnected = true;
                var type = CockpitType is { } t ? $"Cockpit-Typ {DaumCockpit.DescribeType(t)}" : "Cockpit-Typ unbekannt";
                Report($"Verbunden auf {port}", true, $"{type}, Gerätegrenze {HardwareMaxWatt} W, eingestellte Grenze {_userLimitWatt} W");
                Poll(cockpit);
            }
            catch (Exception ex) when (_running)
            {
                Report($"{port}: {Describe(ex)} – neuer Versuch …", false, $"{ex.GetType().Name}: {ex.Message}");
            }
            catch
            {
                // Shutting down while a request was in flight.
            }
            finally
            {
                IsConnected = false;
                if (cockpit is not null)
                {
                    if (!_running || _portChanged)
                    {
                        try { cockpit.StopProgram(); } catch { /* best effort */ }
                    }
                    cockpit.Dispose();
                }
            }

            if (_running && !_portChanged)
                _wake.Wait(RetryDelay);
        }
    }

    private void QueryCockpitType(DaumCockpit cockpit)
    {
        try
        {
            AssumeCockpitType(cockpit.GetCockpitType());
        }
        catch (TimeoutException)
        {
            // Older cockpits don't know Get_Version: stay with the safe 400 W.
            CockpitType = null;
            _hardwareMaxWatt = DaumCockpit.MaxWattForType(null);
        }
        if (_targetWatt > MaxWatt)
            _targetWatt = MaxWatt;
    }

    private void InitializeCockpit(DaumCockpit cockpit)
    {
        cockpit.QueryAddress();
        cockpit.SelectFreeTraining();
        cockpit.StartProgram();
        cockpit.SetWatt(_targetWatt);
    }

    private void Poll(DaumCockpit cockpit)
    {
        var wattMismatchCount = 0;
        while (_running && !_portChanged)
        {
            if (_wattRequests.TryDequeue(out var watt))
            {
                while (_wattRequests.TryDequeue(out var newer))
                    watt = newer; // only the latest pending value matters
                cockpit.SetWatt(watt);
            }

            var data = cockpit.ReadRunData();

            // After a cockpit reset (e.g. static discharge) it may answer again but has left
            // free training or fallen back to its default power, so restore our state.
            if (data.Program != 0)
            {
                InitializeCockpit(cockpit);
                continue;
            }
            wattMismatchCount = data.Watt == _targetWatt ? 0 : wattMismatchCount + 1;
            if (wattMismatchCount >= 3)
            {
                cockpit.SetWatt(_targetWatt);
                wattMismatchCount = 0;
            }

            DataReceived?.Invoke(data);
            _wake.Wait(PollDelay);
        }
    }

    private void Report(string text, bool ok, string? detail = null)
    {
        if (text == _lastStatus)
            return;
        _lastStatus = text;
        StatusChanged?.Invoke(text, ok, detail);
    }

    private static string Describe(Exception ex) => ex switch
    {
        TimeoutException => "keine Antwort",
        UnauthorizedAccessException => "Port belegt",
        IOException => "Port nicht verfügbar",
        _ => ex.Message,
    };

    public void Dispose()
    {
        _running = false;
        _wake.Set();
        // Not disposing _wake: if the join times out mid-request, the thread may still touch it.
        _thread?.Join(5000);
    }
}
