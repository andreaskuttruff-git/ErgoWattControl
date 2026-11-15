namespace ErgoWattControl.Tests;

/// <summary>The list of ergometer kinds (<see cref="ErgometerTypes"/>): what settings.json and the settings dialog rely on.</summary>
public class ErgometerTypesTests
{
    [Fact]
    public void Ids_AndNames_AreUnique()
    {
        Assert.Equal(ErgometerTypes.All.Count, ErgometerTypes.All.Select(t => t.Id).Distinct().Count());
        Assert.Equal(ErgometerTypes.All.Count, ErgometerTypes.All.Select(t => t.Name).Distinct().Count());
    }

    [Fact]
    public void ReleasedIds_StayTheSame()
    {
        // Stored in settings.json: renaming one would switch existing installations to another device.
        Assert.Equal("DaumRs232", ErgometerTypes.DaumRs232);
        Assert.Equal("Demo", ErgometerTypes.Demo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("GibtEsNicht")]
    public void UnknownOrMissingId_MeansTheDaumCockpit(string? id)
    {
        // Settings from before the device choice have no id.
        Assert.Equal(ErgometerTypes.DaumRs232, ErgometerTypes.Find(id).Id);
        using var device = ErgometerTypes.Create(id, new AppSettings { PortName = "COM7" });
        Assert.IsType<DaumErgometer>(device);
        Assert.Equal("COM7", ((DaumErgometer)device).PortName);
    }

    [Fact]
    public void EveryType_CreatesItsDevice_WithoutConnecting()
    {
        foreach (var type in ErgometerTypes.All)
        {
            using var device = type.Create(new AppSettings { PortName = "COM99" });
            Assert.False(device.IsConnected, type.Id);
            Assert.Same(type, ErgometerTypes.Find(type.Id));
        }
    }

    [Fact]
    public void Daum_IsConnectedViaACOMPort_WithAConnectionTest()
    {
        var daum = ErgometerTypes.Find(ErgometerTypes.DaumRs232);
        Assert.True(daum.UsesSerialPort);
        Assert.NotNull(daum.TestConnection);

        Assert.True(daum.ConnectionChanged(new AppSettings { PortName = "COM1" }, new AppSettings { PortName = "COM3" }));
        Assert.False(daum.ConnectionChanged(new AppSettings { PortName = "COM1" }, new AppSettings { PortName = "COM1" }));
    }

    [Fact]
    public void Demo_NeedsNoConnectionFields()
    {
        var demo = ErgometerTypes.Find(ErgometerTypes.Demo);
        Assert.False(demo.UsesSerialPort);
        Assert.Null(demo.TestConnection);
        // A changed COM port is no reason to restart the demo.
        Assert.False(demo.ConnectionChanged(new AppSettings { PortName = "COM1" }, new AppSettings { PortName = "COM3" }));
    }

    [Fact]
    public void Daum_ConnectionTest_OnAMissingPort_SaysSo()
    {
        var result = ErgometerTypes.Find(ErgometerTypes.DaumRs232).TestConnection!("COM199", null);
        Assert.StartsWith("Fehler", result);
    }
}
