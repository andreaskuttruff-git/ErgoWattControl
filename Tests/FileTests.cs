namespace ErgoWattControl.Tests;

/// <summary>Recordings, exports, programs and routes: what the app writes it must be able to read again.</summary>
public class FileTests
{
    /// <summary>A 2-minute free training: 100 W for a minute, then 200 W; pulse rising from 100 to 159.</summary>
    private static RecordingFile SampleRecording()
    {
        var file = new RecordingFile
        {
            Start = new DateTime(2026, 11, 14, 18, 0, 0),
            End = new DateTime(2026, 11, 14, 18, 2, 0),
            Settings = new AppSettings(),
            Columns = RecordingFile.BaseColumns,
        };
        for (var s = 0; s < 120; s++)
        {
            var watt = s < 60 ? 100 : 200;
            // Uhrzeit_s, Sekunden, Soll_W, Ist_W, RPM, Puls, Speed_kmh, Distanz_km, Energie_kJ, Umdrehungen
            file.Values.Add([s, s, watt, watt, 85, 100 + s / 2, 30, Math.Round(s / 120.0, 1), Math.Round(s * watt / 1000.0, 1), s * 85 / 60]);
        }
        return file;
    }

    [Fact]
    public void Recording_SavedAsJson_ReplaysWithItsPowerAndPulse()
    {
        var path = Path.Combine(TestFolder.New("json"), "Lauf.json");
        SampleRecording().Save(path);

        var workout = WorkoutRecording.Load(path);

        Assert.False(workout.IsProgram);
        Assert.Equal(119, workout.Duration);
        Assert.Equal(100, workout.WattAt(30));
        Assert.Equal(200, workout.WattAt(90));
        Assert.True(workout.HasPulse);
        Assert.Equal(115, workout.PulseAt(30));
    }

    [Fact]
    public void Recording_IsWrittenWithEnglishKeysOnly()
    {
        var path = Path.Combine(TestFolder.New("format"), "Lauf.json");
        SampleRecording().Save(path);

        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var root = json.RootElement;
        Assert.Equal("ErgoWattControl-Recording", root.GetProperty("Format").GetString());
        Assert.Equal(2, root.GetProperty("Version").GetInt32());
        foreach (var key in new[] { "Kind", "Start", "Settings", "Columns", "Values" })
            Assert.True(root.TryGetProperty(key, out _), key);
        foreach (var old in new[] { "Art", "Ende", "Einstellungen", "Vorgabe", "Spalten", "Werte" })
            Assert.False(root.TryGetProperty(old, out _), old);
        Assert.Equal(RecordingFile.Col.Watt, root.GetProperty("Columns")[3].GetString());
    }

    /// <summary>A route recording as v1.5 wrote it: German keys, column names and kind.</summary>
    private const string OldRouteRecording = """
        {
          "Format": "ErgoWattControl-Aufzeichnung",
          "Version": 1,
          "Art": "Strecke",
          "Start": "2026-11-09T18:00:00",
          "Ende": "2026-11-09T18:00:12",
          "Vorgabe": { "Name": "Hügel", "Datei": "C:\\GPX\\Huegel.gpx",
                       "Strecke": [[0, 400, 47.0, 9.0], [10, 400.5, 47.0001, 9.0], [20, 401, 47.0002, 9.0]] },
          "Spalten": ["Uhrzeit_s", "Sekunden", "Soll_W", "Ist_W", "RPM", "Puls", "Speed_kmh", "Distanz_km", "Energie_kJ", "Umdrehungen", "Steigung_Prozent", "Gang"],
          "Werte": [
            [0, 0, 150, 148, 80, 120, 20, 0, 0, 0, 5, 8],
            [6, 6, 150, 150, 80, 125, 20, 0.033, 0.9, 8, 5, 8],
            [12, 12, 160, 158, 82, 130, 21, 0.068, 1.8, 16, 5, 8]
          ]
        }
        """;

