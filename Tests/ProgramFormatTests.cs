using System.Globalization;
using System.Text.Json;

namespace ErgoWattControl.Tests;

/// <summary>Program files with name and description per language (<see cref="LocalizedText"/>), and the old format.</summary>
public class ProgramFormatTests
{
    private static string Write(string json, string name = "Programm.json")
    {
        var path = Path.Combine(TestFolder.New("programm"), name);
        File.WriteAllText(path, json);
        return path;
    }

    private static T WithUiLanguage<T>(string culture, Func<T> action)
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try { return action(); }
        finally { CultureInfo.CurrentUICulture = before; }
    }

    [Theory]
    [InlineData("de", "Schwellenblöcke")]
    [InlineData("en", "Threshold blocks")]
    [InlineData("fr", "Threshold blocks")] // no French: English
    public void Text_InTheWantedLanguage_ElseEnglish(string language, string expected)
    {
        var text = new LocalizedText(new Dictionary<string, string> { ["de"] = "Schwellenblöcke", ["en"] = "Threshold blocks" });
        Assert.Equal(expected, text.In(language));
    }

    [Fact]
    public void Text_OnlyGerman_IsShownInEveryLanguage()
    {
        var text = new LocalizedText(new Dictionary<string, string> { ["de"] = "Nur deutsch" });
        Assert.Equal("Nur deutsch", text.In("en"));
        Assert.Equal("Nur deutsch", text.In("fr"));
    }

    [Fact]
    public void PlainText_IsWrittenAsString_LanguagesAsObject()
    {
        Assert.Equal("\"Mein Programm\"", JsonSerializer.Serialize(new LocalizedText("Mein Programm")));
        var both = new LocalizedText(new Dictionary<string, string> { ["en"] = "Ride", ["de"] = "Fahrt" });
        Assert.Equal("""{"de":"Fahrt","en":"Ride"}""", JsonSerializer.Serialize(both)); // German first, then English
    }

    [Fact]
    public void Program_WithTextsPerLanguage_ShowsTheUiLanguage()
    {
        var path = Write("""
            { "Name": { "de": "Mein Intervall", "en": "My intervals" },
              "Description": { "de": "Aufbau", "en": "Structure" },
              "Profile": [[0, 80], [600, 80]] }
            """);

        Assert.Equal("Mein Intervall", WithUiLanguage("de-DE", () => TrainingProgram.Load(path).DisplayName));
        Assert.Equal("My intervals", WithUiLanguage("en-US", () => TrainingProgram.Load(path).DisplayName));
        Assert.Equal("Structure", WithUiLanguage("en-GB", () => TrainingProgram.Load(path).Description.Text));
    }

    [Fact]
    public void Program_WithPlainTexts_StaysValid()
    {
        var program = TrainingProgram.Load(Write("""{ "Name": "Mein Programm", "Description": "Kurz", "Profile": [[0, 80], [600, 80]] }"""));
        Assert.Equal("Mein Programm", program.Name.In("en"));
        Assert.Equal("Kurz", program.Description.In("de"));
    }

    [Fact]
    public void OldProgram_WithBeschreibungAndProfil_IsRead()
    {
        var program = TrainingProgram.Load(Write("""{ "Name": "Alt", "Beschreibung": "Von früher", "Profil": [[0, 90], [300, 120], [600, 120]] }"""));

        Assert.Equal("Von früher", program.Description.Text);
        Assert.Equal(3, program.Profile.Length);
        Assert.Equal(120, program.ToWorkout().WattAt(400));
    }

    [Fact]
    public void Program_WithoutName_IsNamedAfterItsFile()
    {
        var program = TrainingProgram.Load(Write("""{ "Profile": [[0, 80], [600, 80]] }""", "Ohne_Namen.json"));
        Assert.Equal("Ohne_Namen", program.DisplayName);
    }

    [Fact]
    public void SavedProgram_UsesTheCurrentKeys()
    {
        var path = Write("""{ "Name": "Alt", "Beschreibung": "Von früher", "Profil": [[0, 90], [600, 90]] }""");
        var program = TrainingProgram.Load(path);
        var copy = Path.Combine(Path.GetDirectoryName(path)!, "Neu.json");

        program.Save(copy);

        var text = File.ReadAllText(copy);
        Assert.Contains("\"Description\"", text);
        Assert.Contains("\"Profile\"", text);
        Assert.DoesNotContain("Beschreibung", text);
        Assert.DoesNotContain("Profil\"", text);
        Assert.Contains("[0, 90]", text); // one pair per line
    }

    [Fact]
    public void ShippedPrograms_HaveGermanAndEnglishNames_AndEnglishFileNames()
    {
        ProgramFiles.Install();
        var programs = ProgramFiles.ProgramsIn(ProgramFiles.Folder).ToList();
        Assert.Equal(56, programs.Count);
        foreach (var path in programs)
        {
            var file = Path.GetFileName(path);
            var program = TrainingProgram.Load(path);
            Assert.Contains("de", program.Name.Languages);
            Assert.Contains("en", program.Name.Languages);
            Assert.NotEqual(program.Name.In("de"), "");
            Assert.False(program.Description.IsEmpty, file);
            // Language-neutral file names: plain ASCII, no German words with umlauts.
            Assert.Matches(@"^[PS]\d\d(_FTP\d{3})?_[A-Za-z0-9_\-]+\.json$", file);
            // Code and FTP level are the same in every language.
            var code = file[..3];
            Assert.StartsWith(code + " ", program.Name.In("de"));
            Assert.StartsWith(code + " ", program.Name.In("en"));
        }
    }

    [Fact]
    public void ShippedPrograms_StillContainTheKnownProfiles()
    {
        ProgramFiles.Install();
        var s08 = TrainingProgram.Load(Path.Combine(ProgramFiles.Folder, "S08_FTP200_Threshold_blocks_30min.json"));
        Assert.Equal("S08 Schwellenblöcke 30 min – FTP 200 W", s08.Name.In("de"));
        Assert.Equal("S08 Threshold blocks 30 min – FTP 200 W", s08.Name.In("en"));
        Assert.Equal(1800, s08.ToWorkout().Duration);
    }
}
