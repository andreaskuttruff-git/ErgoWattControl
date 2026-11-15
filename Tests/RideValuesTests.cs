using System.Text.Json;

namespace ErgoWattControl.Tests;

/// <summary>
/// The ride values (<see cref="RideValues"/>) live apart from the other settings, under "Ride". Files from v1.5.1 and
/// earlier kept them at the top level – those must keep their values.
/// </summary>
public class RideValuesTests : IDisposable
{
    /// <summary>A settings.json as v1.5.1 wrote it: ride values next to the port and folders.</summary>
    private const string OldSettings = """
        {
          "Device": "DaumRs232",
          "PortName": "COM1",
          "MaxWattLimit": 300,
          "FileNameTraining": "Training_{Datum}_{Uhrzeit}",
          "ReplayShowOriginalPulse": true,
          "RiderWeightKg": 128,
          "BikeWeightKg": 14,
          "GradeFactorPercent": 70,
          "StartGear": 5,
          "Gearing": "Rennrad",
          "RollingResistance": 0.008,
          "DragAreaM2": 0.4
        }
        """;

    public RideValuesTests() => File.Delete(AppPaths.SettingsFile);

    public void Dispose() => File.Delete(AppPaths.SettingsFile);

    private static void AssertOldValues(RideValues ride)
    {
        Assert.Equal(128, ride.RiderWeightKg);
        Assert.Equal(14, ride.BikeWeightKg);
        Assert.Equal(142, ride.TotalMassKg);
        Assert.Equal(70, ride.GradeFactorPercent);
        Assert.Equal(5, ride.StartGear);
        Assert.Equal("Rennrad", ride.Gearing);
        Assert.Equal(0.008, ride.RollingResistance);
        Assert.Equal(0.4, ride.DragAreaM2);
        Assert.Equal(300, ride.MaxWattLimit);
    }

    [Fact]
    public void OldSettingsFile_KeepsItsRideValues()
    {
        File.WriteAllText(AppPaths.SettingsFile, OldSettings);

        var settings = AppSettings.Load();

        Assert.Equal("COM1", settings.PortName);
        AssertOldValues(settings.Ride);
    }

    [Fact]
    public void OldSettingsFile_IsRewrittenWithTheRideValuesGrouped()
    {
        File.WriteAllText(AppPaths.SettingsFile, OldSettings);

        AppSettings.Load(); // rewrites the file in the current layout

        using var json = JsonDocument.Parse(File.ReadAllText(AppPaths.SettingsFile));
        var root = json.RootElement;
        Assert.False(root.TryGetProperty("RiderWeightKg", out _));
        Assert.False(root.TryGetProperty("MaxWattLimit", out _));
        Assert.Equal(128, root.GetProperty("Ride").GetProperty("RiderWeightKg").GetDouble());
        Assert.Equal("COM1", root.GetProperty("PortName").GetString());
        AssertOldValues(AppSettings.Load().Ride); // and reads back the same
    }

    [Fact]
    public void HandEditedRideNull_FallsBackToTheDefaults()
    {
        File.WriteAllText(AppPaths.SettingsFile, """{ "PortName": "COM2", "Ride": null }""");

        var settings = AppSettings.Load();

        Assert.Equal("COM2", settings.PortName);
        Assert.Equal(135, settings.Ride.RiderWeightKg);
        Assert.Equal(135, AppSettings.Load().Ride.RiderWeightKg); // and the rewritten file loads too
    }

