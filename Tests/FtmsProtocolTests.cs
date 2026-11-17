namespace ErgoWattControl.Tests;

/// <summary>Byte layouts of FTMS and the ErgoBridge status (<see cref="FtmsProtocol"/>), worked out from the specification.</summary>
public class FtmsProtocolTests
{
    [Fact]
    public void IndoorBikeData_AsTheErgoBridgeSendsIt()
    {
        // Same bytes as the firmware's test: speed 30 km/h, 88.5 rpm, 12.3 km, 150 W, 135 bpm, 300 s.
        byte[] value = [0x54, 0x0A, 0xB8, 0x0B, 0xB1, 0x00, 0x0C, 0x30, 0x00, 0x96, 0x00, 0x87, 0x2C, 0x01];

        var d = FtmsProtocol.ParseIndoorBikeData(value)!;

        Assert.Equal(30.0, d.SpeedKmh);
        Assert.Equal(88.5, d.Rpm);
        Assert.Equal(12.3, d.DistanceKm!.Value, 3);
        Assert.Equal(150, d.Watt);
        Assert.Equal(135, d.Pulse);
        Assert.Equal(300, d.ElapsedSeconds);
        Assert.Null(d.EnergyKJ);
    }

    [Fact]
    public void IndoorBikeData_WithoutHeartRate()
    {
        byte[] value = [0x54, 0x08, 0, 0, 0, 0, 0, 0, 0, 0x64, 0x00, 0, 0];
        var d = FtmsProtocol.ParseIndoorBikeData(value)!;
        Assert.Equal(100, d.Watt);
        Assert.Null(d.Pulse);
    }

    [Fact]
    public void IndoorBikeData_SkipsFieldsTheAppDoesntUse()
    {
        // More Data set (no speed), average cadence, resistance level, power, average power, expended energy.
        byte[] value =
        [
            0xE9, 0x01,       // flags: 0 more data, 3 avg cadence, 5 resistance, 6 power, 7 avg power, 8 energy
            0xB4, 0x00,       // average cadence
            0x0A, 0x00,       // resistance level
            0xC8, 0x00,       // 200 W
            0xBE, 0x00,       // average power
            0x64, 0x00,       // 100 kcal total
            0xFF, 0xFF, 0xFF, // per hour, per minute: not available
        ];
        var d = FtmsProtocol.ParseIndoorBikeData(value)!;
        Assert.Null(d.SpeedKmh);
        Assert.Null(d.Rpm);
        Assert.Equal(200, d.Watt);
        Assert.Equal(418.4, d.EnergyKJ!.Value, 3);
    }

    [Fact]
    public void IndoorBikeData_ShorterThanItsFlagsSay_IsRejected()
    {
        Assert.Null(FtmsProtocol.ParseIndoorBikeData([0x54, 0x0A, 0xB8, 0x0B, 0xB1]));
        Assert.Null(FtmsProtocol.ParseIndoorBikeData([0x00]));
    }

    [Fact]
    public void PowerRange_AndBridgeStatus()
    {
        Assert.Equal(new PowerRange(25, 800, 5), FtmsProtocol.ParsePowerRange([0x19, 0x00, 0x20, 0x03, 0x05, 0x00]));
        Assert.Null(FtmsProtocol.ParsePowerRange([0x19, 0x00]));

        Assert.Equal(new BridgeStatus(true, 40, 800, 800, 150),
            FtmsProtocol.ParseBridgeStatus([1, 0x01, 40, 0x20, 0x03, 0x20, 0x03, 0x96, 0x00]));
        Assert.Equal(new BridgeStatus(false, null, 400, 800, 100),
            FtmsProtocol.ParseBridgeStatus([1, 0x00, 0xFF, 0x90, 0x01, 0x20, 0x03, 0x64, 0x00]));
        Assert.Null(FtmsProtocol.ParseBridgeStatus([1, 0, 0]));
    }

    [Fact]
    public void ControlPoint_RequestsAndResponses()
    {
        Assert.Equal([0x05, 0x96, 0x00], FtmsProtocol.SetTargetPower(150));
        Assert.Equal([0x05, 0x00, 0x00], FtmsProtocol.SetTargetPower(-5));
        Assert.Equal([0x00], FtmsProtocol.RequestControl());
        Assert.Equal(((byte)0x05, FtmsResult.ControlNotPermitted), FtmsProtocol.ParseResponse([0x80, 0x05, 0x05]));
        Assert.Null(FtmsProtocol.ParseResponse([0x08, 0x96, 0x00])); // a status, not a response
    }
}
