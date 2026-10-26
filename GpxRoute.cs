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

    public string Name { get; }
    public double TotalDistance { get; }
    public double Ascent { get; }
    public double MaxGrade { get; }
    public double MinElevation { get; }
    public double MaxElevation { get; }

    private GpxRoute(string name, double[] elevation)
    {
        Name = name;
        _elevation = elevation;
        TotalDistance = (elevation.Length - 1) * Step;
        MinElevation = elevation.Min();
        MaxElevation = elevation.Max();
        for (var i = 1; i < elevation.Length; i++)
        {
            Ascent += Math.Max(0, elevation[i] - elevation[i - 1]);
            MaxGrade = Math.Max(MaxGrade, (elevation[i] - elevation[i - 1]) / Step);
        }
    }

    public static GpxRoute Load(string path)
    {
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
            throw new InvalidDataException("Die GPX-Datei enthält keinen Track mit Höhenangaben.");

        var cumulative = new double[points.Count];
        for (var i = 1; i < points.Count; i++)
            cumulative[i] = cumulative[i - 1] + Haversine(points[i - 1], points[i]);
        if (cumulative[^1] < 100)
            throw new InvalidDataException("Die Strecke ist kürzer als 100 m.");

        var count = (int)(cumulative[^1] / Step) + 1;
        var raw = new double[count];
        var j = 0;
        for (var i = 0; i < count; i++)
        {
            var d = i * Step;
            while (j < points.Count - 2 && cumulative[j + 1] < d)
                j++;
            var span = cumulative[j + 1] - cumulative[j];
            var f = span > 0 ? Math.Clamp((d - cumulative[j]) / span, 0, 1) : 0;
            raw[i] = points[j].Ele + f * (points[j + 1].Ele - points[j].Ele);
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

        return new GpxRoute(Path.GetFileNameWithoutExtension(path), smooth);
    }

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
