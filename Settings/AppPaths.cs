namespace ErgoWattControl;

/// <summary>
/// Where the app keeps its files. Normally in the Windows folders – settings in %AppData%\ErgoWattControl, recordings,
/// routes and programs in Documents\ErgoWattControl – so it can be installed anywhere (also read-only). A settings.json
/// next to the exe switches to portable mode: everything stays next to the exe, as in earlier versions (KellerPC, ZIP).
/// </summary>
public static class AppPaths
{
    private const string Name = "ErgoWattControl";

    /// <summary>True if a settings.json lies next to the exe: settings and data stay in the app folder.</summary>
    public static bool Portable { get; } = File.Exists(Path.Combine(AppContext.BaseDirectory, "settings.json"));

    /// <summary>Folder of settings.json and the log (<see cref="LogFile"/>).</summary>
    public static string SettingsFolder { get; private set; } = Portable
        ? AppContext.BaseDirectory
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Name);

    /// <summary>Default parent of Aufzeichnungen\, GPX\ and Programme\.</summary>
    public static string DataFolder { get; private set; } = Portable
        ? AppContext.BaseDirectory
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Name);

    /// <summary>
    /// Unit tests only: keeps settings, the log and data in <paramref name="folder"/>, so a test never touches the
    /// rider's real settings or recordings.
    /// </summary>
    internal static void UseFolderForTests(string folder)
    {
        SettingsFolder = folder;
        DataFolder = folder;
        FallbackRecordingFolder = Path.Combine(folder, "Ersatzordner");
    }

    /// <summary>
    /// Last resort for recordings when the chosen folder (and the default one) can't be written: in the rider's own
    /// profile, %LocalAppData%\ErgoWattControl\Recordings – not Temp, which Windows empties without asking.
    /// </summary>
    public static string FallbackRecordingFolder { get; private set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Name, "Recordings");

    public static string SettingsFile => Path.Combine(SettingsFolder, "settings.json");

    /// <summary>Errors and connection notes, in English whatever the app's language (for troubleshooting, not for the rider).</summary>
    public static string LogFile => Path.Combine(SettingsFolder, "ErgoWattControl.log");

    /// <summary>Default folder for recordings below <see cref="DataFolder"/>.</summary>
    public static string DefaultRecordingFolder => Path.Combine(DataFolder, "Recordings");

    /// <summary>Appends an error with its details to the log; never throws.</summary>
    public static void Log(string what, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            File.AppendAllText(LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {what}\r\n{ex}\r\n\r\n");
        }
        catch (Exception) { }
    }

    /// <summary>Appends a one-line note (no exception) to the log, e.g. the cockpit connection coming back; never throws.</summary>
    public static void Note(string what)
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            File.AppendAllText(LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {what}\r\n");
        }
        catch (Exception) { }
    }
}
