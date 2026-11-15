using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ErgoWattControl.Tests;

/// <summary>
/// The texts of the user interface (Localization\Strings*.resx): every key the code uses exists in every language,
/// with the same placeholders, and the language setting picks the right one.
/// </summary>
public partial class LocalizationTests
{
    private static readonly string Repo = FindRepo();

    private static string FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ErgoWattControl.csproj")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Projektordner nicht gefunden");
    }

    private static Dictionary<string, string> Table(string file) =>
        XDocument.Load(Path.Combine(Repo, "Localization", file)).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    private static readonly Dictionary<string, string> English = Table("Strings.resx");
    private static readonly Dictionary<string, string> German = Table("Strings.de.resx");

    /// <summary>Keys in L.T("…") / L.F("…") and in helpers that take a key (ShowLoadError).</summary>
    [GeneratedRegex(@"(?:\bL\.[TF]\(|ShowLoadError\()\s*""([A-Za-z0-9_]+)""")]
    private static partial Regex KeyUse();

    [GeneratedRegex(@"\{(\d+)(?:[:,][^}]*)?\}")]
    private static partial Regex Placeholder();

    private static IEnumerable<(string File, string Key)> UsedKeys() =>
        Directory.GetFiles(Repo, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}"))
            .SelectMany(f => KeyUse().Matches(File.ReadAllText(f)).Select(m => (Path.GetRelativePath(Repo, f), m.Groups[1].Value)));

    [Fact]
    public void EveryKeyUsedInTheCode_ExistsInEveryLanguage()
    {
        var used = UsedKeys().ToList();
        Assert.NotEmpty(used);
        var missing = used.Where(u => !English.ContainsKey(u.Key) || !German.ContainsKey(u.Key))
            .Select(u => $"{u.File}: {u.Key}").Distinct().ToList();
        Assert.True(missing.Count == 0, "Fehlende Texte:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void BothLanguages_HaveTheSameKeys()
    {
        Assert.Empty(English.Keys.Except(German.Keys));
        Assert.Empty(German.Keys.Except(English.Keys));
    }

    [Fact]
    public void Placeholders_AreTheSameInBothLanguages()
    {
        var wrong = new List<string>();
        foreach (var (key, english) in English)
        {
            var en = Placeholder().Matches(english).Select(m => m.Groups[1].Value).ToHashSet();
            var de = Placeholder().Matches(German[key]).Select(m => m.Groups[1].Value).ToHashSet();
            if (!en.SetEquals(de))
                wrong.Add($"{key}: en {{{string.Join(",", en)}}} / de {{{string.Join(",", de)}}}");
            // string.Format must accept the text (balanced braces).
            try { string.Format(english, new object[10]); string.Format(German[key], new object[10]); }
            catch (FormatException) { wrong.Add($"{key}: ungültiges Format"); }
        }
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    [Fact]
    public void NoTextIsEmpty()
    {
        Assert.Empty(English.Where(t => string.IsNullOrWhiteSpace(t.Value)).Select(t => "en " + t.Key));
        Assert.Empty(German.Where(t => string.IsNullOrWhiteSpace(t.Value)).Select(t => "de " + t.Key));
    }

    [Fact]
    public void UnusedTexts_AreReported()
    {
        // Texts nobody uses any more: delete them from both files. A key passed on as a string (e.g. the name key of
        // a device type) counts as used when it appears in quotes somewhere in the code.
        var code = string.Join("\n", Directory.GetFiles(Repo, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText));
        Assert.Empty(English.Keys.Where(k => !code.Contains($"\"{k}\"")));
    }

    [Fact]
    public void KeysPassedOnAsStrings_Exist()
    {
        foreach (var type in ErgometerTypes.All)
            Assert.DoesNotContain("[", type.Name);
        foreach (var service in new[] { UploadService.Garmin, UploadService.Strava })
            foreach (var key in new[] { service.LoginHint, service.PageName, service.NextStep })
                Assert.True(English.ContainsKey(key) && German.ContainsKey(key), key);
    }

    [Fact]
    public void Texts_ComeInTheChosenLanguage()
    {
        Assert.Equal("Settings", L.T("Settings_Title", CultureInfo.GetCultureInfo("en")));
        Assert.Equal("Einstellungen", L.T("Settings_Title", CultureInfo.GetCultureInfo("de")));
        // A language without its own texts gets English.
        Assert.Equal("Settings", L.T("Settings_Title", CultureInfo.GetCultureInfo("fr")));
    }

    [Theory]
    [InlineData("de", "de")]
    [InlineData("DE", "de")]
    [InlineData("en", "en")]
    [InlineData("pseudo", "pseudo")]
    [InlineData("auto", "auto")]
    [InlineData("fr", "auto")]
    [InlineData(null, "auto")]
    public void LanguageSetting_IsNormalized(string? stored, string expected)
    {
        // An unknown or differently written value must not look like a change in the settings dialog.
        Assert.Equal(expected, new AppSettings { Language = stored! }.Language);
    }

    [Fact]
    public void HelpPage_ExistsInTheExe_ForEveryLanguage()
    {
        var assembly = typeof(L).Assembly;
        foreach (var (code, _) in Language.Available)
        {
            var page = L.T("Help_PageUrl", CultureInfo.GetCultureInfo(code));
            Assert.NotNull(assembly.GetManifestResourceStream("Help/" + page));
        }
    }

    [Theory]
    [InlineData("de", "de")]
    [InlineData("en", "en")]
    public void LanguageSetting_IsApplied(string setting, string expected)
    {
        var before = CultureInfo.CurrentUICulture;
        try
        {
            Language.Apply(setting);
            Assert.Equal(expected, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        }
        finally
        {
            Language.Apply(before.TwoLetterISOLanguageName);
        }
    }

    [Fact]
    public void AutomaticLanguage_IsAnAvailableOne()
    {
        var code = Language.Resolve(Language.Auto).TwoLetterISOLanguageName;
        Assert.Contains(Language.Available, l => l.Code == code);
    }

    [Fact]
    public void Numbers_FollowWindows_NotTheLanguage()
    {
        var (ui, culture) = (CultureInfo.CurrentUICulture, CultureInfo.CurrentCulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Language.Apply("en");
            Assert.Equal("1,8 km", string.Format(CultureInfo.CurrentCulture, "{0:0.0} km", 1.8));
            Assert.Equal("de-DE", CultureInfo.CurrentCulture.Name); // the language setting leaves the number format alone
        }
        finally
        {
            CultureInfo.CurrentUICulture = ui;
            CultureInfo.CurrentCulture = culture;
            Language.Apply(ui.TwoLetterISOLanguageName);
        }
    }
}
