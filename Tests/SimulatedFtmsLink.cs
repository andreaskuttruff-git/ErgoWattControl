using System.Buffers.Binary;

namespace ErgoWattControl.Tests;

/// <summary>
/// A Bluetooth FTMS trainer in software behind <see cref="IFtmsLink"/>, behaving like the ErgoBridge firmware: control
/// only after Request Control, Indoor Bike Data every 100 ms with the target power, power range 25–800 W, and the
/// bridge status (cockpit connected or not). Can be told to be missing, drop the connection or refuse control.
/// </summary>
internal sealed class SimulatedFtmsLink : IFtmsLink
{
    private readonly object _lock = new();

    /// <summary>False: like a switched-off trainer – the scan finds nothing.</summary>
    public bool Available { get; set; } = true;
    public bool IsErgoBridge { get; init; } = true;
    public string Name { get; init; } = "ErgoBridge";
    public int MaxWatt { get; init; } = 800;
    /// <summary>-1 = the bridge doesn't know the cockpit type.</summary>
    public int CockpitType { get; set; } = 40;
    /// <summary>The next connections refuse Request Control (another app has it).</summary>
    public bool RefuseControl { get; set; }
    public int Rpm { get; set; } = 85;
    public int Pulse { get; set; } = 120;

    private bool _cockpitConnected = true;
    /// <summary>Whether the Daum cockpit behind the bridge answers; a change is notified like the firmware does.</summary>
    public bool CockpitConnected
    {
        get => _cockpitConnected;
        set
        {
            _cockpitConnected = value;
            Current?.NotifyBridgeStatus();
        }
    }

    public int Connects { get; private set; }
    public SimulatedTrainer? Current { get; private set; }

    public Task<IFtmsConnection?> ConnectAsync(CancellationToken cancel)
    {
        lock (_lock)
        {
            if (!Available)
                return Task.FromResult<IFtmsConnection?>(null);
            Connects++;
            Current = new SimulatedTrainer(this);
            return Task.FromResult<IFtmsConnection?>(Current);
        }
    }

    internal sealed class SimulatedTrainer : IFtmsConnection
    {
        private readonly SimulatedFtmsLink _link;
        private readonly System.Threading.Timer _timer;
        private readonly object _lock = new();
        private bool _hasControl;
        private bool _disposed;

        public int TargetWatt { get; private set; } = 100;
        public List<byte[]> Writes { get; } = [];

        public string Name => _link.Name;
        public bool IsErgoBridge => _link.IsErgoBridge;

        public event Action<byte[]>? IndoorBikeData;
        public event Action<byte[]>? ControlPointResponse;
        public event Action<byte[]>? BridgeStatus;
        public event Action? Lost;

        public SimulatedTrainer(SimulatedFtmsLink link)
        {
            _link = link;
            _timer = new System.Threading.Timer(_ => SendData(), null, 100, 100);
        }

        public int Count(byte opCode)
        {
            lock (_lock)
                return Writes.Count(w => w[0] == opCode);
        }

        /// <summary>The trainer forgets who controls it (e.g. after its own reset).</summary>
        public void ForgetControl()
        {
            lock (_lock)
                _hasControl = false;
        }

        /// <summary>The connection breaks (out of range, switched off).</summary>
        public void Drop()
        {
            _link.Available = false;
            Dispose();
            Lost?.Invoke();
        }

        private void SendData()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;
            }
            var cockpit = !_link.IsErgoBridge || _link.CockpitConnected;
            var watt = cockpit ? TargetWatt : 0;
            var rpm = cockpit ? _link.Rpm : 0;
            var pulse = cockpit ? _link.Pulse : 0;
            // Same layout as the firmware: speed, cadence, distance, power, (heart rate,) elapsed time.
            var flags = (ushort)(0x0854 | (pulse > 0 ? 0x0200 : 0));
            var data = new List<byte>();
            data.AddRange(BitConverter.GetBytes(flags));
            data.AddRange(BitConverter.GetBytes((ushort)(rpm * 35))); // 0.35 km/h per rpm, in 0.01 km/h
            data.AddRange(BitConverter.GetBytes((ushort)(rpm * 2)));
            data.AddRange([0x10, 0x27, 0x00]); // 10 km
            data.AddRange(BitConverter.GetBytes((short)watt));
            if (pulse > 0)
                data.Add((byte)pulse);
            data.AddRange(BitConverter.GetBytes((ushort)600));
            IndoorBikeData?.Invoke([.. data]);
        }

        public void NotifyBridgeStatus()
        {
            if (_link.IsErgoBridge)
                BridgeStatus?.Invoke(StatusValue());
        }

        private byte[] StatusValue()
        {
            var limit = _link.CockpitType < 0 ? 800 : _link.MaxWatt;
            var v = new byte[9];
            v[0] = 1;
            v[1] = (byte)(_link.CockpitConnected ? 1 : 0);
            v[2] = (byte)(sbyte)_link.CockpitType;
            BinaryPrimitives.WriteUInt16LittleEndian(v.AsSpan(3), (ushort)_link.MaxWatt);
            BinaryPrimitives.WriteUInt16LittleEndian(v.AsSpan(5), (ushort)limit);
            BinaryPrimitives.WriteUInt16LittleEndian(v.AsSpan(7), (ushort)TargetWatt);
            return v;
        }

        public Task<byte[]?> ReadPowerRangeAsync(CancellationToken cancel)
        {
            var v = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(v, 25);
            BinaryPrimitives.WriteInt16LittleEndian(v.AsSpan(2), (short)_link.MaxWatt);
            BinaryPrimitives.WriteUInt16LittleEndian(v.AsSpan(4), 5);
            return Task.FromResult<byte[]?>(v);
        }

        public Task<byte[]?> ReadBridgeStatusAsync(CancellationToken cancel) =>
            Task.FromResult(_link.IsErgoBridge ? StatusValue() : null);

        public Task WriteControlPointAsync(byte[] request, CancellationToken cancel)
        {
            FtmsResult result;
            lock (_lock)
            {
                if (_disposed)
                    throw new IOException("not connected");
                Writes.Add(request);
                result = request[0] switch
                {
                    FtmsProtocol.OpRequestControl when _link.RefuseControl => FtmsResult.ControlNotPermitted,
                    FtmsProtocol.OpRequestControl => Grant(),
                    FtmsProtocol.OpStartOrResume or FtmsProtocol.OpReset or FtmsProtocol.OpSetTargetPower
                        when !_hasControl => FtmsResult.ControlNotPermitted,
                    FtmsProtocol.OpSetTargetPower => SetTarget(BinaryPrimitives.ReadInt16LittleEndian(request.AsSpan(1))),
                    FtmsProtocol.OpStartOrResume or FtmsProtocol.OpReset => FtmsResult.Success,
                    _ => FtmsResult.OpCodeNotSupported,
                };
            }
            var response = new byte[] { FtmsProtocol.OpResponse, request[0], (byte)result };
            // The indication arrives after the write, on another thread – as with Windows' Bluetooth stack.
            _ = Task.Run(() => ControlPointResponse?.Invoke(response));
            return Task.CompletedTask;
        }

        private FtmsResult Grant()
        {
            _hasControl = true;
            return FtmsResult.Success;
        }

        private FtmsResult SetTarget(int watt)
        {
            TargetWatt = (watt + 2) / 5 * 5;
            return FtmsResult.Success;
        }

        public void Dispose()
        {
            lock (_lock)
                _disposed = true;
            _timer.Dispose();
        }
    }
}
