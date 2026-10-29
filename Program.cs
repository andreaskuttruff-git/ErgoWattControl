namespace ErgoWattControl;

static class Program
{
    private const string HelpText =
        """
        ErgoWattControl – Steuerung für Daum ergo_bike 8008 TRS

        Aufruf:
          ErgoWattControl.exe
              Normalbetrieb: verbindet sich mit dem Cockpit am eingestellten COM-Port.

          ErgoWattControl.exe -demo [GPX- oder CSV-Datei]
              Demo-Modus ohne Cockpit: ein simulierter Fahrer (ca. 75 RPM) ersetzt das
              Ergometer, die serielle Schnittstelle wird nicht benutzt.
              Mit GPX-Datei startet diese Strecke sofort, ohne Übersicht;
              mit einer Aufzeichnung (CSV) wird dieses Training wiederholt.
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
            layout.Controls.Add(new Label { Text = error, AutoSize = true, ForeColor = Color.Firebrick, Font = new Font("Segoe UI", 10, FontStyle.Bold), Margin = new Padding(0, 0, 0, 12) });
        layout.Controls.Add(new Label { Text = HelpText, AutoSize = true, Font = new Font("Consolas", 10) });
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(90, 0), Anchor = AnchorStyles.Right, Margin = new Padding(0, 16, 0, 0) };
        layout.Controls.Add(ok);
        form.Controls.Add(layout);
        form.AcceptButton = ok;
        form.CancelButton = ok;
        form.ShowDialog();
    }
}
