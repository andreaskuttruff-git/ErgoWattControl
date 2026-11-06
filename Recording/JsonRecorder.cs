namespace ErgoWattControl;

/// <summary>Values that only exist while riding a route.</summary>
public sealed record RouteRow(double PositionKm, double GradePercent, int Gear, double SpeedKmh);

/// <summary>
/// Records one row per second into a <see cref="RecordingFile"/> together with the settings and the target (route or
/// power profile). The whole file is rewritten every few seconds via a temporary file, so a crash or power cut
/// leaves a valid file that lacks at most the last seconds.
/// </summary>
public sealed class JsonRecorder : IDisposable
{
    public const string Extension = ".json";

    private static readonly TimeSpan RowInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(10);
    // Don't extrapolate across a connection gap.
    private static readonly TimeSpan MaxIntegrationStep = TimeSpan.FromSeconds(5);

    private readonly RecordingFile _file;
    private readonly bool _routeColumns;
    private readonly DateTime _started = DateTime.Now;
    private DateTime _lastRow = DateTime.MinValue;
    private DateTime _lastSave = DateTime.Now;
    private DateTime? _lastSample;
    private readonly SinceStart _distance = new();
    private readonly SinceStart _energy = new();

    public string FilePath { get; }
    public int Rows => _file.Werte.Count;
    /// <summary>Recorded time; in a route only the time spent pedalling.</summary>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>Distance and energy since the recording started (the cockpit only reports totals since it was switched on).</summary>
    public double DistanceKm { get; private set; }
    public double EnergyKJ { get; private set; }

    /// <summary>Crank revolutions since start, integrated from RPM (the cockpit reports no revolution counter).</summary>
    public double Revolutions { get; private set; }

    /// <param name="kind">"Training", "Strecke" or "Programm".</param>
    /// <param name="target">The route or power profile being ridden; null in free training.</param>
    public JsonRecorder(string directory, string fileName, string kind, AppSettings settings, RecordingTarget? target)
    {
        Directory.CreateDirectory(directory);
        FilePath = UniquePath(Path.Combine(directory, fileName + Extension));
        _routeColumns = target?.Strecke is not null;
        _file = new RecordingFile
        {
            Art = kind,
            Start = _started,
            Einstellungen = settings.Snapshot(),
            Vorgabe = target,
            Spalten = [.. RecordingFile.BaseColumns, .. _routeColumns ? RecordingFile.RouteColumns : []],
        };
        _file.Save(FilePath);
    }

    /// <param name="counting">false pauses the recording (route mode while not pedalling): no time, no rows.</param>
    /// <param name="route">Route values; distance and speed then come from the virtual ride instead of the cockpit.</param>
    public void Write(ErgometerData d, int targetWatt, bool counting = true, RouteRow? route = null)
    {
        var now = DateTime.Now;
        var dt = _lastSample is { } last && now - last <= MaxIntegrationStep ? now - last : TimeSpan.Zero;
        _lastSample = now;

        // Devices that don't count distance or energy themselves: integrate speed and power.
        DistanceKm = route?.PositionKm
            ?? (d.DistanceKm is { } km ? _distance.Update(km) : DistanceKm + (d.SpeedKmh ?? 0) * dt.TotalHours);
        EnergyKJ = d.EnergyKJ is { } kj ? _energy.Update(kj) : EnergyKJ + d.Watt * dt.TotalSeconds / 1000;
        if (!counting)
            return;
        Elapsed += dt;
        Revolutions += d.Rpm * dt.TotalMinutes;

        if (now - _lastRow < RowInterval)
            return;
        _lastRow = now;

        var row = new List<double>
        {
            Math.Round((now - _started).TotalSeconds),
            (int)Elapsed.TotalSeconds,
            targetWatt,
            d.Watt,
            d.Rpm,
            d.Pulse,
            Math.Round(route?.SpeedKmh ?? d.SpeedKmh ?? 0, 1),
            Math.Round(DistanceKm, route is null ? 1 : 3),
            Math.Round(EnergyKJ, 1),
            (int)Revolutions,
        };
        if (_routeColumns)
        {
            row.Add(Math.Round(route?.GradePercent ?? 0, 1));
            row.Add(route?.Gear ?? 0);
        }
        _file.Werte.Add([.. row]);
        if (now - _lastSave >= SaveInterval)
            Save();
    }

    private void Save()
    {
        _lastSave = DateTime.Now;
        _file.Ende = _lastSave;
        _file.Save(FilePath);
    }

    public void Dispose() => Save();

    public static string UniquePath(string path)
    {
        if (!File.Exists(path))
            return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    /// <summary>Converts a running cockpit total into "amount since first sample"; survives a cockpit reset that drops the total back to 0.</summary>
    private sealed class SinceStart
    {
        private double? _base;
        private double _carried;
        private double _last;

        public double Update(double total)
        {
            if (_base is null)
            {
                _base = total;
            }
            else if (total < _last)
            {
                _carried += _last - _base.Value;
                _base = total;
            }
            _last = total;
            return _carried + total - _base.Value;
        }
    }
}
