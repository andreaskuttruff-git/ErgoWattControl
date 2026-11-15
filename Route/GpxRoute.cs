using System.Globalization;
using System.Xml.Linq;

namespace ErgoWattControl;

/// <summary>
/// A GPX track resampled every <see cref="Step"/> metres with smoothed elevation.
/// GPS elevations jump by several metres between points, so gradients are only meaningful after smoothing over distance.
/// </summary>
public sealed class GpxRoute
{
    public const double Step = 10;
    private const double SmoothingWindow = 75;

    private readonly double[] _elevation;
    private readonly double[] _lat;
    private readonly double[] _lon;

    public string Name { get; }
    /// <summary>The GPX file the route was loaded from (or originally came from, for a route stored with a recording).</summary>
    public string? FilePath { get; }
    public double TotalDistance { get; }
    public double Ascent { get; }
    public double MaxGrade { get; }
    /// <summary>Position (m) of the steepest 10-m step.</summary>
    public double MaxGradeDistance { get; }
    public double MinElevation { get; }
    public double MaxElevation { get; }

    private GpxRoute(string name, string? filePath, double[] elevation, double[] lat, double[] lon)
    {
        Name = name;
        FilePath = filePath;
        _elevation = elevation;
        _lat = lat;
        _lon = lon;
        TotalDistance = (elevation.Length - 1) * Step;
        MinElevation = elevation.Min();
        MaxElevation = elevation.Max();
        for (var i = 1; i < elevation.Length; i++)
        {
            Ascent += Math.Max(0, elevation[i] - elevation[i - 1]);
            var grade = (elevation[i] - elevation[i - 1]) / Step;
            if (grade > MaxGrade)
            {
                MaxGrade = grade;
                MaxGradeDistance = (i - 0.5) * Step;
            }
        }
    }

    /// <summary>A GPX file, or the track of a FIT activity (<see cref="FitImport"/>).</summary>
    public static GpxRoute Load(string path)
    {
        if (FitImport.IsFit(path))
            return FitImport.Load(path).ToRoute();
        var doc = XDocument.Load(path);
        var points = doc.Descendants()
            .Where(e => e.Name.LocalName is "trkpt" or "rtept")
            .Select(e => (
                Lat: double.Parse((string)e.Attribute("lat")!, CultureInfo.InvariantCulture),
                Lon: double.Parse((string)e.Attribute("lon")!, CultureInfo.InvariantCulture),
                Ele: e.Elements().FirstOrDefault(c => c.Name.LocalName == "ele") is { } ele
                    ? double.Parse(ele.Value, CultureInfo.InvariantCulture)
                    : double.NaN))
            .Where(p => !double.IsNaN(p.Ele))
            .ToList();
        if (points.Count < 2)
            throw new InvalidDataException(L.T("Gpx_NoTrack"));
        return FromTrack(Path.GetFileNameWithoutExtension(path), path, points);
    }

    /// <summary>A track of positions with elevation (GPX or FIT), resampled every <see cref="Step"/> m and smoothed.</summary>
    public static GpxRoute FromTrack(string name, string? path, IReadOnlyList<(double Lat, double Lon, double Ele)> points)
    {
        if (points.Count < 2)
            throw new InvalidDataException(L.T("Route_NoPoints"));
        var cumulative = new double[points.Count];
        for (var i = 1; i < points.Count; i++)
            cumulative[i] = cumulative[i - 1] + Haversine(points[i - 1], points[i]);
        if (cumulative[^1] < 100)
            throw new InvalidDataException(L.T("Route_TooShort"));

        var count = (int)(cumulative[^1] / Step) + 1;
        var raw = new double[count];
        var lat = new double[count];
        var lon = new double[count];
        var j = 0;
        for (var i = 0; i < count; i++)
        {
            var d = i * Step;
            while (j < points.Count - 2 && cumulative[j + 1] < d)
                j++;
            var span = cumulative[j + 1] - cumulative[j];
            var f = span > 0 ? Math.Clamp((d - cumulative[j]) / span, 0, 1) : 0;
            raw[i] = points[j].Ele + f * (points[j + 1].Ele - points[j].Ele);
            lat[i] = points[j].Lat + f * (points[j + 1].Lat - points[j].Lat);
            lon[i] = points[j].Lon + f * (points[j + 1].Lon - points[j].Lon);
        }

        var half = (int)(SmoothingWindow / Step / 2);
        var smooth = new double[count];
        for (var i = 0; i < count; i++)
        {
            var from = Math.Max(0, i - half);
            var to = Math.Min(count - 1, i + half);
            double sum = 0;
            for (var k = from; k <= to; k++)
                sum += raw[k];
            smooth[i] = sum / (to - from + 1);
        }

        return new GpxRoute(name, path, smooth, lat, lon);
    }

