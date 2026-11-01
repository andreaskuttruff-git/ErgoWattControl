namespace ErgoWattControl;

static class Program
{
    private const string HelpText =
        """
        ErgoWattControl – Steuerung für Daum ergo_bike 8008 TRS

        Aufruf:
          ErgoWattControl.exe
              Normalbetrieb: verbindet sich mit dem Cockpit am eingestellten COM-Port.

          ErgoWattControl.exe -demo [GPX-Datei oder Aufzeichnung]
              Demo-Modus ohne Cockpit: ein simulierter Fahrer (ca. 75 RPM) ersetzt das
              Ergometer, die serielle Schnittstelle wird nicht benutzt.
              Mit GPX-Datei startet diese Strecke sofort, ohne Übersicht;
              mit einer Aufzeichnung (JSON/CSV) oder einem Programm wird dieses wiederholt.
              Beispiel: ErgoWattControl.exe -demo "GPX\Tour.gpx"

          ErgoWattControl.exe -main
              Startet auf dem Hauptbildschirm statt auf dem Fahrer-Monitor.
              "Fahransicht" schiebt das Fenster wie gewohnt auf den Fahrer-Monitor.
              Kombinierbar: ErgoWattControl.exe -main -demo

          ErgoWattControl.exe -help
              Zeigt diese Hilfe.

        Schreibweisen: -demo, --demo, /demo,  -main, --main, /main  sowie  -help, --help, -h, -?, /?
        Einstellungen: %AppData%\ErgoWattControl\settings.json, Aufzeichnungen, Strecken
        und Programme in Dokumente\ErgoWattControl. Liegt eine settings.json neben der exe,
        bleibt alles im Programmordner (portabel).
        """;

    /// <param name="args">"-demo [gpx/json/csv]": simulated cockpit, optionally starting a route or replay; "-main": open on the primary monitor; "-help": usage.</param>
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        // All dialogs dark like the ride view (and the Windows dark title bar); MessageBox stays as Windows draws it.
        Application.SetColorMode(SystemColorMode.Dark);

        bool demo = false, mainScreen = false;
        string? demoRoute = null;
        for (var i = 0; i < args.Length; i++)
        {
            // Accept the usual spellings (--demo, -demo, /demo) so a typo doesn't silently start the real cockpit mode.
            switch (args[i].TrimStart('-', '/').ToLowerInvariant())
            {
                case "demo":
                    demo = true;
                    // The file is the next argument unless that is another option.
                    if (i + 1 < args.Length && !args[i + 1].StartsWith('-') && !args[i + 1].StartsWith('/'))
                        demoRoute = args[++i];
                    break;
                // From the setup's "Starten" button: the checkbox value decides (1 = demo, 0 = normal start).
                case "demo:1":
                    demo = true;
                    break;
                case "demo:0":
                    break;
                case "main":
                    mainScreen = true;
                    break;
                case "help" or "h" or "?":
                    ShowHelp(null);
                    return;
                default:
                    ShowHelp($"Unbekannter Parameter: {args[i]}");
                    return;
            }
        }
        ProgramFiles.Install();
        Application.Run(new RideForm(AppSettings.Load(), demo, demoRoute, mainScreen));
        HelpWindow.Close();
    }

    /// <summary>
    /// Shown in a modal window rather than on the console: a WinExe doesn't make the console wait,
    /// so console output would appear after the next prompt.
    /// </summary>
    private static void ShowHelp(string? error)
    {
        // Own dialog instead of MessageBox: it wraps long lines early and breaks the indentation.
        using var form = new Form
        {
            Text = "ErgoWattControl – Parameter",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterScreen,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16),
            Font = new Font("Segoe UI", 10),
        };
        var layout = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill };
        if (error is not null)
            layout.Controls.Add(new Label { Text = error, AutoSize = true, ForeColor = DialogColors.Warning, Font = new Font("Segoe UI", 10, FontStyle.Bold), Margin = new Padding(0, 0, 0, 12) });
        layout.Controls.Add(new Label { Text = HelpText, AutoSize = true, Font = new Font("Consolas", 10) });
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(90, 0), Anchor = AnchorStyles.Right, Margin = new Padding(0, 16, 0, 0) };
        layout.Controls.Add(ok);
        form.Controls.Add(layout);
        form.AcceptButton = ok;
        form.CancelButton = ok;
        form.ShowDialog();
    }
}
