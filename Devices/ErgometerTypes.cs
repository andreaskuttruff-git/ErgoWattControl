namespace ErgoWattControl;

/// <summary>
/// One kind of ergometer: everything the app needs to know about it in one place – the id stored in settings.json, the
/// name in the settings dialog, how to create it, and which connection fields the dialog shows.
/// </summary>
/// <param name="Id">Stored in settings.json (<see cref="AppSettings.Device"/>); never change it once released.</param>
/// <param name="NameKey">Key of the name shown in the settings dialog (Localization\Strings.resx).</param>
/// <param name="Create">Creates the device from the settings without connecting yet (<see cref="IErgometer.Start"/> does that).</param>
/// <param name="UsesSerialPort">The device is connected via <see cref="AppSettings.PortName"/>: the dialog shows the COM port.</param>
/// <param name="TestConnection">
/// "Verbindung testen" for a port: returns a one-line result; gets the running device so it can report on its own port
/// instead of opening it a second time. Null = the dialog offers no test.
/// </param>
public sealed record ErgometerType(
    string Id,
    string NameKey,
    Func<AppSettings, IErgometer> Create,
    bool UsesSerialPort = false,
    Func<string, IErgometer?, string>? TestConnection = null)
{
    /// <summary>Whether switching from <paramref name="before"/> to <paramref name="after"/> needs a new connection.</summary>
    public bool ConnectionChanged(AppSettings before, AppSettings after) =>
        UsesSerialPort && before.PortName != after.PortName;

    /// <summary>Name in the language of the user interface.</summary>
    public string Name => L.T(NameKey);

    public override string ToString() => Name;
}

/// <summary>
/// The kinds of ergometer the app knows. A new kind of device: implement <see cref="IErgometer"/> (in its own folder
/// under Devices\), add one <see cref="ErgometerType"/> to <see cref="All"/>, and run the contract tests for it
/// (Tests\ErgometerContractTests.cs). Connection fields other than a COM port also need a row in the settings dialog.
/// </summary>
public static class ErgometerTypes
{
    public const string DaumRs232 = "DaumRs232";
    public const string Demo = "Demo";

    public static readonly IReadOnlyList<ErgometerType> All =
    [
        new(DaumRs232, "Device_DaumRs232", settings => new DaumErgometer(settings.PortName),
            UsesSerialPort: true,
            TestConnection: (port, running) => DaumErgometer.TestPort(port, running as DaumErgometer)),
        new(Demo, "Device_Demo", _ => new DemoErgometer()),
    ];

    /// <summary>The type with this id; unknown or missing ids (settings from before the device choice) mean the Daum cockpit.</summary>
    public static ErgometerType Find(string? id) => All.FirstOrDefault(t => t.Id == id) ?? All[0];

    public static IErgometer Create(string? id, AppSettings settings) => Find(id).Create(settings);
}
