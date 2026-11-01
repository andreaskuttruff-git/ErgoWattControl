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
        var program = JsonSerializer.Deserialize<TrainingProgram>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("Die Datei ist leer.");
        if (program.Profil.Length < 2 || program.Profil.Any(p => p.Length < 2))
            throw new InvalidDataException("Das Programm enthält kein gültiges Leistungsprofil.");
        program.FilePath = path;
        if (string.IsNullOrWhiteSpace(program.Name))
            program.Name = Path.GetFileNameWithoutExtension(path);
        return program;
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
