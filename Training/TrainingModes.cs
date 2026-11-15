namespace ErgoWattControl;

/// <summary>
/// What differs between the three ways to ride: free training (the rider sets the power), a route (gears and gradient
/// set it) and a replay of a recording or training program (the profile sets it). <see cref="TrainingController"/>
/// holds one of them; the ride view only asks it, without its own mode switches.
/// </summary>
public abstract class TrainingMode
{
    /// <summary>The power comes from the route or profile, the rider only adjusts it; the recording starts by itself.</summary>
    public abstract bool Guided { get; }

    /// <summary>Kind of the recording file (<see cref="RecordingFile.Kinds"/>).</summary>
    public abstract string RecordingKind { get; }

    /// <summary>The route or power profile stored with the recording; null in free training.</summary>
    public virtual RecordingTarget? RecordingTarget => null;

    /// <summary>Section of the help that F1 opens.</summary>
    public abstract string HelpSection { get; }

    /// <summary>What is being ridden, shown above the pulse ("Strecke", "Tour (12.3 km)", file); empty in free training.</summary>
    public virtual (string Kind, string Name, string? File) RunName => ("", "", null);

    /// <summary>Name of the route, program or recording (end dialogs); empty in free training.</summary>
    public virtual string Name => "";

    /// <summary>The route or profile has been ridden to its end.</summary>
    public virtual bool Finished => false;

    /// <summary>Speed to show; null = the cockpit's own value.</summary>
    public virtual double? SpeedKmh => null;

    /// <summary>File name (without extension) the configured pattern gives for a recording started now.</summary>
    public abstract string DefaultFileName(AppSettings settings, DateTime now);

    /// <summary>Advances with a new reading; returns the power the cockpit should now hold, or null to keep it.</summary>
    public virtual int? Update(ErgometerData d, DateTime now) => null;

    /// <summary>A power key (±5 or ±25 W): changes the target, the gear or the profile offset.</summary>
    public abstract void Step(int step, ErgoController ergo);

    /// <summary>Appends a reading to the recording.</summary>
    public abstract void Write(JsonRecorder recorder, ErgometerData d, int targetWatt);

    /// <summary>Pulse of the original ride at the same point; null if there is none (or it is switched off).</summary>
    public virtual int? PulseThen(bool showOriginal) => null;

    /// <summary>Result lines of the end dialog.</summary>
    public virtual List<(string Label, string Value)> Facts(JsonRecorder recorded) => [];

    /// <summary>Title and heading of the end dialog after reaching the end.</summary>
    public virtual (string Title, string Heading) CompletedTitles => ("Training abgeschlossen", Name);

    protected static void AddTotals(List<(string, string)> facts, JsonRecorder recorded)
    {
        facts.Add(("Energie", $"{recorded.EnergyKJ:0} kJ"));
        facts.Add(("Umdrehungen", $"{recorded.Revolutions:0}"));
    }
}

/// <summary>The rider sets the power; recording on demand, it keeps running through pauses.</summary>
public sealed class FreeTraining : TrainingMode
{
    public override bool Guided => false;
    public override string RecordingKind => RecordingFile.Kinds.Training;
    public override string HelpSection => "ansicht";

    public override string DefaultFileName(AppSettings settings, DateTime now) =>
        AppSettings.BuildFileName(settings.FileNameTraining, now, null);

    public override void Step(int step, ErgoController ergo) => ergo.SetTargetWatt(ergo.TargetWatt + step);

    public override void Write(JsonRecorder recorder, ErgometerData d, int targetWatt) => recorder.Write(d, targetWatt);
}

/// <summary>A GPX/FIT route: gradient, weight and gear set the power; the power keys shift gears (5 W ≙ 1 gear).</summary>
public sealed class RouteTraining(RouteSession session) : TrainingMode
{
    public RouteSession Session { get; } = session;

    public override bool Guided => true;
    public override string RecordingKind => RecordingFile.Kinds.Route;
    public override RecordingTarget? RecordingTarget => RecordingTarget.FromRoute(Session.Route);
    public override string HelpSection => "strecke";
    public override string Name => Session.Route.Name;
    public override (string Kind, string Name, string? File) RunName =>
        ("Strecke", $"{Session.Route.Name} ({Session.Route.TotalDistance / 1000:0.0} km)", Session.Route.FilePath);
    public override bool Finished => Session.Finished;
    public override double? SpeedKmh => Session.SpeedKmh;
    public override (string Title, string Heading) CompletedTitles => ("Ziel erreicht", $"Ziel erreicht – {Name}");

    public override string DefaultFileName(AppSettings settings, DateTime now) =>
        AppSettings.BuildFileName(settings.FileNameRoute, now, Session.Route.Name);

