using System.Text.Json;
using System.Text.RegularExpressions;
using ErgoWattControl;

// Turns the generated profiles (CSV: Sekunden;Soll_W;Puls) into the app's program files: name and description from
// beschreibungen.json (keyed by the file's prefix, e.g. "P22" or "S02") and the profile.
//
//   dotnet run -- <csv-ordner> <beschreibungen.json> <ziel-ordner>
if (args.Length != 3)
{
    Console.Error.WriteLine("Aufruf: dotnet run -- <csv-ordner> <beschreibungen.json> <ziel-ordner>");
    return 1;
}
var (source, descriptionsFile, target) = (args[0], args[1], args[2]);
var descriptions = JsonSerializer.Deserialize<Dictionary<string, Description>>(File.ReadAllText(descriptionsFile))!;
Directory.CreateDirectory(target);

var exit = 0;
foreach (var csv in Directory.GetFiles(source, "*.csv").Order())
{
    var file = Path.GetFileNameWithoutExtension(csv);
    var code = file.Split('_')[0];
    if (!descriptions.TryGetValue(code, out var description))
    {
        Console.Error.WriteLine($"{file}: keine Beschreibung für {code}");
        exit = 1;
        continue;
    }
    var ftp = Regex.Match(file, @"_FTP(\d+)_") is { Success: true } m && m.Groups[1].Value != "000" ? int.Parse(m.Groups[1].Value) : 0;
    var program = new TrainingProgram
    {
        Name = $"{code} {description.Name}" + (ftp > 0 ? $" – FTP {ftp} W" : ""),
        Beschreibung = description.Beschreibung,
        Profil = [.. File.ReadAllLines(csv).Skip(1).Select(l => l.Split(';')).Where(c => c.Length >= 2)
            .Select(c => new[] { int.Parse(c[0]), int.Parse(c[1]) })],
    };
    program.ToWorkout(); // fails here, not in the app, if the profile is unusable
    program.Save(Path.Combine(target, file + ".json"));
    Console.WriteLine($"{file}.json  {program.Name}");
}
return exit;

record Description(string Name, string Beschreibung);
