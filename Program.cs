namespace ErgoWattControl;

static class Program
{
    /// <param name="args">"-demo [gpx/json/csv]": simulated cockpit, optionally starting a route or replay; "-main": open on the primary monitor; "-help": usage.</param>
    [STAThread]
    static void Main(string[] args)
    {
        // Last line of defence: an unexpected error in an event handler is logged and reported instead of
        // ending the app mid-training without a trace; the app keeps running (the log has the stack trace).
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportUnexpected(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                AppPaths.Log("Unhandled error (app ends)", ex);
        };
        ApplicationConfiguration.Initialize();
        // Settings first: they set the language of everything that follows (also the -help window).
        var settings = AppSettings.Load();
        Language.Apply(settings.Language);
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
                    ShowHelp(L.F("Start_UnknownParameter", args[i]));
                    return;
            }
        }
        ProgramFiles.Install();
        Application.Run(new RideForm(settings, demo, demoRoute, mainScreen));
        HelpWindow.Close();
    }

    private static bool _reporting;

    /// <summary>Logs an error that escaped an event handler and tells the rider once (no stacked message boxes).</summary>
    private static void ReportUnexpected(Exception ex)
    {
        AppPaths.Log("Unexpected error", ex);
        if (_reporting)
            return;
        _reporting = true;
        try
        {
            MessageBox.Show(L.F("Error_Unexpected", ex.Message, AppPaths.LogFile),
                "ErgoWattControl", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception) { /* nothing left to report with */ }
        finally { _reporting = false; }
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
            Text = L.T("Start_HelpTitle"),
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
        layout.Controls.Add(new Label { Text = L.T("Start_Help"), AutoSize = true, Font = new Font("Consolas", 10) });
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(90, 0), Anchor = AnchorStyles.Right, Margin = new Padding(0, 16, 0, 0) };
        layout.Controls.Add(ok);
        form.Controls.Add(layout);
        form.AcceptButton = ok;
        form.CancelButton = ok;
        form.ShowDialog();
    }
}
