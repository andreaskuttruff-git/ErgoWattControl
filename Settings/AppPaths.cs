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

    /// <summary>Folder of settings.json and Fehler.log.</summary>
    public static string SettingsFolder { get; } = Portable
        ? AppContext.BaseDirectory
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Name);

    /// <summary>Default parent of Aufzeichnungen\, GPX\ and Programme\.</summary>
    public static string DataFolder { get; } = Portable
        ? AppContext.BaseDirectory
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Name);

    public static string SettingsFile => Path.Combine(SettingsFolder, "settings.json");

    public static string LogFile => Path.Combine(SettingsFolder, "Fehler.log");

    /// <summary>Appends an error with its details to Fehler.log; never throws.</summary>
    public static void Log(string what, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            File.AppendAllText(LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {what}\r\n{ex}\r\n\r\n");
        }
        catch (Exception) { }
    }

    /// <summary>Appends a one-line note (no exception) to Fehler.log, e.g. the cockpit connection coming back; never throws.</summary>
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
