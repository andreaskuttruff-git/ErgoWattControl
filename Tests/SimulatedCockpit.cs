namespace ErgoWattControl.Tests;

/// <summary>
/// A Daum cockpit in software, behind <see cref="ICockpitPort"/>: answers the commands the app uses like the real
/// one (command byte and address first) and can be told to stay silent, answer out of step or fall out of free training.
/// </summary>
internal sealed class SimulatedCockpit : ICockpitPort
{
    public enum Fault { Silent, WrongReply }

    private readonly object _lock = new();
    private readonly Queue<byte> _pending = new();
    private readonly Queue<Fault> _faults = new();

    public byte Address { get; init; } = 7;
    /// <summary>False: like an old cockpit that doesn't echo the command (the app must not check then).</summary>
    public bool Echoes { get; init; } = true;
    public byte CockpitType { get; init; } = 40; // 8008

    public int Program { get; set; }
    /// <summary>The program the cockpit falls back to again and again (a stuck cockpit); null = behaves.</summary>
    public int? StuckInProgram { get; set; }
    public int WattSteps { get; set; } = 20;
    public int Rpm { get; set; } = 80;
    public int Pulse { get; set; } = 120;

    public List<byte[]> Requests { get; } = [];
    public bool Disposed { get; private set; }

    public int Count(byte command)
    {
        lock (_lock)
            return Requests.Count(r => r[0] == command);
    }

    /// <summary>The next requests (one fault each) go wrong in this way.</summary>
    public void Inject(params Fault[] faults)
    {
        lock (_lock)
            foreach (var f in faults)
                _faults.Enqueue(f);
    }

    /// <summary>Only the next requests of this command fail (the others keep working).</summary>
    public byte? FaultOnlyFor { get; set; }

    public void DiscardInBuffer()
    {
        lock (_lock)
            _pending.Clear();
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        var request = buffer.Skip(offset).Take(count).ToArray();
        lock (_lock)
        {
            Requests.Add(request);
            var reply = Answer(request);
            if ((FaultOnlyFor is null || FaultOnlyFor == request[0]) && _faults.TryDequeue(out var fault))
            {
                if (fault == Fault.Silent)
                    return;
                reply[0] = (byte)(reply[0] ^ 0x5A); // a stray byte where the command echo should be
            }
            foreach (var b in reply)
                _pending.Enqueue(b);
        }
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            if (_pending.Count == 0)
                throw new TimeoutException("Simuliertes Cockpit antwortet nicht.");
            var n = 0;
            while (n < count && _pending.TryDequeue(out var b))
                buffer[offset + n++] = b;
            return n;
        }
    }

    public void Dispose() => Disposed = true;

    private byte[] Answer(byte[] r)
    {
        byte first = Echoes ? r[0] : (byte)0x00;
        switch (r[0])
        {
            case 0x11:
                return [first, Address];
            case 0x10:
                return [first, Address, 0x2A];
            case 0x73:
                var version = new byte[11];
                version[0] = first;
                version[1] = Address;
                version[10] = CockpitType;
                return version;
            case 0x23:
                Program = StuckInProgram ?? r[2];
                return [first, Address, r[2], 0];
            case 0x21 or 0x22:
                return [first, Address, 1];
            case 0x51:
                WattSteps = r[2];
                return [first, Address, r[2]];
            case 0x40:
                var d = new byte[19];
                d[0] = first;
                d[1] = Address;
                d[2] = (byte)Program;
                d[4] = 0x81; // pedalling, with the stray high bit the firmware sometimes sends
                d[5] = (byte)WattSteps;
                d[6] = (byte)Rpm;
                d[7] = 30;
                d[8] = 123 & 0xFF; d[9] = 0; // 12.3 km
                d[10] = 0x2C; d[11] = 0x01; // 300 s
                d[12] = 0xE8; d[13] = 0x03; // 100.0 kJ
                d[14] = (byte)Pulse;
                d[16] = 14;
                return d;
            default:
                throw new InvalidOperationException($"Unbekannter Befehl 0x{r[0]:X2}");
        }
    }
}
