namespace ErgoWattControl;

/// <summary>
/// A recording written by <see cref="CsvRecorder"/>, read back as a power profile over time (Soll_W) with the pulse
/// of that day. Any of our recordings works – free training or a route.
/// </summary>
public sealed class WorkoutRecording
{
    private readonly double[] _seconds;
    private readonly int[] _watts;
    private readonly int[] _pulses;

    public string Name { get; }
    public double Duration => _seconds[^1];
    public bool HasPulse { get; }
    public int MaxWatt { get; }
    public double AverageWatt { get; }
    public double AveragePulse { get; }
    public int Count => _seconds.Length;

    private WorkoutRecording(string name, List<(double Seconds, int Watt, int Pulse)> rows)
    {
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

    public static WorkoutRecording Load(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 3)
            throw new InvalidDataException("Die Datei enthält keine Messwerte.");
        var header = lines[0].TrimStart('﻿').Split(';');
        int Column(string name)
        {
            var index = Array.IndexOf(header, name);
            return index >= 0 ? index : throw new InvalidDataException($"Spalte \"{name}\" fehlt – ist das eine Aufzeichnung dieser App?");
        }
        var secondsColumn = Column("Sekunden");
        var wattColumn = Column("Soll_W");
        var pulseColumn = Column("Puls");

        var rows = new List<(double, int, int)>();
        var last = -1.0;
        foreach (var line in lines.Skip(1))
        {
            var cells = line.Split(';');
            if (cells.Length <= Math.Max(secondsColumn, Math.Max(wattColumn, pulseColumn))
                || !double.TryParse(cells[secondsColumn], out var seconds)
                || !int.TryParse(cells[wattColumn], out var watt))
                continue;
            if (seconds <= last)
                continue; // keep time strictly increasing
            int.TryParse(cells[pulseColumn], out var pulse);
            rows.Add((seconds, watt, pulse));
            last = seconds;
        }
        if (rows.Count < 2 || rows[^1].Item1 < 10)
            throw new InvalidDataException("Die Aufzeichnung ist kürzer als 10 Sekunden.");
        return new WorkoutRecording(Path.GetFileNameWithoutExtension(path), rows);
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
