using System.IO.Ports;

namespace ErgoWattControl;

public sealed record RunData
{
    public int Program { get; init; }
    public int Person { get; init; }
    public bool Pedaling { get; init; }
    public int Watt { get; init; }
    public int Rpm { get; init; }
    public int SpeedKmh { get; init; }
    public double DistanceKm { get; init; }
    public int TimeSeconds { get; init; }
    public double EnergyKJ { get; init; }
    public int Pulse { get; init; }
    public int PulseState { get; init; }
    public int Gear { get; init; }
}

/// <summary>
/// Talks the daum electronic "ergo_bike" RS232 protocol (see interface_2003_offiz191203.pdf).
/// Only the subset needed for free training (Programm 0: watt control + telemetry) is implemented.
/// </summary>
public sealed class DaumCockpit : IDisposable
{
    // Cockpit needs at least ~50ms between packets or it can corrupt its own state.
    private static readonly TimeSpan MinGap = TimeSpan.FromMilliseconds(60);

    private readonly SerialPort _port;
    private DateTime _lastSend = DateTime.MinValue;

    public byte Address { get; private set; }

    public DaumCockpit(string portName)
    {
        _port = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 1200,
            WriteTimeout = 500,
            // Some daum update cables draw their supply from the handshake lines.
            DtrEnable = true,
            RtsEnable = true,
        };
        _port.Open();
    }

    public byte QueryAddress()
    {
        var reply = Transact([0x11], 2);
        Address = reply[1];
        return Address;
    }

    public void Dispose() => _port.Dispose();

    private void WaitForMinGap()
    {
        var remaining = MinGap - (DateTime.UtcNow - _lastSend);
        if (remaining > TimeSpan.Zero)
            Thread.Sleep(remaining);
    }

    private byte[] Transact(byte[] request, int expectedReplyLength)
    {
        WaitForMinGap();
        _port.DiscardInBuffer();
        _port.Write(request, 0, request.Length);
        _lastSend = DateTime.UtcNow;

        var buffer = new byte[expectedReplyLength];
        var offset = 0;
        while (offset < expectedReplyLength)
        {
            var read = _port.Read(buffer, offset, expectedReplyLength - offset);
            offset += read;
        }
        return buffer;
    }

    /// <summary>Returns the cockpit firmware/version byte; throws TimeoutException on very old cockpits that ignore this command.</summary>
    public byte CheckCockpit()
    {
        var reply = Transact([0x10, Address], 3);
        return reply[2];
    }

    /// <summary>Switches the cockpit to Programm 0 (freies Training) so it accepts Set_Watt from the PC.</summary>
    public void SelectFreeTraining() => Transact([0x23, Address, 0x00], 4);

    public bool StartProgram()
    {
        var reply = Transact([0x21, Address], 3);
        return reply[2] == 0x01;
    }

    public bool StopProgram()
    {
        var reply = Transact([0x22, Address], 3);
        return reply[2] == 0x01;
    }

    /// <summary>Sets the target power. Returns the watt value actually accepted (rounded to 5W steps).</summary>
    public int SetWatt(int watt)
    {
        watt = Math.Clamp(watt, 25, 400);
        var steps = (byte)Math.Round(watt / 5.0);
        var reply = Transact([0x51, Address, steps], 3);
        return reply[2] * 5;
    }

    public RunData ReadRunData()
    {
        var r = Transact([0x40, Address], 19);

        var distanceRaw = r[8] | (r[9] << 8);
        var timeRaw = r[10] | (r[11] << 8);
        var kjouleRaw = r[12] | (r[13] << 8);

        return new RunData
        {
            Program = r[2],
            Person = r[3],
            // Firmware quirk per spec: pedaling flag can arrive with a stray high bit set, so only look at bit 0.
            Pedaling = (r[4] & 0x01) != 0,
            Watt = r[5] * 5,
            Rpm = r[6],
            SpeedKmh = r[7],
            DistanceKm = distanceRaw / 10.0,
            TimeSeconds = timeRaw,
            EnergyKJ = kjouleRaw / 10.0,
            Pulse = r[14],
            PulseState = r[15],
            Gear = r[16],
        };
    }
}
