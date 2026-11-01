using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ErgoWattControl;

/// <summary>
/// A recording as JSON: what was ridden (kind, settings at the start, the route or power profile), then one row of
/// measured values per second. The rows are a table (<see cref="Spalten"/> + <see cref="Werte"/>) with one row per
/// line, so the file stays compact and readable.
/// </summary>
public sealed partial class RecordingFile
{
    public static readonly string[] BaseColumns =
        ["Uhrzeit_s", "Sekunden", "Soll_W", "Ist_W", "RPM", "Puls", "Speed_kmh", "Distanz_km", "Energie_kJ", "Umdrehungen"];
    public static readonly string[] RouteColumns = ["Steigung_Prozent", "Gang"];

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string Format { get; set; } = "ErgoWattControl-Aufzeichnung";
    public int Version { get; set; } = 1;
    /// <summary>"Training" (free or a replayed recording), "Strecke" or "Programm".</summary>
    public string Art { get; set; } = "Training";
    public DateTime Start { get; set; }
    /// <summary>Time of the last save; the end of the ride once the recording is finished.</summary>
    public DateTime? Ende { get; set; }
    /// <summary>The settings at the start of the ride (weights, gearing, grade factor, …).</summary>
    public AppSettings? Einstellungen { get; set; }
    public RecordingTarget? Vorgabe { get; set; }
    /// <summary>Uhrzeit_s = seconds since <see cref="Start"/>; Sekunden = ridden time (pauses in a route don't count).</summary>
    public string[] Spalten { get; set; } = [];
    public List<double[]> Werte { get; set; } = [];

    public static RecordingFile Load(string path) =>
        JsonSerializer.Deserialize<RecordingFile>(File.ReadAllText(path), Options) ?? throw new InvalidDataException("Die Datei ist leer.");

    /// <summary>Writes via a temporary file, so the recording on disk is always complete JSON.</summary>
    public void Save(string path)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, Compact(JsonSerializer.Serialize(this, Options)));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Puts every array of plain numbers on one line ([1, 2, 3] instead of one number per line).</summary>
    public static string Compact(string json) =>
        NumberArray().Replace(json, m => "[" + string.Join(", ", m.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries)) + "]");

    [GeneratedRegex(@"\[\s*(-?[\d.eE+-]+(?:\s*,\s*-?[\d.eE+-]+)*)\s*\]")]
    private static partial Regex NumberArray();
}

/// <summary>What the ride followed: a GPX route or a power profile (program or replayed recording).</summary>
public sealed class RecordingTarget
{
    public string Name { get; set; } = "";
    public string? Datei { get; set; }
    /// <summary>Power profile as [seconds, watts]; the power holds until the next point.</summary>
    public int[][]? Profil { get; set; }
    /// <summary>The route as ridden, every 10 m: [distance m, smoothed elevation m, latitude, longitude]. Elevation in mm:
    /// gradients come from 10-m differences, coarser rounding would make them jump.</summary>
    public double[][]? Strecke { get; set; }

    public static RecordingTarget FromRoute(GpxRoute route) => new()
    {
        Name = route.Name,
        Datei = route.FilePath,
        Strecke = [.. route.Points().Select(p => new[] { p.Distance, Math.Round(p.Elevation, 3), Math.Round(p.Lat, 6), Math.Round(p.Lon, 6) })],
    };

    public static RecordingTarget FromWorkout(WorkoutRecording workout) => new()
    {
        Name = workout.Name,
        Datei = workout.FilePath,
        Profil = [.. workout.Steps().Select(p => new[] { (int)Math.Round(p.Seconds), p.Watt })],
    };
}
