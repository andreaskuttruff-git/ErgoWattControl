namespace ErgoWattControl;

/// <summary>
/// Finds and connects a Bluetooth FTMS indoor bike: Windows' Bluetooth LE stack (<see cref="WinRtFtmsLink"/>), or a
/// simulated trainer in the unit tests.
/// </summary>
internal interface IFtmsLink
{
    /// <summary>
    /// Looks for an FTMS indoor bike (ErgoBridge first) and connects with notifications on. Null when none was found
    /// in time; throws <see cref="FtmsLinkException"/> when Bluetooth itself isn't usable.
    /// </summary>
    Task<IFtmsConnection?> ConnectAsync(CancellationToken cancel);
}

/// <summary>One connection to a trainer; events may come on any thread.</summary>
internal interface IFtmsConnection : IDisposable
{
    /// <summary>Name the trainer advertises, e.g. "ErgoBridge".</summary>
    string Name { get; }

    /// <summary>Whether the trainer is an ErgoBridge (has the bridge status characteristic, a Daum cockpit behind it).</summary>
    bool IsErgoBridge { get; }

    event Action<byte[]>? IndoorBikeData;
    event Action<byte[]>? ControlPointResponse;
    event Action<byte[]>? BridgeStatus;
    /// <summary>The connection is gone (out of range, switched off).</summary>
    event Action? Lost;

    Task<byte[]?> ReadPowerRangeAsync(CancellationToken cancel);
    /// <summary>Null for trainers that aren't an ErgoBridge.</summary>
    Task<byte[]?> ReadBridgeStatusAsync(CancellationToken cancel);
    Task WriteControlPointAsync(byte[] request, CancellationToken cancel);
}

/// <summary>
/// A known reason why riding over Bluetooth doesn't work right now (no adapter, radio off, control refused).
/// The message is for the status line (in the user's language), <see cref="LogText"/> for the English log.
/// </summary>
internal sealed class FtmsLinkException(string message, string logText) : IOException(message)
{
    public string LogText { get; } = logText;
}
