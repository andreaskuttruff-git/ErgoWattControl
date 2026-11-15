namespace ErgoWattControl.Tests;

/// <summary>settings.json: saving without half-written files, and a start that survives a broken or locked file.</summary>
public class AppSettingsTests : IDisposable
{
    private static string File_ => AppPaths.SettingsFile;

    public AppSettingsTests()
    {
        foreach (var f in new[] { File_, File_ + ".tmp", File_ + ".defekt" })
            File.Delete(f);
    }

    public void Dispose() => File.Delete(File_);

    [Fact]
    public void Save_ThenLoad_KeepsTheValues_AndLeavesNoTempFile()
    {
        var settings = new AppSettings { PortName = "COM5", Ride = { MaxWattLimit = 333, RiderWeightKg = 98.5 } };
        settings.Save();

        Assert.False(File.Exists(File_ + ".tmp"));
        var loaded = AppSettings.Load();
        Assert.Equal("COM5", loaded.PortName);
        Assert.Equal(333, loaded.Ride.MaxWattLimit);
        Assert.Equal(98.5, loaded.Ride.RiderWeightKg);
    }

    [Fact]
    public void Save_ReplacesAnExistingFile()
    {
        new AppSettings { PortName = "COM5" }.Save();
        new AppSettings { PortName = "COM6" }.Save();
        Assert.Equal("COM6", AppSettings.Load().PortName);
    }

    [Fact]
    public void MissingFile_IsCreatedWithDefaults()
    {
        var settings = AppSettings.Load();
        Assert.NotNull(settings);
        Assert.True(File.Exists(File_));
    }

    [Fact]
    public void BrokenFile_IsKeptAsBackup_AndReplacedByDefaults()
    {
        File.WriteAllText(File_, "{ \"PortName\": \"COM5\", kaputt");
        var mark = TestFolder.LogMark();

        var settings = AppSettings.Load();

        Assert.NotNull(settings);
        Assert.Equal("{ \"PortName\": \"COM5\", kaputt", File.ReadAllText(File_ + ".defekt"));
        Assert.DoesNotContain("kaputt", File.ReadAllText(File_));
        Assert.Contains("settings.json defekt", TestFolder.LogSince(mark));
    }

    [Fact]
    public void LockedFile_StartsWithDefaults_AndLeavesTheFileAlone()
    {
        new AppSettings { PortName = "COM5" }.Save();
        var before = File.ReadAllText(File_);
        var mark = TestFolder.LogMark();

        using (new FileStream(File_, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.NotEqual("COM5", AppSettings.Load().PortName);

        Assert.Equal(before, File.ReadAllText(File_));
        Assert.Contains("settings.json nicht lesbar", TestFolder.LogSince(mark));
    }

    [Theory]
    [InlineData("{Datum}_{Strecke}", "Tour", "2026-11-14_Tour")]
    [InlineData("{Programm} {Uhrzeit}", "S08", "S08 18-05-09")]
    [InlineData("a/b:c", null, "a_b_c")]
    public void BuildFileName_FillsThePattern(string pattern, string? route, string expected) =>
        Assert.Equal(expected, AppSettings.BuildFileName(pattern, new DateTime(2026, 11, 14, 18, 5, 9), route));

    [Theory]
    [InlineData("  Lauf.json ", "Lauf")]
    [InlineData("Lauf.CSV", "Lauf")]
    [InlineData("a?b", "a_b")]
    [InlineData("   ", null)]
    public void SanitizeFileName_MakesAUsableName(string name, string? expected) =>
        Assert.Equal(expected, AppSettings.SanitizeFileName(name));
}
