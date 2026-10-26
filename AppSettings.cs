using System.IO.Ports;
using System.Text.Json;

namespace ErgoWattControl;

/// <summary>Persisted user settings, stored as settings.json next to the executable.</summary>
public sealed class AppSettings
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "settings.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string? PortName { get; set; }
    public string RecordingDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Aufzeichnungen");

    /// <summary>Windows device name (e.g. \\.\DISPLAY1) of the monitor for the ride view; null = first non-primary monitor.</summary>
    public string? RideMonitor { get; set; }

    /// <summary>
    /// CSV file name patterns per recording type (without .csv). Placeholders:
    /// {Datum} = yyyy-MM-dd, {Uhrzeit} = HH-mm-ss, {Strecke} = GPX file name.
    /// </summary>
    public string FileNameFreeTraining { get; set; } = "Training_{Datum}_{Uhrzeit}";
    public string FileNameRoute { get; set; } = "Strecke_{Strecke}_{Datum}_{Uhrzeit}";

    public string GpxDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "GPX");
    public double RiderWeightKg { get; set; } = 135;
    public double BikeWeightKg { get; set; } = 11;
    /// <summary>Percentage of the real gradient that is ridden (100 = original, 50 = half as steep).</summary>
    public int GradeFactorPercent { get; set; } = 100;
    public int StartGear { get; set; } = 8;
    /// <summary>Gear table for route riding: "Rennrad" (daum table, 42:24 – 53:12) or "MTB" (moderately lower gears).</summary>
    public string Gearing { get; set; } = "MTB";
    /// <summary>Physics constants, only editable in this file: MTB tyres on mixed surface, upright rider.</summary>
    public double RollingResistance { get; set; } = 0.010;
    public double DragAreaM2 { get; set; } = 0.55;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
                // Rewrite so options added in newer versions show up in the file with their defaults.
                try { loaded.Save(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                return loaded;
            }
        }
        catch (JsonException)
        {
            // Corrupt file: fall back to defaults and overwrite it below.
        }

        var ports = SerialPort.GetPortNames();
        var settings = new AppSettings { PortName = ports.Contains("COM1") ? "COM1" : ports.FirstOrDefault() };
        settings.Save();
        return settings;
    }

    public void Save() => File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));

    /// <summary>Fills a file name pattern; characters not allowed in file names are replaced by "_".</summary>
    public static string BuildFileName(string pattern, DateTime time, string? route)
    {
        var name = pattern
            .Replace("{Datum}", time.ToString("yyyy-MM-dd"))
            .Replace("{Uhrzeit}", time.ToString("HH-mm-ss"))
            .Replace("{Strecke}", route ?? "");
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? $"Aufnahme_{time:yyyy-MM-dd_HH-mm-ss}" : name;
    }
}
