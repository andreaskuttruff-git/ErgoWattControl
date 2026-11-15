using System.Text.Json;
using System.Text.RegularExpressions;
using ErgoWattControl;

// Turns the generated profiles (CSV: seconds;watts;…) into the app's program files. From descriptions.json (keyed by
// the file's prefix, e.g. "P22" or "S02") come the name and description in every language and the file name: code,
// FTP level (from the CSV name, e.g. "_FTP200_") and the English "File" part, e.g. S08_FTP200_Threshold_blocks_30min.json.
// The name gets the code in front and the FTP level behind, the same in every language.
//
//   dotnet run -- <csv-ordner> <descriptions.json> <ziel-ordner>
if (args.Length != 3)
{
    Console.Error.WriteLine("Aufruf: dotnet run -- <csv-ordner> <descriptions.json> <ziel-ordner>");
    return 1;
}
var (source, descriptionsFile, target) = (args[0], args[1], args[2]);
var descriptions = JsonSerializer.Deserialize<Dictionary<string, ProgramTexts>>(File.ReadAllText(descriptionsFile))!;
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
    var level = Regex.Match(file, @"_FTP(\d+)_");
    var ftp = level.Success && level.Groups[1].Value != "000" ? int.Parse(level.Groups[1].Value) : 0;
    string FullName(string name) => $"{code} {name}" + (ftp > 0 ? $" – FTP {ftp} W" : "");
    var program = new TrainingProgram
    {
        Name = description.Name.IsSingle
            ? new LocalizedText(FullName(description.Name.Text))
            : new LocalizedText(description.Name.Languages.ToDictionary(l => l, l => FullName(description.Name.In(l)))),
        Description = description.Description,
        Profile = [.. File.ReadAllLines(csv).Skip(1).Select(l => l.Split(';')).Where(c => c.Length >= 2)
            .Select(c => new[] { int.Parse(c[0]), int.Parse(c[1]) })],
    };
    program.ToWorkout(); // fails here, not in the app, if the profile is unusable
    var name = code + (level.Success ? $"_FTP{level.Groups[1].Value}" : "") + "_" + description.File;
    program.Save(Path.Combine(target, name + ".json"));
    Console.WriteLine($"{name}.json  {program.Name.In("de")}  |  {program.Name.In("en")}");
}
return exit;

/// <param name="File">English, language-neutral part of the file name after code and FTP level.</param>
record ProgramTexts(string File, LocalizedText Name, LocalizedText Description);
