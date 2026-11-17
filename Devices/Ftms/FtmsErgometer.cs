namespace ErgoWattControl;

/// <summary>
/// A Bluetooth smart trainer speaking the Fitness Machine Service (FTMS) as an <see cref="IErgometer"/> – first of all
/// the ErgoBridge (an ESP32 on the Daum cockpit's RS232 port), but any FTMS indoor bike with target power (ERG) works.
/// A background loop finds and connects the trainer, takes control, sends the target power and keeps reconnecting
/// after a loss. With an ErgoBridge, the bridge's status tells whether the Daum cockpit behind it answers; only then
/// the device counts as connected.
/// </summary>
public sealed class FtmsErgometer : IErgometer
{
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(3);
    /// <summary>No Indoor Bike Data for this long counts as a lost connection (Windows notices a drop only late).</summary>
    private static readonly TimeSpan SilenceLimit = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan LoopInterval = TimeSpan.FromMilliseconds(500);
    /// <summary>Highest power until the trainer told its range – the safe value, as for an unknown Daum cockpit.</summary>
    private const int UnknownMaxWatt = 400;

    private readonly IFtmsLink _link;
    private readonly TimeSpan _retryDelay;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cancel = new();
    private readonly SemaphoreSlim _wake = new(0);
    private Task? _loop;
    private bool _disposed;

    private volatile int _targetWatt = 100;
    private volatile ErgometerCapabilities _capabilities = new(UnknownMaxWatt, 5, null, L.F("Ftms_InfoUnknownYet", UnknownMaxWatt));
    private volatile bool _connected;
    private volatile bool _lost;
    private volatile bool _cockpitConnected = true;
    private volatile string _name = "";
    private bool _isBridge;
    private PowerRange? _range;
    private BridgeStatus? _bridgeStatus;
    private IndoorBikeData _merged = new();
    private long _lastDataTicks;
    private ErgometerStatus? _lastStatus;
    /// <summary>Set once a connection got control: only then a failure counts as a lost connection.</summary>
    private volatile bool _riding;
    /// <summary>Bridge status notifications so far: a status read only counts if none came in meanwhile.</summary>
    private int _statusNotifications;
    private readonly object _stateGate = new();
    /// <summary>When the last connection was lost (for the log line when it is back); null while connected.</summary>
    private DateTime? _lostAt;

    private readonly object _responseGate = new();
    private (byte OpCode, TaskCompletionSource<FtmsResult> Done)? _pending;

    public event Action<ErgometerData>? DataReceived;
    public event Action<ErgometerStatus>? StatusChanged;

    public FtmsErgometer() : this(new WinRtFtmsLink()) { }

    /// <summary>Over any link; unit tests pass a simulated trainer and a short retry delay.</summary>
    internal FtmsErgometer(IFtmsLink link, TimeSpan? retryDelay = null)
    {
        _link = link;
        _retryDelay = retryDelay ?? DefaultRetryDelay;
    }