    [Fact]
    public void OldRecording_IsReadWithCurrentNames()
    {
        var path = Path.Combine(TestFolder.New("alt"), "Strecke_alt.json");
        File.WriteAllText(path, OldRouteRecording);

        var file = RecordingFile.Load(path);

        Assert.Equal(RecordingFile.Kinds.Route, file.Kind);
        Assert.Equal(new DateTime(2026, 11, 9, 18, 0, 12), file.End);
        Assert.Equal("C:\\GPX\\Huegel.gpx", file.Target!.File);
        Assert.Equal(3, file.Target.Route!.Length);
        Assert.Equal([.. RecordingFile.BaseColumns, .. RecordingFile.RouteColumns], file.Columns);
        Assert.Equal(158, file.Values[2][file.ColumnIndex(RecordingFile.Col.Watt)]);
    }

    [Fact]
    public void OldRecording_CanBeReplayedAndExported()
    {
        var folder = TestFolder.New("alt_export");
        var path = Path.Combine(folder, "Strecke_alt.json");
        File.WriteAllText(path, OldRouteRecording);

        var workout = WorkoutRecording.Load(path);
        Assert.Equal(160, workout.WattAt(12));
        Assert.Equal(130, workout.PulseAt(12));

        var fit = Path.Combine(folder, "alt.fit");
        RecordingExport.WriteFit(RecordingFile.Load(path), fit);
        Assert.Equal(3, FitImport.Load(fit).Samples.Count);
    }

    [Fact]
    public void Csv_WithEnglishColumnIds_CanBeReplayed()
    {
        var path = Path.Combine(TestFolder.New("csv_en"), "Ride.csv");
        File.WriteAllText(path, "Zeitstempel;Time_s;Seconds;Target_W;Pulse\n2026-11-15 10:00:00;0;0;100;110\n2026-11-15 10:00:20;20;20;150;120\n");

        var workout = WorkoutRecording.Load(path);

        Assert.Equal(150, workout.WattAt(20));
        Assert.Equal(120, workout.PulseAt(20));
    }

    [Fact]
    public void FitExport_CanBeImportedAgain()
    {
        var folder = TestFolder.New("fit");
        var path = Path.Combine(folder, "Lauf.fit");

        RecordingExport.WriteFit(SampleRecording(), path);

        Assert.False(File.Exists(path + ".tmp"));
        var fit = FitImport.Load(path);
        Assert.True(fit.HasPower);
        Assert.False(fit.HasTrack);
        var workout = fit.ToWorkout();
        Assert.Equal(119, workout.Duration, 0);
        Assert.Equal(100, workout.WattAt(30));
        Assert.Equal(200, workout.WattAt(90));
        Assert.Equal(115, workout.PulseAt(30));
    }

