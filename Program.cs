namespace ErgoWattControl;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    /// <param name="args">"--demo [gpx-Datei]": simulated cockpit instead of the serial port, optionally starting a route right away.</param>
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var demo = args.Length > 0 && args[0] == "--demo";
        var demoRoute = demo && args.Length > 1 ? args[1] : null;
        Application.Run(new RideForm(AppSettings.Load(), demo, demoRoute));
    }
}