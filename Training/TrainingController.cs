namespace ErgoWattControl;

/// <summary>
/// The training running now, without any window: the mode (<see cref="FreeTraining"/>, <see cref="RouteTraining"/>,
/// <see cref="ReplayTraining"/>), its recording, the settings changed for this one run and the file name typed in a
/// start dialog. The ride view feeds it the readings and power keys and shows what it reports; dialogs stay there.
/// </summary>
public sealed class TrainingController
{
    private readonly AppSettings _settings;
    private readonly ErgoController _ergo;
    /// <summary>Ride values for the running route or replay when the start dialog changed them for this run; null = the saved ones.</summary>
    private RideValues? _runValues;
    /// <summary>Recording name typed in a start dialog, waiting for the first pedal stroke.</summary>
    private string? _customFileName;

    public TrainingController(AppSettings settings, ErgoController ergo)
    {
        _settings = settings;
        _ergo = ergo;
    }

    public TrainingMode Mode { get; private set; } = new FreeTraining();

    /// <summary>The running recording; null when nothing is recorded.</summary>
    public JsonRecorder? Recorder { get; private set; }

    /// <summary>A route or a replayed training sets the power; free training is controlled by the rider.</summary>
    public bool Guided => Mode.Guided;

    /// <summary>The ride values this run uses: the start dialog's for this run, otherwise the saved ones.</summary>
    public RideValues Ride => _runValues ?? _settings.Ride;

    /// <summary>Starts a route (from free training; a free recording still running is closed first).</summary>
    public RouteTraining BeginRoute(GpxRoute route, string? customName, RideValues? runValues = null)
    {
        Prepare(customName, runValues);
        var mode = new RouteTraining(new RouteSession(route, Ride, ErgoController.MinWatt, _ergo.MaxWatt));
        Mode = mode;
        _ergo.SetTargetWatt(mode.Session.TargetWatt);
        return mode;
    }

    /// <summary>Starts a replay of a recording or, with <paramref name="programFile"/>, a training program.</summary>
    public ReplayTraining BeginReplay(WorkoutRecording workout, string? customName, string? programFile, RideValues? runValues = null)
    {
        Prepare(customName, runValues);
        var mode = new ReplayTraining(new ReplaySession(workout, ErgoController.MinWatt, _ergo.MaxWatt), programFile);
        Mode = mode;
        _ergo.SetTargetWatt(mode.Session.TargetWatt);
        return mode;
    }

    private void Prepare(string? customName, RideValues? runValues)
    {
        StopRecording();
        _customFileName = customName;
        UseRunValues(runValues);
    }

    /// <summary>Back to free training: recording closed, the saved settings (and power limit) apply again.</summary>
    public void EndMode()
    {
        StopRecording();
        Mode = new FreeTraining();
        _customFileName = null;
        UseRunValues(null);
    }

    private void UseRunValues(RideValues? runValues)
    {
        _runValues = runValues;
        _ergo.SetUserLimit(Ride.MaxWattLimit);
    }

    /// <summary>A power key: target power, gear or profile offset, depending on the mode.</summary>
    public void Step(int step) => Mode.Step(step, _ergo);

    /// <summary>
    /// A new reading: advances the route or profile and passes its power to the cockpit. Returns true when a guided
    /// training should start its recording now (first pedal stroke, so getting on the bike isn't recorded).
    /// </summary>
    public bool Update(ErgometerData d, DateTime now)
    {
        if (Mode.Update(d, now) is { } watt)
            _ergo.SetTargetWatt(watt);
        return Recorder is null && Guided && d.Rpm > 0 && !Mode.Finished;
    }

    /// <summary>Appends the reading to the recording, if one runs; file errors are thrown (the caller stops the recording).</summary>
    public void Record(ErgometerData d)
    {
        if (Recorder is { } recorder)
            Mode.Write(recorder, d, _ergo.TargetWatt);
    }

    /// <summary>The route or profile has been ridden to its end while recording: time for the end dialog.</summary>
    public bool ReachedEnd => Recorder is not null && Mode.Finished;

    /// <summary>File name (without extension) the next recording gets: the typed one, otherwise the pattern's.</summary>
    public string NextRecordingName => _customFileName ?? Mode.DefaultFileName(_settings, DateTime.Now);

    /// <summary>The running recording's file, or where one would be written (shown when closing before the first stroke).</summary>
    public string RecordingPath => Recorder?.FilePath
        ?? Path.Combine(_settings.RecordingDirectory, NextRecordingName + JsonRecorder.Extension);

    /// <summary>Creates the recording file; throws if it can't be written (the typed name is kept for another try).</summary>
    public JsonRecorder StartRecording()
    {
        // The recording keeps the settings it was ridden with, including the ride values changed for this run.
        var settings = _settings.Snapshot();
        settings.Ride = Ride.Copy();
        var recorder = new JsonRecorder(_settings.RecordingDirectory, NextRecordingName, Mode.RecordingKind, settings, Mode.RecordingTarget);
        // A name typed in the start dialog is used for this one recording only.
        _customFileName = null;
        Recorder = recorder;
        return recorder;
    }

    /// <summary>Closes the recording; never throws (a failure is logged), afterwards nothing is recorded any more.</summary>
    /// <returns>The closed recording, or null if none ran.</returns>
    public JsonRecorder? StopRecording()
    {
        if (Recorder is not { } recorder)
            return null;
        // Forget the recorder first: even if closing the file fails, the view must leave the recording state
        // (otherwise every following second would hit the broken recorder again).
        Recorder = null;
        try
        {
            recorder.Dispose();
        }
        catch (Exception ex)
        {
            AppPaths.Log("Aufzeichnung schließen", ex);
        }
        return recorder;
    }

    /// <summary>"Als Standard speichern" in a start dialog: the ride values become the saved settings.</summary>
    public void SaveRideDefaults(RideValues values)
    {
        _settings.Ride = values.Copy();
        try { _settings.Save(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        if (!Guided)
            _ergo.SetUserLimit(_settings.Ride.MaxWattLimit);
    }
}