    [Fact]
    public void OldFileNamePatterns_GetTheEnglishPlaceholders()
    {
        File.WriteAllText(AppPaths.SettingsFile, """
            { "FileNameTraining": "Training_{Datum}_{Uhrzeit}",
              "FileNameRoute": "Strecke_{Strecke}_{Datum}_{Uhrzeit}",
              "FileNameProgram": "Prog_{Programm}_{Datum}" }
            """);

        var settings = AppSettings.Load();

        // The rider's own prefixes stay, only the placeholders change.
        Assert.Equal("Training_{Date}_{Time}", settings.FileNameTraining);
        Assert.Equal("Strecke_{Route}_{Date}_{Time}", settings.FileNameRoute);
        Assert.Equal("Prog_{Program}_{Date}", settings.FileNameProgram);
        Assert.Equal("Strecke_Tour_2026-11-15_10-00-00",
            AppSettings.BuildFileName(settings.FileNameRoute, new DateTime(2026, 11, 15, 10, 0, 0), "Tour"));
    }

    [Fact]
    public void NewSettings_UseEnglishFolderAndPatterns()
    {
        var settings = new AppSettings();
        Assert.Equal(AppPaths.DefaultRecordingFolder, settings.RecordingDirectory);
        Assert.EndsWith("Recordings", settings.RecordingDirectory);
        Assert.Equal("Route_{Route}_{Date}_{Time}", settings.FileNameRoute);
        Assert.Equal("Program_{Program}_{Date}_{Time}", settings.FileNameProgram);
        Assert.EndsWith("ErgoWattControl.log", AppPaths.LogFile);
        Assert.EndsWith("Programs", ProgramFiles.Folder);
    }

    [Fact]
    public void NewSettings_HaveTheDefaults()
    {
        var ride = new AppSettings().Ride;
        Assert.Equal(135, ride.RiderWeightKg);
        Assert.Equal(11, ride.BikeWeightKg);
        Assert.Equal(100, ride.GradeFactorPercent);
        Assert.Equal(RouteSession.GearingMtb, ride.Gearing);
        Assert.Equal(ErgoController.AbsoluteMaxWatt, ride.MaxWattLimit);
    }

    [Fact]
    public void OldRecording_KeepsTheRideValuesItWasRiddenWith()
    {
        // A route recording of v1.5.1: "Einstellungen" is the whole settings object of that day, ride values on top.
        var path = Path.Combine(TestFolder.New("alt"), "Strecke_alt.json");
        File.WriteAllText(path, $$"""
            {
              "Format": "ErgoWattControl-Aufzeichnung",
              "Version": 1,
              "Art": "Strecke",
              "Start": "2026-11-09T18:00:00",
              "Einstellungen": {{OldSettings}},
              "Spalten": ["Uhrzeit_s", "Sekunden", "Soll_W", "Ist_W", "RPM", "Puls", "Speed_kmh", "Distanz_km", "Energie_kJ", "Umdrehungen"],
              "Werte": [[0, 0, 100, 100, 80, 120, 20, 0, 0, 0]]
            }
            """);

        var file = RecordingFile.Load(path);

        AssertOldValues(file.Settings!.Ride);
    }

    [Fact]
    public void Copy_IsIndependent()
    {
        var ride = new RideValues { RiderWeightKg = 100 };
        var copy = ride.Copy();
        copy.RiderWeightKg = 90;
        Assert.Equal(100, ride.RiderWeightKg);
    }

    [Fact]
    public void Recording_StoresTheValuesChangedForThisRun()
    {
        var settings = new AppSettings { RecordingDirectory = TestFolder.New("lauf") };
        var ergo = new ErgoController();
        var training = new TrainingController(settings, ergo);
        var run = settings.Ride.Copy();
        run.RiderWeightKg = 99;
        run.GradeFactorPercent = 50;
        var points = Enumerable.Range(0, 101).Select(i => (47.0 + i * 0.0001, 9.0, 400.0 + i * 0.5)).ToList();
        training.BeginRoute(GpxRoute.FromTrack("Hügel", null, points), customName: null, run);

        var recorder = training.StartRecording();
        training.StopRecording();

        var stored = RecordingFile.Load(recorder.FilePath).Settings!.Ride;
        Assert.Equal(99, stored.RiderWeightKg);
        Assert.Equal(50, stored.GradeFactorPercent);
        Assert.Equal(135, settings.Ride.RiderWeightKg); // the saved settings are untouched
    }
}
