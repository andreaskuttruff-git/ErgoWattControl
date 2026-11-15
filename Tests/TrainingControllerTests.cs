namespace ErgoWattControl.Tests;

/// <summary>The three training modes and their recording, without a window (<see cref="TrainingController"/>).</summary>
public class TrainingControllerTests
{
    private readonly AppSettings _settings = new() { RecordingDirectory = TestFolder.New("training") };
    private readonly ErgoController _ergo = new();
    private readonly TrainingController _training;

    public TrainingControllerTests()
    {
        _ergo.SetUserLimit(_settings.Ride.MaxWattLimit);
        _training = new TrainingController(_settings, _ergo);
    }

    private static readonly DateTime T0 = new(2026, 11, 15, 10, 0, 0);

    private static ErgometerData Pedalling(int watt = 100, int pulse = 120) =>
        new() { Watt = watt, Rpm = 80, Pulse = pulse, Pedaling = true, SpeedKmh = 30, DistanceKm = 0, EnergyKJ = 0 };

    private static readonly ErgometerData Standing = new() { Watt = 0, Rpm = 0, Pulse = 110 };

    /// <summary>About 1.1 km due north, 50 m of climbing.</summary>
    private static GpxRoute Hill() =>
        GpxRoute.FromTrack("Hügel", null, Enumerable.Range(0, 101).Select(i => (47.0 + i * 0.0001, 9.0, 400.0 + i * 0.5)).ToList());

    private static WorkoutRecording ShortProgram() =>
        WorkoutRecording.FromProfile("Kurz", [(0, 120), (5, 160), (12, 160)]);

    private static WorkoutRecording RecordingWithPulse() =>
        WorkoutRecording.FromRows("Lauf vom Montag", [(0, 150, 130), (60, 150, 140), (120, 150, 150)]);

    [Fact]
    public void StartsInFreeTraining_WhereThePowerKeysSetTheTarget()
    {
        Assert.IsType<FreeTraining>(_training.Mode);
        Assert.False(_training.Guided);
        _ergo.SetTargetWatt(100);

        _training.Step(+25);
        _training.Step(-5);

        Assert.Equal(120, _ergo.TargetWatt);
        Assert.Equal("ansicht", _training.Mode.HelpSection);
    }

    [Fact]
    public void FreeTraining_NeverStartsARecordingByItself()
    {
        Assert.False(_training.Update(Pedalling(), T0));
        Assert.Null(_training.Recorder);
    }

    [Fact]
    public void FreeRecording_IsATrainingFile_NamedByThePattern()
    {
        var recorder = _training.StartRecording();
        _training.Record(Pedalling());
        Assert.Same(recorder, _training.StopRecording());

        var file = RecordingFile.Load(recorder.FilePath);
        Assert.Equal("Training", file.Kind);
        Assert.Null(file.Target);
        Assert.StartsWith("Training_", Path.GetFileName(recorder.FilePath));
        Assert.Single(file.Values);
        Assert.Null(_training.StopRecording()); // nothing left to stop
    }

    [Fact]
    public void Route_PowerKeysShiftGears_AndTheRouteSetsThePower()
    {
        var route = _training.BeginRoute(Hill(), customName: null);

        Assert.True(_training.Guided);
        Assert.Equal(_ergo.TargetWatt, route.Session.TargetWatt);
        var gear = route.Session.Gear;
        var target = _ergo.TargetWatt;

        _training.Step(+5);
        _training.Step(+5);

        Assert.Equal(gear + 2, route.Session.Gear);
        Assert.Equal(target, _ergo.TargetWatt); // the power follows on the next reading, not the key
        Assert.Equal("strecke", route.HelpSection);
        Assert.Equal(("Strecke", "Hügel (1.1 km)"), (route.RunName.Kind, route.RunName.Name.Replace(',', '.')));
    }

    [Fact]
    public void Route_StartsRecordingWithTheFirstPedalStroke_UnderTheTypedName()
    {
        _training.BeginRoute(Hill(), customName: "Mein Hügel");
        Assert.EndsWith("Mein Hügel.json", _training.RecordingPath); // shown when closing before the first stroke

        Assert.False(_training.Update(Standing, T0));
        Assert.True(_training.Update(Pedalling(), T0.AddSeconds(1)));

        var recorder = _training.StartRecording();
        _training.Record(Pedalling());
        _training.StopRecording();

        Assert.Equal("Mein Hügel.json", Path.GetFileName(recorder.FilePath));
        var file = RecordingFile.Load(recorder.FilePath);
        Assert.Equal(RecordingFile.Kinds.Route, file.Kind);
        Assert.NotNull(file.Target?.Route);
        Assert.Contains(RecordingFile.Col.Grade, file.Columns);
        // The typed name is for one recording only.
        Assert.StartsWith("Route_Hügel_", _training.NextRecordingName);
    }

    [Fact]
    public void Program_PowerKeysShiftTheProfile()
    {
        var replay = _training.BeginReplay(ShortProgram(), customName: null, programFile: "S99_Kurz");

        Assert.Equal(120, _ergo.TargetWatt);
        _training.Step(+10);

        Assert.Equal(10, replay.Session.Offset);
        Assert.Equal(130, _ergo.TargetWatt);
        Assert.Equal("programm", replay.HelpSection);
        Assert.Equal(RecordingFile.Kinds.Program, replay.RecordingKind);
        Assert.StartsWith("Program_S99_Kurz_", _training.NextRecordingName);
        Assert.Null(replay.PulseThen(showOriginal: true)); // programs have no pulse of their own
    }