    public override int? Update(ErgometerData d, DateTime now) => Session.Update(d, now);

    public override void Step(int step, ErgoController ergo) => Session.Shift(step / 5);

    // Only pedalling time counts; distance and speed come from the virtual ride.
    public override void Write(JsonRecorder recorder, ErgometerData d, int targetWatt) =>
        recorder.Write(d, targetWatt, counting: d.Rpm > 0,
            new RouteRow(Session.Position / 1000, Session.GradePercent, Session.Gear, Session.SpeedKmh));

    public override int? PulseThen(bool showOriginal) => showOriginal ? Session.Route.OriginalPulseAt(Session.Position) : null;

    public override List<(string Label, string Value)> Facts(JsonRecorder recorded)
    {
        var facts = new List<(string, string)>
        {
            ("Strecke", $"{Session.Position / 1000:0.0} von {Session.Route.TotalDistance / 1000:0.0} km"),
            ("Fahrzeit", $"{Session.MovingTime:h\\:mm\\:ss}"),
        };
        if (Session.MovingTime.TotalSeconds > 0)
            facts.Add(("Ø Geschwindigkeit", $"{Session.Position / Session.MovingTime.TotalSeconds * 3.6:0.0} km/h"));
        facts.Add(("Ø Leistung", $"{Session.AverageWatt:0} W"));
        if (Session.AveragePulse > 0)
            facts.Add(("Ø Puls", $"{Session.AveragePulse:0}"));
        AddTotals(facts, recorded);
        return facts;
    }
}

/// <summary>
/// A recording or training program ridden again: its power profile sets the power, the power keys shift the whole
/// profile up or down.
/// </summary>
/// <param name="programFile">File name (without .json) of a training program; null for a replayed recording.</param>
public sealed class ReplayTraining(ReplaySession session, string? programFile) : TrainingMode
{
    public ReplaySession Session { get; } = session;
    public string? ProgramFile { get; } = programFile;
    public bool IsProgram => ProgramFile is not null;

    public override bool Guided => true;
    public override string RecordingKind => IsProgram ? RecordingFile.Kinds.Program : RecordingFile.Kinds.Training;
    public override RecordingTarget? RecordingTarget => RecordingTarget.FromWorkout(Session.Workout);
    public override string HelpSection => IsProgram ? "programm" : "training";
    public override string Name => Session.Workout.Name;
    public override (string Kind, string Name, string? File) RunName =>
        (IsProgram || Session.Workout.IsProgram ? "Programm" : "Aufzeichnung", Session.Workout.Name, Session.Workout.FilePath);
    public override bool Finished => Session.Finished;
    public override (string Title, string Heading) CompletedTitles => ("Training abgeschlossen", $"Geschafft – {Name}");

    public override string DefaultFileName(AppSettings settings, DateTime now) => IsProgram
        ? AppSettings.BuildFileName(settings.FileNameProgram, now, ProgramFile)
        : AppSettings.BuildFileName(settings.FileNameTraining, now, Session.Workout.Name);

    public override int? Update(ErgometerData d, DateTime now) => Session.Update(d, now);

    public override void Step(int step, ErgoController ergo) => ergo.SetTargetWatt(Session.ShiftOffset(step));

    public override void Write(JsonRecorder recorder, ErgometerData d, int targetWatt) =>
        recorder.Write(d, targetWatt, counting: d.Rpm > 0);

    // Programs have no pulse of their own.
    public override int? PulseThen(bool showOriginal) =>
        showOriginal && Session.Workout is { HasPulse: true, IsProgram: false } workout
        && workout.PulseAt(Session.Elapsed.TotalSeconds) is var p and > 0 ? p : null;

    public override List<(string Label, string Value)> Facts(JsonRecorder recorded)
    {
        // "damals" refers to the same section of the original, so a replay stopped early compares fairly.
        var then = Session.Workout.AveragesUntil(Session.Elapsed.TotalSeconds);
        var facts = new List<(string, string)>
        {
            ("Gefahren", $"{WorkoutProfile.Format(Session.Elapsed.TotalSeconds)} von {WorkoutProfile.Format(Session.Workout.Duration)}"),
            ("Ø Leistung", $"{Session.AverageWatt:0} W   (damals Ø {then.Watt:0} W)"),
        };
        if (Session.Offset != 0)
            facts.Add(("Profil verschoben", $"{Session.Offset:+0;-0} W"));
        if (Session.AveragePulse > 0)
            facts.Add(("Ø Puls", then.Pulse > 0
                ? $"{Session.AveragePulse:0}   (damals Ø {then.Pulse:0})"
                : $"{Session.AveragePulse:0}"));
        AddTotals(facts, recorded);
        return facts;
    }
}
