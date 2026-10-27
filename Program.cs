using System.Runtime.InteropServices;

namespace ErgoWattControl;

static class Program
{
    private const string HelpText =
        """
        ErgoWattControl – Steuerung für Daum ergo_bike 8008 TRS

        Aufruf:
          ErgoWattControl.exe
              Normalbetrieb: verbindet sich mit dem Cockpit am eingestellten COM-Port.

          ErgoWattControl.exe -demo [GPX-Datei]
              Demo-Modus ohne Cockpit: ein simulierter Fahrer (ca. 75 RPM) ersetzt das
              Ergometer, die serielle Schnittstelle wird nicht benutzt.
              Mit GPX-Datei startet diese Strecke sofort, ohne Übersicht.
              Beispiel: ErgoWattControl.exe -demo "GPX\Tour.gpx"

          ErgoWattControl.exe -help
              Zeigt diese Hilfe.

        Schreibweisen: -demo, --demo, /demo  sowie  -help, --help, -h, -?, /?
        Einstellungen: settings.json neben der exe (wird beim ersten Start angelegt).
        """;

    /// <param name="args">"-demo [gpx-Datei]": simulated cockpit, optionally starting a route; "-help": usage.</param>
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Accept the usual spellings (--demo, -demo, /demo) so a typo doesn't silently start the real cockpit mode.
        var option = args.Length > 0 ? args[0].TrimStart('-', '/').ToLowerInvariant() : null;
        switch (option)
        {
            case null:
                Application.Run(new RideForm(AppSettings.Load()));
                break;
            case "demo":
                Application.Run(new RideForm(AppSettings.Load(), demo: true, demoRoute: args.Length > 1 ? args[1] : null));
                break;
            case "help" or "h" or "?":
                ShowHelp(null);
                break;
            default:
                ShowHelp($"Unbekannter Parameter: {args[0]}");
                break;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    /// <summary>
    /// A WinExe has no console of its own: when started from a console window the help is printed there,
    /// otherwise (shortcut, Explorer) it is shown in a message box.
    /// </summary>
    private static void ShowHelp(string? error)
    {
        var text = error is null ? HelpText : $"{error}\n\n{HelpText}";
        const int parentProcess = -1;
        if (AttachConsole(parentProcess))
        {
            Console.WriteLine();
            Console.WriteLine(text);
        }
        else
        {
            MessageBox.Show(text, "ErgoWattControl – Parameter", MessageBoxButtons.OK,
                error is null ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
    }
}