    /// <summary>A route stored with a recording (see <see cref="Points"/>): already resampled and smoothed.</summary>
    public static GpxRoute FromPoints(string name, string? filePath, IReadOnlyList<double[]> points)
    {
        if (points.Count < 2 || points.Any(p => p.Length < 2))
            throw new InvalidDataException(L.T("Route_StoredIncomplete"));
        for (var i = 0; i < points.Count; i++)
        {
            if (Math.Abs(points[i][0] - i * Step) > 0.5)
                throw new InvalidDataException(L.F("Route_StoredGaps", Step));
        }
        double Value(double[] p, int index) => p.Length > index ? p[index] : 0;
        return new GpxRoute(name, filePath, [.. points.Select(p => p[1])], [.. points.Select(p => Value(p, 2))], [.. points.Select(p => Value(p, 3))]);
    }

    /// <summary>The resampled route as ridden: every <see cref="Step"/> m the smoothed elevation and the position.</summary>
    public IEnumerable<(double Distance, double Elevation, double Lat, double Lon)> Points() =>
        _elevation.Select((e, i) => (i * Step, e, _lat[i], _lon[i]));

    public double ElevationAt(double distance)
    {
        var x = Math.Clamp(distance, 0, TotalDistance) / Step;
        var i = Math.Min((int)x, _elevation.Length - 2);
        return _elevation[i] + (x - i) * (_elevation[i + 1] - _elevation[i]);
    }

    /// <summary>Mean gradient (rise/run) between two positions on the route.</summary>
    public double GradeBetween(double from, double to)
    {
        from = Math.Clamp(from, 0, TotalDistance);
        to = Math.Clamp(to, 0, TotalDistance);
        if (to - from < Step)
        {
            from = Math.Max(0, Math.Min(from, TotalDistance - Step));
            to = from + Step;
        }
        return (ElevationAt(to) - ElevationAt(from)) / (to - from);
    }

    /// <summary>
    /// The pulse of the ride the route comes from (a FIT activity or a route recording of this app), by distance in
    /// metres – shown green in the route view next to today's pulse. Null for a plain GPX route.
    /// </summary>
    public IReadOnlyList<(double Meters, int Pulse)>? OriginalPulse { get; private set; }

    /// <summary>Attaches the pulse of the original ride (points without pulse are dropped); returns the route.</summary>
    public GpxRoute WithOriginalPulse(IEnumerable<(double Meters, int Pulse)> pulse)
    {
        var points = pulse.Where(p => p.Pulse > 0).OrderBy(p => p.Meters).ToList();
        OriginalPulse = points.Count >= 10 ? points : null;
        return this;
    }

    /// <summary>The original pulse around this point (average within ±50 m, else the nearest value within 300 m); null without one.</summary>
    public int? OriginalPulseAt(double meters)
    {
        if (OriginalPulse is not { Count: > 0 } points)
            return null;
        // First point at or after meters - 50 (the list is sorted by distance).
        int lo = 0, hi = points.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (points[mid].Meters < meters - 50) lo = mid + 1; else hi = mid;
        }
        var (sum, count) = (0, 0);
        for (var i = lo; i < points.Count && points[i].Meters <= meters + 50; i++)
        {
            sum += points[i].Pulse;
            count++;
        }
        if (count > 0)
            return (int)Math.Round((double)sum / count);
        var nearest = points.MinBy(p => Math.Abs(p.Meters - meters));
        return Math.Abs(nearest.Meters - meters) <= 300 ? nearest.Pulse : null;
    }

    /// <summary>Distance in metres between two positions (great circle).</summary>
    internal static double Distance(double lat1, double lon1, double lat2, double lon2) => Haversine((lat1, lon1, 0), (lat2, lon2, 0));

    private static double Haversine((double Lat, double Lon, double Ele) a, (double Lat, double Lon, double Ele) b)
    {
        const double r = 6371000;
        var p1 = a.Lat * Math.PI / 180;
        var p2 = b.Lat * Math.PI / 180;
        var dp = p2 - p1;
        var dl = (b.Lon - a.Lon) * Math.PI / 180;
        var h = Math.Sin(dp / 2) * Math.Sin(dp / 2) + Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
        return 2 * r * Math.Asin(Math.Sqrt(h));
    }
}
