using System.IO.Ports;
using System.Text.Json;

namespace ErgoWattControl;

/// <summary>Persisted user settings, stored as settings.json (see <see cref="AppPaths"/> for where).</summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Kind of ergometer (<see cref="ErgometerTypes"/>); settings from before the device choice have none and mean the Daum cockpit.</summary>
    public string Device { get; set; } = ErgometerTypes.DaumRs232;
    /// <summary>COM port of the Daum cockpit.</summary>
    public string? PortName { get; set; }
    public string RecordingDirectory { get; set; } = Path.Combine(AppPaths.DataFolder, "Aufzeichnungen");

    /// <summary>Windows device name (e.g. \\.\DISPLAY1) of the monitor for the ride view; null = first non-primary monitor.</summary>
    public string? RideMonitor { get; set; }

    /// <summary>Upper power limit in W; the cockpit type may allow less (8008/8080: 800 W, others 400 W).</summary>
    public int MaxWattLimit { get; set; } = ErgoController.AbsoluteMaxWatt;

    /// <summary>
    /// File name patterns per recording type (without extension). Placeholders:
    /// {Datum} = yyyy-MM-dd, {Uhrzeit} = HH-mm-ss, {Strecke} = GPX file name, {Training} = replayed recording,
    /// {Programm} = program file name (e.g. S02_FTP200_Norwegisch_4x4).
    /// Training covers free training and replaying a recording: both are the rider's own training.
    /// </summary>
    public string FileNameTraining { get; set; } = "Training_{Datum}_{Uhrzeit}";
    public string FileNameRoute { get; set; } = "Strecke_{Strecke}_{Datum}_{Uhrzeit}";
    public string FileNameProgram { get; set; } = "Prog_{Programm}_{Datum}_{Uhrzeit}";

    /// <summary>"Aufzeichnung exportieren": formats ticked last time and the target folder (null = Export\ in the recording folder).</summary>
    public bool ExportFit { get; set; } = true;
    public bool ExportCsv { get; set; }
    public string? ExportDirectory { get; set; }

    /// <summary>Open Garmin Connect / Strava after exporting (FIT) to upload the files there (<see cref="UploadWindow"/>).</summary>
    public bool ExportOpenGarmin { get; set; }
    public bool ExportOpenStrava { get; set; }
    /// <summary>Platforms switched on in the settings; only these get a checkbox in the export dialog.</summary>
    public bool GarminEnabled { get; set; }
    public bool StravaEnabled { get; set; }
    /// <summary>Sign-in pages, used while not signed in (no saved session in the platform's window).</summary>
    public string GarminLoginUrl { get; set; } = UploadService.GarminDefaultLoginUrl;
    public string StravaLoginUrl { get; set; } = UploadService.StravaDefaultLoginUrl;
    /// <summary>Upload pages, opened directly once signed in (Garmin: with "Daten speichern").</summary>
    public string GarminImportUrl { get; set; } = UploadService.GarminDefaultImportUrl;
    public string StravaUploadUrl { get; set; } = UploadService.StravaDefaultUploadUrl;

    /// <summary>Show the pulse of the original recording while replaying it (only if it contains pulse data).</summary>
    public bool ReplayShowOriginalPulse { get; set; } = true;

    public string GpxDirectory { get; set; } = Path.Combine(AppPaths.DataFolder, "GPX");
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
            if (File.Exists(AppPaths.SettingsFile))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile)) ?? new AppSettings();
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

    public void Save()
    {
        Directory.CreateDirectory(AppPaths.SettingsFolder);
        File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(this, JsonOptions));
    }

    /// <summary>Takes over the ride values (weights, gearing, grade factor, resistances, power limit) of <paramref name="other"/>.</summary>
    public void CopyRideValuesFrom(AppSettings other)
    {
        RiderWeightKg = other.RiderWeightKg;
        BikeWeightKg = other.BikeWeightKg;
        GradeFactorPercent = other.GradeFactorPercent;
        Gearing = other.Gearing;
        StartGear = other.StartGear;
        RollingResistance = other.RollingResistance;
        DragAreaM2 = other.DragAreaM2;
        MaxWattLimit = other.MaxWattLimit;
    }

    /// <summary>A copy of the current values, e.g. to store with a recording.</summary>
    public AppSettings Snapshot() => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this))!;

    /// <summary>Fills a file name pattern; characters not allowed in file names are replaced by "_".</summary>
    /// <param name="route">Route, replayed recording or program that {Strecke}, {Training} and {Programm} stand for.</param>
    public static string BuildFileName(string pattern, DateTime time, string? route)
    {
        var name = pattern
            .Replace("{Datum}", time.ToString("yyyy-MM-dd"))
            .Replace("{Uhrzeit}", time.ToString("HH-mm-ss"))
            .Replace("{Strecke}", route ?? "")
            .Replace("{Training}", route ?? "")
            .Replace("{Programm}", route ?? "");
        return SanitizeFileName(name) ?? $"Aufnahme_{time:yyyy-MM-dd_HH-mm-ss}";
    }

    /// <summary>Replaces characters not allowed in file names by "_" and drops a typed ".json"/".csv"; null if nothing is left.</summary>
    public static string? SanitizeFileName(string name)
    {
        name = name.Trim();
        foreach (var extension in new[] { JsonRecorder.Extension, ".csv" })
        {
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                name = name[..^extension.Length];
        }
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
