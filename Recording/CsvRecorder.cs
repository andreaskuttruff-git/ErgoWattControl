using System.Globalization;
using System.Text;

namespace ErgoWattControl;

/// <summary>Values that only exist while riding a route.</summary>
public sealed record RouteRow(double PositionKm, double GradePercent, int Gear, double SpeedKmh);

/// <summary>Writes one CSV row per second; semicolon + decimal comma so it opens directly in German Excel.</summary>
public sealed class CsvRecorder : IDisposable
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private static readonly TimeSpan RowInterval = TimeSpan.FromSeconds(1);
    // Don't extrapolate across a connection gap.
    private static readonly TimeSpan MaxIntegrationStep = TimeSpan.FromSeconds(5);

    private readonly StreamWriter _writer;
    private readonly bool _routeColumns;
    private DateTime _lastRow = DateTime.MinValue;
    private DateTime? _lastSample;
    private readonly SinceStart _distance = new();
    private readonly SinceStart _energy = new();

    public string FilePath { get; }
    public int Rows { get; private set; }
    /// <summary>Recorded time; in a route only the time spent pedalling.</summary>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>Distance and energy since the recording started (the cockpit only reports totals since it was switched on).</summary>
    public double DistanceKm { get; private set; }
    public double EnergyKJ { get; private set; }

    /// <summary>Crank revolutions since start, integrated from RPM (the cockpit reports no revolution counter).</summary>
    public double Revolutions { get; private set; }

    public CsvRecorder(string directory, string fileName, bool routeColumns)
    {
        Directory.CreateDirectory(directory);
        FilePath = UniquePath(Path.Combine(directory, fileName + ".csv"));
        _routeColumns = routeColumns;
        // AutoFlush so a crash or power cut loses at most the current row.
        _writer = new StreamWriter(FilePath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)) { AutoFlush = true };
        _writer.WriteLine("Zeitstempel;Sekunden;Soll_W;Ist_W;RPM;Puls;Speed_kmh;Distanz_km;Energie_kJ;Umdrehungen"
            + (routeColumns ? ";Steigung_Prozent;Gang" : ""));
    }

    /// <param name="counting">false pauses the recording (route mode while not pedalling): no time, no rows.</param>
    /// <param name="route">Route values; distance and speed then come from the virtual ride instead of the cockpit.</param>
    public void Write(RunData d, int targetWatt, bool counting = true, RouteRow? route = null)
    {
        var now = DateTime.Now;
        var dt = _lastSample is { } last && now - last <= MaxIntegrationStep ? now - last : TimeSpan.Zero;
        _lastSample = now;

        DistanceKm = route?.PositionKm ?? _distance.Update(d.DistanceKm);
        EnergyKJ = _energy.Update(d.EnergyKJ);
        if (!counting)
            return;
        Elapsed += dt;
        Revolutions += d.Rpm * dt.TotalMinutes;

        if (now - _lastRow < RowInterval)
            return;
        _lastRow = now;

        var row = new List<string>
        {
            now.ToString("yyyy-MM-dd HH:mm:ss", German),
            ((int)Elapsed.TotalSeconds).ToString(),
            targetWatt.ToString(),
            d.Watt.ToString(),
            d.Rpm.ToString(),
            d.Pulse.ToString(),
            route is null ? d.SpeedKmh.ToString() : route.SpeedKmh.ToString("0.0", German),
            DistanceKm.ToString(route is null ? "0.0" : "0.000", German),
            EnergyKJ.ToString("0.0", German),
            ((int)Revolutions).ToString(),
        };
        if (_routeColumns && route is not null)
        {
            row.Add(route.GradePercent.ToString("0.0", German));
            row.Add(route.Gear.ToString());
        }
        _writer.WriteLine(string.Join(';', row));
        Rows++;
    }

    public void Dispose() => _writer.Dispose();

    public static string UniquePath(string path)
    {
        if (!File.Exists(path))
            return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i}.csv");
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