    public string DisplayName => _name.Length == 0 ? L.T("Ftms_DisplayName") : L.F("Ftms_DisplayNameOf", _name);
    public ErgometerCapabilities Capabilities => _capabilities;
    public bool IsConnected => _connected;

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || _loop is not null)
                return;
            _loop = Task.Run(() => RunAsync(_cancel.Token));
        }
    }

    public void SetTargetPower(int watt)
    {
        _targetWatt = watt;
        Wake();
    }

    private void Wake()
    {
        try { _wake.Release(); }
        catch (ObjectDisposedException) { }
        catch (SemaphoreFullException) { }
    }

    private async Task RunAsync(CancellationToken cancel)
    {
        string? notedFailure = null;
        while (!cancel.IsCancellationRequested)
        {
            IFtmsConnection? connection = null;
            _riding = false;
            try
            {
                Report(L.T("Ftms_Searching"), false);
                connection = await _link.ConnectAsync(cancel);
                if (connection is null)
                {
                    Report(L.T("Ftms_NotFound"), false);
                    if (notedFailure != "not found")
                    {
                        notedFailure = "not found";
                        AppPaths.Note($"Bluetooth: no FTMS trainer found – retrying every {_retryDelay.TotalSeconds:0} s");
                    }
                }
                else
                {
                    await RideAsync(connection, cancel);
                }
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                var reason = ex is FtmsLinkException link ? link.LogText : $"{ex.GetType().Name}: {ex.Message}";
                if (_riding)
                {
                    // Had been riding: a lost connection, noted each time.
                    _lostAt ??= DateTime.Now;
                    notedFailure = null;
                    AppPaths.Note($"Bluetooth: connection to {_name} lost ({reason})");
                }
                else if (notedFailure != reason)
                {
                    // Not riding yet (refused, Bluetooth off, setup failed): noted once per reason, not every retry.
                    notedFailure = reason;
                    AppPaths.Note($"Bluetooth: {reason} – retrying every {_retryDelay.TotalSeconds:0} s");
                }
                Report(L.F("Ftms_Retrying", Describe(ex)), false, reason);
            }
            finally
            {
                _connected = false;
                Detach(connection);
                connection?.Dispose();
            }

            try { await Task.Delay(_retryDelay, cancel); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RideAsync(IFtmsConnection connection, CancellationToken cancel)
    {
        _name = connection.Name;
        _isBridge = connection.IsErgoBridge;
        _lost = false;
        _merged = new IndoorBikeData();
        lock (_stateGate)
        {
            _range = null;
            _bridgeStatus = null;
        }
        _cockpitConnected = true;
        connection.IndoorBikeData += OnIndoorBikeData;
        connection.ControlPointResponse += OnControlPointResponse;
        connection.BridgeStatus += OnBridgeStatus;
        connection.Lost += OnLost;

        var range = FtmsProtocol.ParsePowerRange(await connection.ReadPowerRangeAsync(cancel) ?? []);
        lock (_stateGate)
            _range = range;
        if (_isBridge)
        {
            var notificationsBefore = Volatile.Read(ref _statusNotifications);
            var read = FtmsProtocol.ParseBridgeStatus(await connection.ReadBridgeStatusAsync(cancel) ?? []);
            lock (_stateGate)
            {
                // A notification that arrived meanwhile is newer than what the read returned.
                if (read is not null && _statusNotifications == notificationsBefore)
                    ApplyBridgeStatus(read);
            }
        }
        lock (_stateGate)
            UpdateCapabilities();

        var control = await CommandAsync(connection, FtmsProtocol.RequestControl(), cancel);
        if (control != FtmsResult.Success)
            throw ControlRefused();
        await CommandAsync(connection, FtmsProtocol.StartOrResume(), cancel); // trainers that don't need it may refuse

        Interlocked.Exchange(ref _lastDataTicks, DateTime.UtcNow.Ticks);
        _riding = true;
        AppPaths.Note(_lostAt is { } since
            ? $"Bluetooth: {_name} reconnected after {(DateTime.Now - since).TotalSeconds:0} s ({_capabilities.Info})"
            : $"Bluetooth: {_name} connected ({_capabilities.Info})");
        _lostAt = null;

        int? sent = null;
        while (true)
        {
            cancel.ThrowIfCancellationRequested();
            if (_lost)
                throw new IOException("Bluetooth connection lost");
            // The ErgoBridge notifies every second, cockpit or not; other trainers may pause while the flywheel stands.
            if (_isBridge && DateTime.UtcNow - new DateTime(Interlocked.Read(ref _lastDataTicks), DateTimeKind.Utc) > SilenceLimit)
                throw new TimeoutException("no data from the trainer");

            var ready = !_isBridge || _cockpitConnected;
            var text = ready ? L.F("Ftms_Connected", _name) : L.F("Ftms_CockpitMissing", _name);
            // Status before the flag when connecting, the other way round when not: whoever sees IsConnected also
            // sees the matching status.
            if (ready)
            {
                Report(text, true, _capabilities.Info);
                _connected = true;
            }
            else
            {
                _connected = false;
                Report(text, false, _capabilities.Info);
            }

            var target = Math.Clamp(_targetWatt, ErgoController.MinWatt, _capabilities.MaxWatt);
            if (sent != target)
            {
                var result = await CommandAsync(connection, FtmsProtocol.SetTargetPower(target), cancel);
                if (result == FtmsResult.Success)
                    sent = target;
                else if (result == FtmsResult.ControlNotPermitted)
                {
                    // Control got lost (e.g. the trainer was reset): ask again, send the target on the next round.
                    AppPaths.Note($"Bluetooth: {_name} refused the target power – requesting control again");
                    if (await CommandAsync(connection, FtmsProtocol.RequestControl(), cancel) != FtmsResult.Success)
                        throw ControlRefused();
                }
                else
                {
                    AppPaths.Note($"Bluetooth: {_name} answered Set Target Power {target} W with {result}");
                    sent = target; // don't hammer a trainer that rejects the value
                }
            }
            await _wake.WaitAsync(LoopInterval, cancel);
        }
    }

    private FtmsLinkException ControlRefused() =>
        new(L.F("Ftms_ControlRefused", _name), $"{_name} refuses control (another app, or the previous connection is still open)");

    /// <summary>Writes a control point request and waits for its indication.</summary>
    private async Task<FtmsResult> CommandAsync(IFtmsConnection connection, byte[] request, CancellationToken cancel)
    {
        var done = new TaskCompletionSource<FtmsResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_responseGate)
            _pending = (request[0], done);
        try
        {
            await connection.WriteControlPointAsync(request, cancel);
            return await done.Task.WaitAsync(ResponseTimeout, cancel);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"no answer to control point request 0x{request[0]:X2}");
        }
        finally
        {
            lock (_responseGate)
                _pending = null;
        }
    }

    private void OnControlPointResponse(byte[] value)
    {
        if (FtmsProtocol.ParseResponse(value) is not { } response)
            return;
        lock (_responseGate)
        {
            if (_pending is { } pending && pending.OpCode == response.OpCode)
                pending.Done.TrySetResult(response.Result);
        }
    }

    private void OnBridgeStatus(byte[] value)
    {
        if (FtmsProtocol.ParseBridgeStatus(value) is not { } status)
            return;
        lock (_stateGate)
        {
            _statusNotifications++;
            ApplyBridgeStatus(status);
            UpdateCapabilities();
        }
        Wake();
    }

    /// <summary>Caller holds <see cref="_stateGate"/>.</summary>
    private void ApplyBridgeStatus(BridgeStatus status)
    {
        if (_bridgeStatus is { } before && before.CockpitConnected != status.CockpitConnected)
            AppPaths.Note($"Bluetooth: {_name} reports the cockpit {(status.CockpitConnected ? "connected" : "not answering")}");
        _bridgeStatus = status;
        _cockpitConnected = status.CockpitConnected;
    }

    /// <summary>Caller holds <see cref="_stateGate"/>.</summary>
    private void UpdateCapabilities()
    {
        if (_isBridge && _bridgeStatus is { } status)
        {
            var max = Math.Clamp(status.TargetLimit, ErgoController.MinWatt, ErgoController.AbsoluteMaxWatt);
            var step = Math.Max(1, _range?.StepWatt ?? 5);
            var info = status.CockpitType is { } type
                ? L.F("Ftms_InfoBridge", _name, DaumCockpit.DescribeType((byte)type), max)
                : L.F("Ftms_InfoBridgeUnknown", _name, max);
            _capabilities = new ErgometerCapabilities(max, step, RpmRange.For, info);
        }
        else if (_range is { } range)
        {
            var max = Math.Clamp(range.MaxWatt, ErgoController.MinWatt, ErgoController.AbsoluteMaxWatt);
            var step = Math.Max(1, range.StepWatt);
            _capabilities = new ErgometerCapabilities(max, step, _isBridge ? RpmRange.For : null,
                L.F("Ftms_Info", _name, max, step));
        }
    }

    private void OnLost()
    {
        _lost = true;
        Wake();
    }

    private void OnIndoorBikeData(byte[] value)
    {
        if (FtmsProtocol.ParseIndoorBikeData(value) is not { } d)
            return;
        Interlocked.Exchange(ref _lastDataTicks, DateTime.UtcNow.Ticks);
        // Some trainers split the values over two notifications ("More Data"): keep the last of each.
        var m = _merged = new IndoorBikeData
        {
            SpeedKmh = d.SpeedKmh ?? _merged.SpeedKmh,
            Rpm = d.Rpm ?? _merged.Rpm,
            Watt = d.Watt ?? _merged.Watt,
            Pulse = d.Pulse ?? _merged.Pulse,
            DistanceKm = d.DistanceKm ?? _merged.DistanceKm,
            EnergyKJ = d.EnergyKJ ?? _merged.EnergyKJ,
            ElapsedSeconds = d.ElapsedSeconds ?? _merged.ElapsedSeconds,
        };
        if (!_connected)
            return; // an ErgoBridge without its cockpit sends zeros
        var rpm = (int)Math.Round(m.Rpm ?? 0);
        var data = new ErgometerData
        {
            Watt = m.Watt ?? 0,
            Rpm = rpm,
            Pulse = d.Pulse ?? 0, // only a pulse in this notification counts: no stale value once the belt is off
            Pedaling = rpm > 0,
            SpeedKmh = m.SpeedKmh,
            DistanceKm = m.DistanceKm,
            EnergyKJ = m.EnergyKJ,
            TimeSeconds = m.ElapsedSeconds,
        };
        // Handing on happens under the lock Dispose takes too: once Dispose returns, no reading follows.
        lock (_gate)
        {
            if (!_disposed)
                DataReceived?.Invoke(data);
        }
    }

    private void Detach(IFtmsConnection? connection)
    {
        if (connection is null)
            return;
        connection.IndoorBikeData -= OnIndoorBikeData;
        connection.ControlPointResponse -= OnControlPointResponse;
        connection.BridgeStatus -= OnBridgeStatus;
        connection.Lost -= OnLost;
    }

    private void Report(string text, bool ok, string? detail = null)
    {
        lock (_gate)
        {
            var status = new ErgometerStatus(text, ok, detail);
            // Same text but new details (e.g. the cockpit type arrived) still counts: the app re-reads the limits then.
            if (_disposed || status == _lastStatus)
                return;
            _lastStatus = status;
            StatusChanged?.Invoke(status);
        }
    }

    private static string Describe(Exception ex) => ex switch
    {
        FtmsLinkException => ex.Message,
        TimeoutException => L.T("Ftms_NoAnswer"),
        IOException => L.T("Ftms_ConnectionLost"),
        _ => ex.Message,
    };

    public void Dispose()
    {
        Task? loop;
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            loop = _loop;
        }
        _connected = false;
        _cancel.Cancel();
        try { loop?.Wait(TimeSpan.FromSeconds(4)); }
        catch (AggregateException) { /* the loop ends with the cancellation */ }
        _connected = false;
    }
}