    [Fact]
    public void FitExport_OfAnEmptyRecording_FailsWithoutLeavingAFile()
    {
        var path = Path.Combine(TestFolder.New("fitleer"), "Leer.fit");
        var empty = SampleRecording();
        empty.Values.Clear();

        Assert.Throws<InvalidDataException>(() => RecordingExport.WriteFit(empty, path));
        Assert.False(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void CsvExport_CanBeReplayed()
    {
        var path = Path.Combine(TestFolder.New("csv"), "Lauf.csv");

        RecordingExport.WriteCsv(SampleRecording(), path);

        var workout = WorkoutRecording.Load(path);
        Assert.Equal(119, workout.Duration);
        Assert.Equal(200, workout.WattAt(90));
        Assert.Equal(115, workout.PulseAt(30));
    }

    [Fact]
    public void FitImport_RejectsAFileThatIsNoFit()
    {
        var path = Path.Combine(TestFolder.New("keinfit"), "Text.fit");
        File.WriteAllText(path, "Das ist keine FIT-Datei, nur Text.");
        Assert.Throws<InvalidDataException>(() => FitImport.Load(path));
    }

    [Fact]
    public void ShippedPrograms_AllLoad()
    {
        ProgramFiles.Install();
        var programs = ProgramFiles.ProgramsIn(ProgramFiles.Folder).ToList();

        Assert.True(programs.Count >= 50, $"nur {programs.Count} Programme installiert");
        foreach (var path in programs)
        {
            var workout = TrainingProgram.Load(path).ToWorkout();
            Assert.True(workout.IsProgram, path);
            Assert.True(workout.Duration >= 60, $"{Path.GetFileName(path)}: {workout.Duration} s");
            Assert.InRange(workout.MaxWatt, 25, 800);
        }
    }

    [Fact]
    public void CustomProgramsFolder_IsCreated_WithSchemaNotesAndAnExampleThatLoads()
    {
        var folder = Path.Combine(TestFolder.New("eigene"), "CustomPrograms");

        ProgramFiles.EnsureCustomFolder(folder);

        Assert.True(File.Exists(Path.Combine(folder, "TrainingProgram.schema.json")));
        Assert.True(File.Exists(Path.Combine(folder, "CLAUDE.md")));
        Assert.True(File.Exists(Path.Combine(folder, "AGENTS.md")));
        var example = Assert.Single(ProgramFiles.ProgramsIn(folder)); // the schema is not listed as a program
        TrainingProgram.Load(example);
    }

    [Fact]
    public void CustomProgramsFolder_ThatCannotBeCreated_DoesNoHarm()
    {
        // A file where the folder should be.
        var blocker = Path.Combine(TestFolder.New("blockiert"), "CustomPrograms");
        File.WriteAllText(blocker, "");

        Assert.Equal(0, ProgramFiles.EnsureCustomFolder(blocker));
        Assert.Empty(ProgramFiles.ProgramsIn(blocker));
    }

    [Theory]
    [InlineData("""{ "Profil": [[0, 100]] }""", "mindestens zwei Punkte")]
    [InlineData("""{ "Profil": [[5, 100], [60, 100]] }""", "Sekunde 0")]
    [InlineData("""{ "Profil": [[0, 100], [60, 100], [30, 100]] }""", "muss größer sein")]
    [InlineData("""{ "Profil": [[0, 100], [60, 900]] }""", "außerhalb von 0–800 W")]
    [InlineData("""{ "Profil": [[0, 100], """, "Kein gültiges JSON")]
    public void BrokenProgram_IsRejectedWithAReason(string json, string reason)
    {
        var path = Path.Combine(TestFolder.New("kaputt"), "Kaputt.json");
        File.WriteAllText(path, json);

        var ex = Assert.Throws<InvalidDataException>(() => TrainingProgram.Load(path));
        Assert.Contains(reason, ex.Message);
    }

    [Fact]
    public void Route_FromTrack_MeasuresDistanceAndClimb()
    {
        // About 1.1 km due north (0.01° latitude), climbing 50 m evenly.
        var points = Enumerable.Range(0, 101).Select(i => (47.0 + i * 0.0001, 9.0, 400.0 + i * 0.5)).ToList();

        var route = GpxRoute.FromTrack("Test", null, points);

        Assert.InRange(route.TotalDistance, 1100, 1125);
        Assert.InRange(route.Ascent, 40, 50);
        Assert.InRange(route.GradeBetween(200, 800), 0.04, 0.05); // 50 m on 1112 m ≈ 4.5 % (as a fraction)
    }

    [Fact]
    public void Route_ShorterThan100m_IsRejected()
    {
        var points = new List<(double, double, double)> { (47.0, 9.0, 400), (47.0002, 9.0, 401) };
        Assert.Throws<InvalidDataException>(() => GpxRoute.FromTrack("Kurz", null, points));
    }

    [Fact]
    public void UniquePath_DoesNotOverwriteAnExistingRecording()
    {
        var folder = TestFolder.New("eindeutig");
        var path = Path.Combine(folder, "Lauf.json");
        File.WriteAllText(path, "{}");

        var other = JsonRecorder.UniquePath(path);

        Assert.NotEqual(path, other);
        Assert.False(File.Exists(other));
    }
}
