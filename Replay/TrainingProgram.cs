using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ErgoWattControl;

/// <summary>
/// A training program from the Programme folder: name, description and the power profile as [seconds, watts] pairs.
/// Replayed like a recording; the start dialog draws the profile from these data.
/// </summary>
public sealed partial class TrainingProgram
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string Name { get; set; } = "";
    public string Beschreibung { get; set; } = "";
    public int[][] Profil { get; set; } = [];

    [JsonIgnore]
    public string FilePath { get; private set; } = "";

    public static TrainingProgram Load(string path)
    {
        TrainingProgram program;
        try
        {
            program = JsonSerializer.Deserialize<TrainingProgram>(File.ReadAllText(path), Options)
                ?? throw new InvalidDataException("Die Datei ist leer.");
        }
        catch (JsonException ex)
        {
            // Hand-written files: say where the JSON breaks (line from the parser, 0-based).
            var where = ex.LineNumber is { } line ? $" (Zeile {line + 1})" : "";
            throw new InvalidDataException($"Kein gültiges JSON{where}: {ex.Message.Split(". Path:")[0]}", ex);
        }
        Validate(program.Profil);
        program.FilePath = path;
        if (string.IsNullOrWhiteSpace(program.Name))
            program.Name = Path.GetFileNameWithoutExtension(path);
        return program;
    }

    /// <summary>The rules of Trainingsprogramm.schema.json, with messages that point to the faulty point.</summary>
    private static void Validate(int[][] profil)
    {
        if (profil is null || profil.Length < 2)
            throw new InvalidDataException("\"Profil\" braucht mindestens zwei Punkte [Sekunde, Watt] – der letzte markiert das Ende.");
        for (var i = 0; i < profil.Length; i++)
        {
            var point = profil[i];
            var where = $"Profil, Punkt {i + 1}";
            if (point is null || point.Length != 2)
                throw new InvalidDataException($"{where}: erwartet [Sekunde, Watt].");
            if (i == 0 && point[0] != 0)
                throw new InvalidDataException($"{where}: Der erste Punkt muss bei Sekunde 0 beginnen.");
            if (i > 0 && point[0] <= profil[i - 1][0])
                throw new InvalidDataException($"{where}: Sekunde {point[0]} muss größer sein als beim Punkt davor ({profil[i - 1][0]}).");
            if (point[1] is < 0 or > 800)
                throw new InvalidDataException($"{where}: {point[1]} W liegt außerhalb von 0–800 W.");
        }
    }

    public void Save(string path)
    {
        // One [seconds, watts] pair per line instead of three, so the file stays readable.
        var json = PairRegex().Replace(JsonSerializer.Serialize(this, Options), "[$1, $2]");
        File.WriteAllText(path, json);
    }

    public WorkoutRecording ToWorkout() => WorkoutRecording.FromProfile(Name, Profil.Select(p => ((double)p[0], p[1])), FilePath);

    [GeneratedRegex(@"\[\s+(-?\d+),\s+(-?\d+)\s+\]")]
    private static partial Regex PairRegex();
}
