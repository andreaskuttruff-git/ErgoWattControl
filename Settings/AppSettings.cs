using System.IO.Ports;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErgoWattControl;

/// <summary>Persisted user settings, stored as settings.json (see <see cref="AppPaths"/> for where).</summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Language of the user interface: "auto" (as Windows), "de" or "en" (<see cref="ErgoWattControl.Language"/>).</summary>
    public string Language { get; set; } = ErgoWattControl.Language.Auto;

    /// <summary>Kind of ergometer (<see cref="ErgometerTypes"/>); settings from before the device choice have none and mean the Daum cockpit.</summary>
    public string Device { get; set; } = ErgometerTypes.DaumRs232;
    /// <summary>COM port of the Daum cockpit.</summary>
    public string? PortName { get; set; }
    public string RecordingDirectory { get; set; } = AppPaths.DefaultRecordingFolder;

    /// <summary>Windows device name (e.g. \\.\DISPLAY1) of the monitor for the ride view; null = first non-primary monitor.</summary>
    public string? RideMonitor { get; set; }

    /// <summary>Weights, gearing, gradient, resistances and power limit: the values a ride is simulated with.</summary>
    // A hand-edited "Ride": null falls back to the defaults instead of breaking the start.
    public RideValues Ride { get => _ride; set => _ride = value ?? new(); }
    private RideValues _ride = new();

    /// <summary>
    /// File name patterns per recording type (without extension). Placeholders:
    /// {Date} = yyyy-MM-dd, {Time} = HH-mm-ss, {Route} = GPX file name, {Training} = replayed recording,
    /// {Program} = program file name (e.g. S02_FTP200_Norwegian_4x4). The German placeholders of v1.x
    /// ({Datum}, {Uhrzeit}, {Strecke}, {Programm}) are converted when the settings are loaded.
    /// Training covers free training and replaying a recording: both are the rider's own training.
    /// </summary>
    public string FileNameTraining { get => _fileNameTraining; set => _fileNameTraining = EnglishPlaceholders(value); }
    public string FileNameRoute { get => _fileNameRoute; set => _fileNameRoute = EnglishPlaceholders(value); }
    public string FileNameProgram { get => _fileNameProgram; set => _fileNameProgram = EnglishPlaceholders(value); }
    private string _fileNameTraining = "Training_{Date}_{Time}";
    private string _fileNameRoute = "Route_{Route}_{Date}_{Time}";
    private string _fileNameProgram = "Program_{Program}_{Date}_{Time}";

    /// <summary>The placeholders of v1.x in a file name pattern replaced by the current ones.</summary>
    public static string EnglishPlaceholders(string pattern) => (pattern ?? "")
        .Replace("{Datum}", "{Date}")
        .Replace("{Uhrzeit}", "{Time}")
        .Replace("{Strecke}", "{Route}")
        .Replace("{Programm}", "{Program}");

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

    // settings.json and recordings up to v1.5.1 kept the ride values at the top level: they are still read (into
    // Ride) but never written again – the next save moves them under "Ride".
    [JsonInclude, JsonPropertyName("RiderWeightKg"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private double? OldRiderWeightKg { get => null; set { if (value is { } v) Ride.RiderWeightKg = v; } }
    [JsonInclude, JsonPropertyName("BikeWeightKg"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private double? OldBikeWeightKg { get => null; set { if (value is { } v) Ride.BikeWeightKg = v; } }
    [JsonInclude, JsonPropertyName("GradeFactorPercent"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private int? OldGradeFactorPercent { get => null; set { if (value is { } v) Ride.GradeFactorPercent = v; } }
    [JsonInclude, JsonPropertyName("StartGear"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private int? OldStartGear { get => null; set { if (value is { } v) Ride.StartGear = v; } }
    [JsonInclude, JsonPropertyName("Gearing"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private string? OldGearing { get => null; set { if (value is { } v) Ride.Gearing = v; } }
    [JsonInclude, JsonPropertyName("RollingResistance"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private double? OldRollingResistance { get => null; set { if (value is { } v) Ride.RollingResistance = v; } }
    [JsonInclude, JsonPropertyName("DragAreaM2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private double? OldDragAreaM2 { get => null; set { if (value is { } v) Ride.DragAreaM2 = v; } }
    [JsonInclude, JsonPropertyName("MaxWattLimit"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private int? OldMaxWattLimit { get => null; set { if (value is { } v) Ride.MaxWattLimit = v; } }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile)) ?? new AppSettings();
                // Rewrite so options added in newer versions show up in the file with their defaults.
                loaded.TrySave();
                return loaded;
            }
        }
        catch (JsonException ex)
        {
            // Corrupt file: keep a copy for diagnosis, then fall back to defaults and overwrite it below.
            AppPaths.Log("settings.json damaged, using defaults", ex);
            try { File.Copy(AppPaths.SettingsFile, AppPaths.SettingsFile + ".defekt", overwrite: true); } catch (Exception) { }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable (locked, no permission): start with defaults but don't overwrite the file.
            AppPaths.Log("settings.json not readable, using defaults", ex);
            return Defaults();
        }

        var settings = Defaults();
        settings.TrySave();
        return settings;
    }

    private static AppSettings Defaults()
    {
        var ports = SerialPort.GetPortNames();
        return new AppSettings { PortName = ports.Contains("COM1") ? "COM1" : ports.FirstOrDefault() };
    }

    /// <summary>Save at start-up: a failure is logged, the app runs on with the settings in memory.</summary>
    private void TrySave()
    {
        try { Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppPaths.Log("Saving settings.json", ex);
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(AppPaths.SettingsFolder);
        // Write a temporary file and swap it in, so a crash or full disk never leaves a half-written settings.json.
        var temp = AppPaths.SettingsFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions));
        File.Move(temp, AppPaths.SettingsFile, overwrite: true);
    }

    /// <summary>A copy of the current values, e.g. to store with a recording.</summary>
    public AppSettings Snapshot() => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this))!;

    /// <summary>Fills a file name pattern; characters not allowed in file names are replaced by "_".</summary>
    /// <param name="route">Route, replayed recording or program that {Route}, {Training} and {Program} stand for.</param>
    public static string BuildFileName(string pattern, DateTime time, string? route)
    {
        var name = EnglishPlaceholders(pattern)
            .Replace("{Date}", time.ToString("yyyy-MM-dd"))
            .Replace("{Time}", time.ToString("HH-mm-ss"))
            .Replace("{Route}", route ?? "")
            .Replace("{Training}", route ?? "")
            .Replace("{Program}", route ?? "");
        return SanitizeFileName(name) ?? $"Recording_{time:yyyy-MM-dd_HH-mm-ss}";
    }

    /// <summary>
    /// Where recordings are written, in this order: the chosen folder; the default folder in Documents if another one is
    /// chosen (e.g. a network drive or USB stick that is missing); <see cref="AppPaths.FallbackRecordingFolder"/>.
    /// </summary>
    public IReadOnlyList<string> RecordingFolders()
    {
        var folders = new List<string>();
        foreach (var folder in new[] { RecordingDirectory, AppPaths.DefaultRecordingFolder, AppPaths.FallbackRecordingFolder })
        {
            if (!string.IsNullOrWhiteSpace(folder) && !folders.Any(f => SameFolder(f, folder)))
                folders.Add(folder);
        }
        return folders;
    }

    /// <summary>Recordings in all <see cref="RecordingFolders"/>, newest first; folders that are missing or unreadable are skipped.</summary>
    public IReadOnlyList<string> RecordingFiles(params string[] patterns)
    {
        var files = new List<string>();
        foreach (var folder in RecordingFolders())
        {
            try
            {
                if (Directory.Exists(folder))
                    files.AddRange(patterns.SelectMany(p => Directory.GetFiles(folder, p)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                AppPaths.Log($"Recordings in {folder} not readable", ex);
            }
        }
        return [.. files.OrderByDescending(File.GetLastWriteTime)];
    }

    /// <summary>The file lies in a fallback folder, not in the chosen one (shown as such in the lists).</summary>
    public bool InFallbackFolder(string file) => !SameFolder(Path.GetDirectoryName(file) ?? "", RecordingDirectory);

    public static bool SameFolder(string a, string b)
    {
        try
        {
            return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
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
