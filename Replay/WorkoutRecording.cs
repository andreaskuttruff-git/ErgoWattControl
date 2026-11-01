using System.Text.Json;

namespace ErgoWattControl;

/// <summary>
/// A recording read back as a power profile over time (Soll_W) with the pulse of that day – any of our recordings
/// (JSON from <see cref="JsonRecorder"/> or the older CSV), free training or a route – or a <see cref="TrainingProgram"/>.
/// </summary>
public sealed class WorkoutRecording
{
    private readonly double[] _seconds;
    private readonly int[] _watts;
    private readonly int[] _pulses;

    public string Name { get; }
    /// <summary>The file it was loaded from.</summary>
    public string? FilePath { get; private init; }
    public double Duration => _seconds[^1];
    public bool HasPulse { get; }
    public int MaxWatt { get; }
    public double AverageWatt { get; }
    public double AveragePulse { get; }
    public int Count => _seconds.Length;

    private WorkoutRecording(string name, List<(double Seconds, int Watt, int Pulse)> rows)
    {
        if (rows.Count < 2 || rows[^1].Seconds < 10)
            throw new InvalidDataException("Die Aufzeichnung ist kürzer als 10 Sekunden.");
        Name = name;
        _seconds = rows.Select(r => r.Seconds).ToArray();
        _watts = rows.Select(r => r.Watt).ToArray();
        _pulses = rows.Select(r => r.Pulse).ToArray();
        MaxWatt = _watts.Max();
        AverageWatt = _watts.Average();
        var pulses = _pulses.Where(p => p > 0).ToArray();
        HasPulse = pulses.Length > 0;
        AveragePulse = HasPulse ? pulses.Average() : 0;
    }

    /// <summary>Power profile without pulse, e.g. from a <see cref="TrainingProgram"/>.</summary>
    public static WorkoutRecording FromProfile(string name, IEnumerable<(double Seconds, int Watt)> points, string? filePath = null) =>
        new(name, Increasing(points.Select(p => (p.Seconds, p.Watt, 0)))) { FilePath = filePath };

    public static WorkoutRecording Load(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (!Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            return new WorkoutRecording(name, LoadCsv(path)) { FilePath = path };

        // Programs and recordings are both JSON: a recording has a table of measured values.
        using (var doc = JsonDocument.Parse(File.ReadAllText(path)))
        {
            if (!doc.RootElement.TryGetProperty("Werte", out _))
                return TrainingProgram.Load(path).ToWorkout();
        }
        var file = RecordingFile.Load(path);
        int Column(string column)
        {
            var index = Array.IndexOf(file.Spalten, column);
            return index >= 0 ? index : throw new InvalidDataException($"Spalte \"{column}\" fehlt – ist das eine Aufzeichnung dieser App?");
        }
        var (seconds, watt, pulse) = (Column("Sekunden"), Column("Soll_W"), Column("Puls"));
        var rows = file.Werte.Where(r => r.Length > Math.Max(seconds, Math.Max(watt, pulse)))
            .Select(r => (r[seconds], (int)r[watt], (int)r[pulse]));
        return new WorkoutRecording(name, Increasing(rows)) { FilePath = path };
    }

    /// <summary>The older recordings: CSV with semicolons.</summary>
    private static List<(double, int, int)> LoadCsv(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 3)
            throw new InvalidDataException("Die Datei enthält keine Messwerte.");
        var header = lines[0].TrimStart('\uFEFF').Split(';');
        int Column(string name)
        {
            var index = Array.IndexOf(header, name);
            return index >= 0 ? index : throw new InvalidDataException($"Spalte \"{name}\" fehlt – ist das eine Aufzeichnung dieser App?");
        }
        var secondsColumn = Column("Sekunden");
        var wattColumn = Column("Soll_W");
        var pulseColumn = Column("Puls");

        var rows = new List<(double, int, int)>();
        foreach (var line in lines.Skip(1))
        {
            var cells = line.Split(';');
            if (cells.Length <= Math.Max(secondsColumn, Math.Max(wattColumn, pulseColumn))
                || !double.TryParse(cells[secondsColumn], out var seconds)
                || !int.TryParse(cells[wattColumn], out var watt))
                continue;
            int.TryParse(cells[pulseColumn], out var pulse);
            rows.Add((seconds, watt, pulse));
        }
        return Increasing(rows);
    }

    /// <summary>Keeps time strictly increasing (a paused route repeats the same second).</summary>
    private static List<(double Seconds, int Watt, int Pulse)> Increasing(IEnumerable<(double Seconds, int Watt, int Pulse)> rows)
    {
        var result = new List<(double, int, int)>();
        foreach (var row in rows)
        {
            if (result.Count == 0 || row.Seconds > result[^1].Item1)
                result.Add(row);
        }
        return result;
    }

    /// <summary>The profile reduced to its changes (plus the end): where the target power steps to a new value.</summary>
    public IEnumerable<(double Seconds, int Watt)> Steps()
    {
        for (var i = 0; i < _seconds.Length; i++)
        {
            if (i == 0 || i == _seconds.Length - 1 || _watts[i] != _watts[i - 1])
                yield return (_seconds[i], _watts[i]);
        }
    }

    private int IndexAt(double seconds)
    {
        var i = Array.BinarySearch(_seconds, seconds);
        return i >= 0 ? i : Math.Max(0, ~i - 1);
    }

    /// <summary>Target power that was set at this point of the recording.</summary>
    public int WattAt(double seconds) => _watts[IndexAt(seconds)];

    /// <summary>Pulse at this point; 0 = no reading.</summary>
    public int PulseAt(double seconds) => _pulses[IndexAt(seconds)];

    /// <summary>Averages over the first <paramref name="seconds"/> of the recording, to compare a partly ridden replay fairly.</summary>
    public (double Watt, double Pulse) AveragesUntil(double seconds)
    {
        var end = IndexAt(seconds);
        var watt = _watts.Take(end + 1).Average();
        var pulses = _pulses.Take(end + 1).Where(p => p > 0).ToArray();
        return (watt, pulses.Length > 0 ? pulses.Average() : 0);
    }

    /// <summary>Next point after <paramref name="seconds"/> where the target power changes, or null if it stays until the end.</summary>
    public (double Seconds, int Watt)? NextChange(double seconds)
    {
        var i = IndexAt(seconds);
        var current = _watts[i];
        for (var j = i + 1; j < _watts.Length; j++)
        {
            if (_watts[j] != current)
                return (_seconds[j], _watts[j]);
        }
        return null;
    }
}
