using Fit = Dynastream.Fit;

namespace ErgoWattControl;

/// <summary>
/// Reads a FIT activity (Garmin Connect, Strava, Zwift, a bike computer …) for riding it again: with power data as a
/// power profile like a recording, with GPS and elevation as a route like a GPX file.
/// </summary>
public sealed partial class FitImport
{
    /// <summary>One record: seconds since the start, power, heart rate, position and elevation (null where the file has none).</summary>
    public readonly record struct Sample(double Seconds, int? Watt, int? Pulse, double? Lat, double? Lon, double? Elevation);

    /// <summary>Pauses longer than this (auto-pause, a stop) are left out of the time line.</summary>
    private const double MaxGap = 10;

    /// <summary>
    /// The file name – or, for Garmin's numbered exports ("24087410711_ACTIVITY.fit"), "Garmin_&lt;date&gt;_&lt;time&gt;" of the
    /// ride, since the number says nothing. Also used for the route and the file name of the new recording.
    /// </summary>
    public string Name => GarminNumber().IsMatch(System.IO.Path.GetFileNameWithoutExtension(Path))
        ? $"Garmin_{Start:yyyy-MM-dd_HH-mm}"
        : System.IO.Path.GetFileNameWithoutExtension(Path);

    public string Path { get; }
    public IReadOnlyList<Sample> Samples { get; }

    /// <summary>Local start time of the ride (first record).</summary>
    public DateTime Start { get; private init; }

    /// <summary>Name for a list of files: <see cref="Name"/> for a FIT file (reads it), otherwise the file name.</summary>
    public static string DisplayName(string path)
    {
        var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
        if (!IsFit(path) || !GarminNumber().IsMatch(fileName))
            return fileName;
        try { return Load(path).Name; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return fileName; }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^\d{6,}(_ACTIVITY)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex GarminNumber();

    /// <summary>Enough power values to ride the ride again as a power profile.</summary>
    public bool HasPower => Samples.Count(s => s.Watt > 0) >= Math.Max(10, Samples.Count / 4);

    /// <summary>Enough positions with elevation to ride it again as a route.</summary>
    public bool HasTrack => Samples.Count(s => s.Lat is not null && s.Elevation is not null) >= 10;

    private FitImport(string path, IReadOnlyList<Sample> samples)
    {
        Path = path;
        Samples = samples;
    }

    public static bool IsFit(string path) => System.IO.Path.GetExtension(path).Equals(".fit", StringComparison.OrdinalIgnoreCase);

    public static FitImport Load(string path)
    {
        var records = new List<(uint Time, int? Watt, int? Pulse, double? Lat, double? Lon, double? Elevation)>();
        var decode = new Fit.Decode();
        var broadcaster = new Fit.MesgBroadcaster();
        decode.MesgEvent += broadcaster.OnMesg;
        broadcaster.RecordMesgEvent += (_, e) =>
        {
            var r = (Fit.RecordMesg)e.mesg;
            if (r.GetTimestamp() is not { } time)
                return;
            int? lat = r.GetPositionLat(), lon = r.GetPositionLong();
            float? elevation = r.GetEnhancedAltitude() ?? r.GetAltitude();
            records.Add((time.GetTimeStamp(), r.GetPower(), r.GetHeartRate(),
                lat is { } a && lon is not null ? Degrees(a) : null,
                lon is { } o && lat is not null ? Degrees(o) : null,
                elevation is { } h && float.IsFinite(h) ? h : null));
        };
        try
        {
            using var stream = File.OpenRead(path);
            if (!decode.IsFIT(stream))
                throw new InvalidDataException("Das ist keine FIT-Datei.");
            stream.Position = 0;
            decode.Read(stream);
        }
        catch (Fit.FitException ex)
        {
            throw new InvalidDataException($"Die FIT-Datei ist beschädigt oder unvollständig: {ex.Message}", ex);
        }
        if (records.Count < 2)
            throw new InvalidDataException("Die FIT-Datei enthält keine Messwerte (records).");

        // Time line without pauses: a gap longer than MaxGap counts as one second.
        records.Sort((a, b) => a.Time.CompareTo(b.Time));
        var samples = new List<Sample>(records.Count);
        double seconds = 0;
        for (var i = 0; i < records.Count; i++)
        {
            if (i > 0)
            {
                var step = (double)(records[i].Time - records[i - 1].Time);
                seconds += step > MaxGap ? 1 : step;
            }
            var r = records[i];
            samples.Add(new Sample(seconds, r.Watt, r.Pulse, r.Lat, r.Lon, r.Elevation));
        }
        return new FitImport(path, samples) { Start = new Fit.DateTime(records[0].Time).GetDateTime().ToLocalTime() };
    }

    /// <summary>The power (and pulse) over time, ridden again like a recording.</summary>
    public WorkoutRecording ToWorkout()
    {
        if (!HasPower)
            throw new InvalidDataException(HasTrack
                ? "Die FIT-Datei enthält keine Leistungswerte – fahre sie unter „Strecke fahren“ als Strecke nach."
                : "Die FIT-Datei enthält weder Leistungswerte noch eine Strecke.");
        // Gaps in the power stream (sensor dropouts) take the last value.
        var watt = 0;
        var rows = Samples.Select(s => (s.Seconds, watt = s.Watt ?? watt, s.Pulse ?? 0));
        return WorkoutRecording.FromRows(Name, rows, Path);
    }

    /// <summary>The ridden track as a route (like a GPX file).</summary>
    public GpxRoute ToRoute()
    {
        if (!HasTrack)
            throw new InvalidDataException("Die FIT-Datei enthält keine GPS-Strecke mit Höhenangaben (z. B. eine Indoor-Fahrt) – "
                + (HasPower ? "nachfahren lässt sie sich unter „Aufzeichnung nachfahren“ als Leistungsprofil." : "sie lässt sich nicht als Strecke fahren."));
        var track = Samples.Where(s => s.Lat is not null && s.Lon is not null && s.Elevation is not null).ToList();
        var route = GpxRoute.FromTrack(Name, Path, [.. track.Select(s => (s.Lat!.Value, s.Lon!.Value, s.Elevation!.Value))]);
        // The pulse of that ride by distance along the same track, for the green curve in the route view.
        var pulse = new List<(double Meters, int Pulse)>(track.Count);
        double meters = 0;
        for (var i = 0; i < track.Count; i++)
        {
            if (i > 0)
                meters += GpxRoute.Distance(track[i - 1].Lat!.Value, track[i - 1].Lon!.Value, track[i].Lat!.Value, track[i].Lon!.Value);
            if (track[i].Pulse is { } p)
                pulse.Add((meters, p));
        }
        return route.WithOriginalPulse(pulse);
    }

    private static double Degrees(int semicircles) => semicircles * (180.0 / 2147483648.0);
}
