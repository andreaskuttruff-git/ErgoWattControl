using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ErgoWattControl;

/// <summary>
/// A recording as JSON: what was ridden (kind, settings at the start, the route or power profile), then one row of
/// measured values per second. The rows are a table (<see cref="Columns"/> + <see cref="Values"/>) with one row per
/// line, so the file stays compact and readable. Keys and column names are English whatever the app's language;
/// recordings of version 1 (German keys, up to v1.5) are read as well and converted on loading.
/// </summary>
public sealed partial class RecordingFile
{
    /// <summary>Column names of the value table (language-neutral ids, also the CSV header in English).</summary>
    public static class Col
    {
        /// <summary>Seconds since <see cref="Start"/>.</summary>
        public const string Time = "Time_s";
        /// <summary>Ridden time: pauses in a route or replay don't count.</summary>
        public const string Seconds = "Seconds";
        public const string TargetWatt = "Target_W";
        public const string Watt = "Power_W";
        public const string Rpm = "Rpm";
        public const string Pulse = "Pulse";
        public const string Speed = "Speed_kmh";
        public const string Distance = "Distance_km";
        public const string Energy = "Energy_kJ";
        public const string Revolutions = "Revolutions";
        public const string Grade = "Grade_percent";
        public const string Gear = "Gear";
    }

    /// <summary>The kinds of recording (<see cref="Kind"/>).</summary>
    public static class Kinds
    {
        /// <summary>Free training or a replayed recording.</summary>
        public const string Training = "Training";
        public const string Route = "Route";
        public const string Program = "Program";
    }

    public static readonly string[] BaseColumns =
        [Col.Time, Col.Seconds, Col.TargetWatt, Col.Watt, Col.Rpm, Col.Pulse, Col.Speed, Col.Distance, Col.Energy, Col.Revolutions];
    public static readonly string[] RouteColumns = [Col.Grade, Col.Gear];

    /// <summary>Column names of version 1 (and of the German CSV export) → current ids.</summary>
    public static readonly IReadOnlyDictionary<string, string> OldColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Uhrzeit_s"] = Col.Time,
        ["Sekunden"] = Col.Seconds,
        ["Soll_W"] = Col.TargetWatt,
        ["Ist_W"] = Col.Watt,
        ["RPM"] = Col.Rpm,
        ["Puls"] = Col.Pulse,
        ["Distanz_km"] = Col.Distance,
        ["Energie_kJ"] = Col.Energy,
        ["Umdrehungen"] = Col.Revolutions,
        ["Steigung_Prozent"] = Col.Grade,
        ["Gang"] = Col.Gear,
    };

    /// <summary>The current id of a column name from any version (unknown names stay as they are).</summary>
    public static string ColumnId(string name) => OldColumns.TryGetValue(name.Trim(), out var id) ? id : name.Trim();

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public const string FormatName = "ErgoWattControl-Recording";
    public const int CurrentVersion = 2;

    public string Format { get; set; } = FormatName;
    public int Version { get; set; } = CurrentVersion;
    /// <summary><see cref="Kinds.Training"/> (free or a replayed recording), <see cref="Kinds.Route"/> or <see cref="Kinds.Program"/>.</summary>
    public string Kind { get; set; } = Kinds.Training;
    public DateTime Start { get; set; }
    /// <summary>Time of the last save; the end of the ride once the recording is finished.</summary>
    public DateTime? End { get; set; }
    /// <summary>The settings at the start of the ride (weights, gearing, grade factor, …).</summary>
    public AppSettings? Settings { get; set; }
    public RecordingTarget? Target { get; set; }
    /// <summary>Names of the values in each row (<see cref="Col"/>).</summary>
    public string[] Columns { get; set; } = [];
    public List<double[]> Values { get; set; } = [];

    // Version 1 (up to v1.5) – read, never written.
    [JsonInclude, JsonPropertyName("Art"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private string? OldKind { get => null; set { if (value is not null) Kind = value; } }
    [JsonInclude, JsonPropertyName("Ende"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private DateTime? OldEnd { get => null; set { if (value is not null) End = value; } }
    [JsonInclude, JsonPropertyName("Einstellungen"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private AppSettings? OldSettings { get => null; set { if (value is not null) Settings = value; } }
    [JsonInclude, JsonPropertyName("Vorgabe"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private RecordingTarget? OldTarget { get => null; set { if (value is not null) Target = value; } }
    [JsonInclude, JsonPropertyName("Spalten"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private string[]? OldColumnNames { get => null; set { if (value is not null) Columns = value; } }
    [JsonInclude, JsonPropertyName("Werte"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private List<double[]>? OldValues { get => null; set { if (value is not null) Values = value; } }

    /// <summary>Index of a column in <see cref="Columns"/>, or -1.</summary>
    public int ColumnIndex(string column) => Array.IndexOf(Columns, column);

    public static RecordingFile Load(string path)
    {
        var file = JsonSerializer.Deserialize<RecordingFile>(File.ReadAllText(path), Options) ?? throw new InvalidDataException(L.T("File_Empty"));
        // Version 1: German column names and kinds.
        file.Columns = [.. file.Columns.Select(ColumnId)];
        file.Kind = file.Kind switch
        {
            "Strecke" => Kinds.Route,
            "Programm" => Kinds.Program,
            _ => file.Kind,
        };
        file.Format = FormatName;
        return file;
    }

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
    public string? File { get; set; }
    /// <summary>Power profile as [seconds, watts]; the power holds until the next point.</summary>
    public int[][]? Profile { get; set; }
    /// <summary>The route as ridden, every 10 m: [distance m, smoothed elevation m, latitude, longitude]. Elevation in mm:
    /// gradients come from 10-m differences, coarser rounding would make them jump.</summary>
    public double[][]? Route { get; set; }

    // Version 1 – read, never written.
    [JsonInclude, JsonPropertyName("Datei"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private string? OldFile { get => null; set { if (value is not null) File = value; } }
    [JsonInclude, JsonPropertyName("Profil"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private int[][]? OldProfile { get => null; set { if (value is not null) Profile = value; } }
    [JsonInclude, JsonPropertyName("Strecke"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private double[][]? OldRoute { get => null; set { if (value is not null) Route = value; } }

    public static RecordingTarget FromRoute(GpxRoute route) => new()
    {
        Name = route.Name,
        File = route.FilePath,
        Route = [.. route.Points().Select(p => new[] { p.Distance, Math.Round(p.Elevation, 3), Math.Round(p.Lat, 6), Math.Round(p.Lon, 6) })],
    };

    public static RecordingTarget FromWorkout(WorkoutRecording workout) => new()
    {
        Name = workout.Name,
        File = workout.FilePath,
        Profile = [.. workout.Steps().Select(p => new[] { (int)Math.Round(p.Seconds), p.Watt })],
    };
}