    [Fact]
    public void Program_FollowsTheProfile_AndReachesItsEnd()
    {
        _training.BeginReplay(ShortProgram(), customName: null, programFile: "S99_Kurz");
        Assert.True(_training.Update(Pedalling(), T0));
        _training.StartRecording();

        for (var s = 1; s <= 5; s++)
            _training.Update(Pedalling(), T0.AddSeconds(s));

        Assert.Equal(160, _ergo.TargetWatt); // second step of the profile
        Assert.False(_training.ReachedEnd);
        for (var s = 6; s <= 12; s++)
            _training.Update(Pedalling(), T0.AddSeconds(s));
        Assert.True(_training.ReachedEnd);
        Assert.Equal(("Training abgeschlossen", "Geschafft – Kurz"), _training.Mode.CompletedTitles);
        Assert.Contains(_training.Mode.Facts(_training.Recorder!), f => f.Label == "Gefahren" && f.Value.StartsWith("0:12"));
        Assert.False(_training.Update(Pedalling(), T0.AddSeconds(13))); // a finished profile records nothing new
    }

    [Fact]
    public void ReplayedRecording_ShowsThePulseOfThen()
    {
        var replay = _training.BeginReplay(RecordingWithPulse(), customName: null, programFile: null);

        Assert.Equal(RecordingFile.Kinds.Training, replay.RecordingKind);
        Assert.Equal("training", replay.HelpSection);
        Assert.Equal("Aufzeichnung", replay.RunName.Kind);
        Assert.Equal(130, replay.PulseThen(showOriginal: true));
        Assert.Null(replay.PulseThen(showOriginal: false));
        Assert.StartsWith("Training_", _training.NextRecordingName);
    }

    [Fact]
    public void RunSettings_ApplyDuringTheRun_AndAreDroppedAfterwards()
    {
        var run = _settings.Ride.Copy();
        run.MaxWattLimit = 150;

        _training.BeginReplay(RecordingWithPulse(), customName: null, programFile: null, run);

        Assert.Same(run, _training.Ride);
        Assert.Equal(150, _ergo.MaxWatt);
        Assert.Equal(150, _ergo.TargetWatt); // the profile's 150 W fits; more would be cut

        _training.EndMode();

        Assert.Same(_settings.Ride, _training.Ride);
        Assert.Equal(Math.Min(400, _settings.Ride.MaxWattLimit), _ergo.MaxWatt);
    }

    [Fact]
    public void EndMode_ClosesTheRecording_AndForgetsAnUnusedTypedName()
    {
        _training.BeginRoute(Hill(), customName: "Nie benutzt");
        _training.Update(Pedalling(), T0);
        var recorder = _training.StartRecording();
        _training.BeginRoute(Hill(), customName: "Nie benutzt");

        _training.EndMode();

        Assert.IsType<FreeTraining>(_training.Mode);
        Assert.Null(_training.Recorder);
        Assert.True(File.Exists(recorder.FilePath));
        Assert.StartsWith("Training_", _training.NextRecordingName); // not "Nie benutzt"
    }

    [Fact]
    public void StartRecording_InAFolderThatCannotBeCreated_KeepsTheTypedName()
    {
        var blocker = Path.Combine(TestFolder.New("blockiert"), "Aufzeichnungen");
        File.WriteAllText(blocker, ""); // a file where the folder should be
        _settings.RecordingDirectory = blocker;
        _training.RecordingFolders = () => [blocker]; // no fallback either
        _training.BeginRoute(Hill(), customName: "Zweiter Versuch");

        Assert.ThrowsAny<IOException>(() => _training.StartRecording());

        Assert.Null(_training.Recorder);
        Assert.Equal("Zweiter Versuch", _training.NextRecordingName);
    }

    [Fact]
    public void FailedRecording_IsNotRetriedWithEveryReading_UntilTheNextRun()
    {
        var blocker = Path.Combine(TestFolder.New("blockiert"), "Aufzeichnungen");
        File.WriteAllText(blocker, ""); // a file where the folder should be
        _settings.RecordingDirectory = blocker;
        _training.RecordingFolders = () => [blocker]; // no fallback either
        _training.BeginRoute(Hill(), customName: null);
        Assert.True(_training.Update(Pedalling(), T0));
        Assert.ThrowsAny<IOException>(() => _training.StartRecording());

        // Previously every reading asked for the recording again – and showed the error again.
        for (var s = 1; s <= 5; s++)
            Assert.False(_training.Update(Pedalling(), T0.AddSeconds(s)));
        Assert.Null(_training.Recorder);

        // The next route tries again (the folder may be back by then).
        _settings.RecordingDirectory = TestFolder.New("wieder_da");
        _training.RecordingFolders = _settings.RecordingFolders;
        _training.EndMode();
        _training.BeginRoute(Hill(), customName: null);
        Assert.True(_training.Update(Pedalling(), T0.AddSeconds(10)));
        Assert.NotNull(_training.StartRecording());
    }

    [Fact]
    public void SaveRideDefaults_InFreeTraining_AppliesTheNewPowerLimitAtOnce()
    {
        var values = _settings.Ride.Copy();
        values.MaxWattLimit = 200;

        _training.SaveRideDefaults(values);

        Assert.Equal(200, _settings.Ride.MaxWattLimit);
        Assert.NotSame(values, _settings.Ride); // a copy: later changes to the dialog's values don't leak in
        Assert.Equal(200, _ergo.MaxWatt);
    }
}
