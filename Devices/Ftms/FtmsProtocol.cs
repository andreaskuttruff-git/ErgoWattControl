using System.Buffers.Binary;

namespace ErgoWattControl;

/// <summary>One Indoor Bike Data notification (FTMS 4.9); fields the trainer didn't send are null.</summary>
public sealed record IndoorBikeData
{
    public double? SpeedKmh { get; init; }
    public double? Rpm { get; init; }
    public int? Watt { get; init; }
    public int? Pulse { get; init; }
    public double? DistanceKm { get; init; }
    public double? EnergyKJ { get; init; }
    public int? ElapsedSeconds { get; init; }
}

/// <summary>Supported Power Range (FTMS 4.14).</summary>
public sealed record PowerRange(int MinWatt, int MaxWatt, int StepWatt);

/// <summary>The ErgoBridge status characteristic: whether the Daum cockpit behind the bridge answers, and its limits.</summary>
public sealed record BridgeStatus(bool CockpitConnected, int? CockpitType, int MaxWatt, int TargetLimit, int TargetWatt);

/// <summary>Control point result codes (FTMS 4.16.2.22).</summary>
public enum FtmsResult : byte
{
    Success = 0x01,
    OpCodeNotSupported = 0x02,
    InvalidParameter = 0x03,
    OperationFailed = 0x04,
    ControlNotPermitted = 0x05,
}

/// <summary>
/// Byte layouts of the Bluetooth Fitness Machine Service (FTMS) as far as the app uses them, plus the ErgoBridge
/// status characteristic. All fields are little-endian.
/// </summary>
public static class FtmsProtocol
{
    public const ushort FitnessMachineService = 0x1826;
    public const ushort IndoorBikeDataChar = 0x2AD2;
    public const ushort SupportedPowerRangeChar = 0x2AD8;
    public const ushort ControlPointChar = 0x2AD9;

    public static readonly Guid BridgeService = new("c7b6d000-4e2a-4f69-9a5e-2d0e8b3f1a01");
    public static readonly Guid BridgeStatusChar = new("c7b6d001-4e2a-4f69-9a5e-2d0e8b3f1a01");

    public const byte OpRequestControl = 0x00;
    public const byte OpReset = 0x01;
    public const byte OpSetTargetPower = 0x05;
    public const byte OpStartOrResume = 0x07;
    public const byte OpResponse = 0x80;

    public static byte[] RequestControl() => [OpRequestControl];
    public static byte[] StartOrResume() => [OpStartOrResume];
    public static byte[] Reset() => [OpReset];

    public static byte[] SetTargetPower(int watt)
    {
        var request = new byte[3];
        request[0] = OpSetTargetPower;
        BinaryPrimitives.WriteInt16LittleEndian(request.AsSpan(1), (short)Math.Clamp(watt, 0, short.MaxValue));
        return request;
    }

    /// <summary>A control point indication: the request's op code and its result; null if it is no response.</summary>
    public static (byte OpCode, FtmsResult Result)? ParseResponse(ReadOnlySpan<byte> value) =>
        value.Length >= 3 && value[0] == OpResponse ? (value[1], (FtmsResult)value[2]) : null;

    public static PowerRange? ParsePowerRange(ReadOnlySpan<byte> value) =>
        value.Length < 6
            ? null
            : new PowerRange(
                BinaryPrimitives.ReadInt16LittleEndian(value),
                BinaryPrimitives.ReadInt16LittleEndian(value[2..]),
                BinaryPrimitives.ReadUInt16LittleEndian(value[4..]));

    public static BridgeStatus? ParseBridgeStatus(ReadOnlySpan<byte> value)
    {
        if (value.Length < 9 || value[0] < 1)
            return null;
        var type = (sbyte)value[2];
        return new BridgeStatus(
            (value[1] & 0x01) != 0,
            type < 0 ? null : type,
            BinaryPrimitives.ReadUInt16LittleEndian(value[3..]),
            BinaryPrimitives.ReadUInt16LittleEndian(value[5..]),
            BinaryPrimitives.ReadUInt16LittleEndian(value[7..]));
    }

    /// <summary>
    /// Decodes Indoor Bike Data with all optional fields of FTMS 1.0, so trainers other than ErgoBridge work too.
    /// Null if the value is shorter than its flags announce.
    /// </summary>
    public static IndoorBikeData? ParseIndoorBikeData(ReadOnlySpan<byte> value)
    {
        if (value.Length < 2)
            return null;
        var flags = BinaryPrimitives.ReadUInt16LittleEndian(value);
        var pos = 2;
        var length = value.Length;
        bool Has(int bit) => (flags & (1 << bit)) != 0;
        bool Fits(int count) => pos + count <= length;

        double? speed = null, rpm = null, distance = null, energy = null;
        int? watt = null, pulse = null, elapsed = null;

        // Bit 0 "More Data" works the other way round: clear = instantaneous speed present.
        if (!Has(0))
        {
            if (!Fits(2)) return null;
            speed = BinaryPrimitives.ReadUInt16LittleEndian(value[pos..]) / 100.0;
            pos += 2;
        }
        if (Has(1)) { if (!Fits(2)) return null; pos += 2; } // average speed
        if (Has(2))
        {
            if (!Fits(2)) return null;
            rpm = BinaryPrimitives.ReadUInt16LittleEndian(value[pos..]) / 2.0;
            pos += 2;
        }
        if (Has(3)) { if (!Fits(2)) return null; pos += 2; } // average cadence
        if (Has(4))
        {
            if (!Fits(3)) return null;
            distance = (value[pos] | (value[pos + 1] << 8) | (value[pos + 2] << 16)) / 1000.0;
            pos += 3;
        }
        if (Has(5)) { if (!Fits(2)) return null; pos += 2; } // resistance level
        if (Has(6))
        {
            if (!Fits(2)) return null;
            watt = BinaryPrimitives.ReadInt16LittleEndian(value[pos..]);
            pos += 2;
        }
        if (Has(7)) { if (!Fits(2)) return null; pos += 2; } // average power
        if (Has(8))
        {
            if (!Fits(5)) return null;
            var kcal = BinaryPrimitives.ReadUInt16LittleEndian(value[pos..]);
            if (kcal != 0xFFFF) // "not available"
                energy = kcal * 4.184;
            pos += 5; // total, per hour, per minute
        }
        if (Has(9))
        {
            if (!Fits(1)) return null;
            pulse = value[pos];
            pos += 1;
        }
        if (Has(10)) { if (!Fits(1)) return null; pos += 1; } // metabolic equivalent
        if (Has(11))
        {
            if (!Fits(2)) return null;
            elapsed = BinaryPrimitives.ReadUInt16LittleEndian(value[pos..]);
            pos += 2;
        }
        if (Has(12) && !Fits(2)) return null; // remaining time

        return new IndoorBikeData
        {
            SpeedKmh = speed,
            Rpm = rpm,
            Watt = watt,
            Pulse = pulse,
            DistanceKm = distance,
            EnergyKJ = energy,
            ElapsedSeconds = elapsed,
        };
    }
}
