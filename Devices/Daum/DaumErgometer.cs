using System.Collections.Concurrent;

namespace ErgoWattControl;

/// <summary>
/// The Daum ergo_bike cockpit over RS232 (<see cref="DaumCockpit"/>) as an <see cref="IErgometer"/>. A background
/// thread connects automatically, keeps retrying while the cockpit is off or unplugged, polls live data and restores
/// free training + target power after a cockpit reset. Events are raised on that thread.
/// </summary>
public sealed class DaumErgometer : IErgometer
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(400);
    /// <summary>A missing answer is asked again this often before the connection counts as lost.</summary>
    private const int QuickRetries = 2;

    /// <summary>When the last connection was lost (for the log line when it is back); null while connected or never connected.</summary>
    private DateTime? _lostAt;

    private readonly ConcurrentQueue<int> _wattRequests = new();
    private readonly ManualResetEventSlim _wake = new();
    private readonly string? _portName;
    private Thread? _thread;
    private volatile bool _running;
    private volatile int _targetWatt = 100;
    private volatile ErgometerCapabilities _capabilities = CapabilitiesFor(null, known: false);
    private string? _lastStatus;

    public event Action<ErgometerData>? DataReceived;
    public event Action<ErgometerStatus>? StatusChanged;

    public DaumErgometer(string? portName) => _portName = portName;

    public string DisplayName => $"Daum ({_portName ?? "kein Port"})";
    public string? PortName => _portName;
    public ErgometerCapabilities Capabilities => _capabilities;
    public bool IsConnected { get; private set; }
    public byte CockpitAddress { get; private set; }

    public void Start()
    {
        _running = true;
        _thread = new Thread(Run) { IsBackground = true, Name = "Cockpit" };
        _thread.Start();
    }

    public void SetTargetPower(int watt)
    {
        _targetWatt = watt;
        _wattRequests.Enqueue(watt);
    }

    /// <summary>Checks whether a cockpit answers on <paramref name="portName"/> without disturbing a running connection.</summary>
    /// <param name="running">The connection in use, if any: its own port is reported from its state instead of opened again.</param>
    public static string TestPort(string portName, DaumErgometer? running)
    {
        if (running is { IsConnected: true } && portName == running.PortName)
            return $"OK – Cockpit verbunden (Adresse {running.CockpitAddress})";
        try
        {
            using var cockpit = new DaumCockpit(portName);
            var address = cockpit.QueryAddress();
            var version = cockpit.CheckCockpit();
            return $"OK – Cockpit gefunden (Adresse {address}, Version {version})";
        }
        catch (UnauthorizedAccessException)
        {
            return portName == running?.PortName
                ? "Port wird gerade von der App für Verbindungsversuche benutzt."
                : "Port ist belegt (läuft noch ein anderes Programm?)";
        }
        catch (Exception ex)
        {
            return $"Fehler: {Describe(ex)}";
        }
    }

    /// <summary>What a cockpit of this type allows; the cadence window is the eddy-current brake's (<see cref="RpmRange"/>).</summary>
    private static ErgometerCapabilities CapabilitiesFor(byte? type, bool known)
    {
        var max = DaumCockpit.MaxWattForType(type);
        var info = type is { } t ? $"Cockpit {DaumCockpit.DescribeType(t)}: max. {max} W"
            : known ? $"Cockpit-Typ nicht abfragbar: max. {max} W"
            : $"Cockpit-Typ noch unbekannt, bis dahin max. {max} W";
        return new ErgometerCapabilities(max, 5, RpmRange.For, info);
    }

    private void Run()
    {
        while (_running)
        {
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
                Report($"Verbunden auf {port}", true, _capabilities.Info);
                if (_lostAt is { } lost)
                {
                    AppPaths.Note($"Cockpit auf {port} wieder verbunden nach {(DateTime.Now - lost).TotalSeconds:0} s");
                    _lostAt = null;
                }
                Poll(cockpit);
            }
            catch (Exception ex) when (_running)
            {
                // Only a connection that was up and broke is logged (not every attempt while the cockpit is off).
                if (IsConnected)
                {
                    _lostAt = DateTime.Now;
                    AppPaths.Log($"Cockpit-Verbindung auf {port} unterbrochen ({Describe(ex)})", ex);
                }
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
                    if (!_running)
                    {
                        try { cockpit.StopProgram(); } catch { /* best effort */ }
                    }
                    cockpit.Dispose();
                }
            }

            if (_running)
                _wake.Wait(RetryDelay);
        }
    }

    private void QueryCockpitType(DaumCockpit cockpit)
    {
        try
        {
            _capabilities = CapabilitiesFor(cockpit.GetCockpitType(), known: true);
        }
        catch (TimeoutException)
        {
            // Older cockpits don't know Get_Version: stay with the safe 400 W.
            _capabilities = CapabilitiesFor(null, known: true);
        }
        if (_targetWatt > _capabilities.MaxWatt)
            _targetWatt = _capabilities.MaxWatt;
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
        while (_running)
        {
            if (_wattRequests.TryDequeue(out var watt))
            {
                while (_wattRequests.TryDequeue(out var newer))
                    watt = newer; // only the latest pending value matters
                Retry(() => cockpit.SetWatt(watt));
            }

            var data = Retry(cockpit.ReadRunData);

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

            DataReceived?.Invoke(new ErgometerData
            {
                Watt = data.Watt,
                Rpm = data.Rpm,
                Pulse = data.Pulse,
                Pedaling = data.Pedaling,
                SpeedKmh = data.SpeedKmh,
                DistanceKm = data.DistanceKm,
                EnergyKJ = data.EnergyKJ,
                TimeSeconds = data.TimeSeconds,
            });
            _wake.Wait(PollDelay);
        }
    }

    /// <summary>
    /// A single missing answer (a disturbed byte, a short load peak on the PC) is asked again at once instead of
    /// tearing down the connection – that took several seconds without data. Only repeated failures count as a lost
    /// connection; each retry that rescued it is noted in Fehler.log.
    /// </summary>
    private T Retry<T>(Func<T> request)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var result = request();
                if (attempt > 0)
                    AppPaths.Note($"Cockpit: Antwort nach {attempt} Wiederholung(en) erhalten");
                return result;
            }
            catch (TimeoutException) when (attempt < QuickRetries && _running)
            {
                // try again right away; DaumCockpit discards stale input before each request
            }
        }
    }

    private void Retry(Action request) => Retry(() => { request(); return 0; });

    private void Report(string text, bool ok, string? detail = null)
    {
        if (text == _lastStatus)
            return;
        _lastStatus = text;
        StatusChanged?.Invoke(new ErgometerStatus(text, ok, detail));
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
