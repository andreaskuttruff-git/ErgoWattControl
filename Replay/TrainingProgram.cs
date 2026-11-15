using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ErgoWattControl;

/// <summary>
/// A training program: name, description and the power profile as [seconds, watts] pairs. Replayed like a recording;
/// the start dialog draws the profile from these data. Name and description may be given per language
/// (<see cref="LocalizedText"/>); files up to v1.5 with "Beschreibung"/"Profil" are read as well.
/// </summary>
public sealed partial class TrainingProgram
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public LocalizedText Name { get; set; } = LocalizedText.Empty;
    public LocalizedText Description { get; set; } = LocalizedText.Empty;
    public int[][] Profile { get; set; } = [];

    // Version 1 (up to v1.5) – read, never written.
    [JsonInclude, JsonPropertyName("Beschreibung"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private LocalizedText? OldDescription { get => null; set { if (value is not null) Description = value; } }
    [JsonInclude, JsonPropertyName("Profil"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private int[][]? OldProfile { get => null; set { if (value is not null) Profile = value; } }

    /// <summary>The name in the language of the user interface (the file name if the program has none).</summary>
    public string DisplayName => Name.Text;

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
        Validate(program.Profile);
        program.FilePath = path;
        if (program.Name.IsEmpty)
            program.Name = new LocalizedText(Path.GetFileNameWithoutExtension(path));
        return program;
    }

    /// <summary>The rules of TrainingProgram.schema.json, with messages that point to the faulty point.</summary>
    private static void Validate(int[][] profile)
    {
        if (profile is null || profile.Length < 2)
            throw new InvalidDataException("\"Profile\" braucht mindestens zwei Punkte [Sekunde, Watt] – der letzte markiert das Ende.");
        for (var i = 0; i < profile.Length; i++)
        {
            var point = profile[i];
            var where = $"Profile, Punkt {i + 1}";
            if (point is null || point.Length != 2)
                throw new InvalidDataException($"{where}: erwartet [Sekunde, Watt].");
            if (i == 0 && point[0] != 0)
                throw new InvalidDataException($"{where}: Der erste Punkt muss bei Sekunde 0 beginnen.");
            if (i > 0 && point[0] <= profile[i - 1][0])
                throw new InvalidDataException($"{where}: Sekunde {point[0]} muss größer sein als beim Punkt davor ({profile[i - 1][0]}).");
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

    public WorkoutRecording ToWorkout() => WorkoutRecording.FromProfile(DisplayName, Profile.Select(p => ((double)p[0], p[1])), FilePath);

    [GeneratedRegex(@"\[\s+(-?\d+),\s+(-?\d+)\s+\]")]
    private static partial Regex PairRegex();
}
