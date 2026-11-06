namespace ErgoWattControl;

/// <summary>
/// The kinds of ergometer the app knows, as stored in settings.json (<see cref="AppSettings.Device"/>). A new kind of
/// device: implement <see cref="IErgometer"/>, add it here, and give the settings dialog its connection fields.
/// </summary>
public static class ErgometerTypes
{
    public const string DaumRs232 = "DaumRs232";
    public const string Demo = "Demo";

    public static readonly (string Id, string Name)[] All =
    [
        (DaumRs232, "Daum ergo_bike (RS232)"),
        (Demo, "Demo (simulierter Fahrer)"),
    ];

    /// <summary>Unknown or missing ids (settings from before the device choice) mean the Daum cockpit.</summary>
    public static IErgometer Create(string? id, AppSettings settings) => id switch
    {
        Demo => new DemoErgometer(),
        _ => new DaumErgometer(settings.PortName),
    };
}
