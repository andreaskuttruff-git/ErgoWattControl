using Fault = ErgoWattControl.Tests.SimulatedCockpit.Fault;

namespace ErgoWattControl.Tests;

/// <summary>The telegrams of <see cref="DaumCockpit"/> against a simulated cockpit.</summary>
public class DaumCockpitTests
{
    private static (DaumCockpit Cockpit, SimulatedCockpit Sim) Connect(bool echoes = true)
    {
        var sim = new SimulatedCockpit { Echoes = echoes };
        var cockpit = new DaumCockpit(sim);
        cockpit.QueryAddress();
        return (cockpit, sim);
    }

    [Fact]
    public void QueryAddress_UsesTheCockpitsAddressInLaterRequests()
    {
        var (cockpit, sim) = Connect();
        Assert.Equal(7, cockpit.Address);
        cockpit.ReadRunData();
        Assert.Equal(7, sim.Requests[^1][1]);
    }

    [Fact]
    public void ReadRunData_DecodesTheTelegram()
    {
        var (cockpit, sim) = Connect();
        sim.WattSteps = 30;
        sim.Rpm = 92;
        sim.Pulse = 135;

        var d = cockpit.ReadRunData();

        Assert.Equal(0, d.Program);
        Assert.True(d.Pedaling); // only bit 0 counts, the stray high bit is ignored
        Assert.Equal(150, d.Watt);
        Assert.Equal(92, d.Rpm);
        Assert.Equal(30, d.SpeedKmh);
        Assert.Equal(12.3, d.DistanceKm, 3);
        Assert.Equal(300, d.TimeSeconds);
        Assert.Equal(100.0, d.EnergyKJ, 3);
        Assert.Equal(135, d.Pulse);
        Assert.Equal(14, d.Gear);
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(103, 105)] // 5 W steps
    [InlineData(10, 25)]   // protocol minimum
    [InlineData(900, 800)] // protocol maximum
    public void SetWatt_RoundsAndClamps(int requested, int accepted)
    {
        var (cockpit, _) = Connect();
        Assert.Equal(accepted, cockpit.SetWatt(requested));
    }

    [Fact]
    public void GetCockpitType_ReadsTheTypeByte()
    {
        var (cockpit, _) = Connect();
        Assert.Equal(40, cockpit.GetCockpitType());
        Assert.Equal(800, DaumCockpit.MaxWattForType(40));
        Assert.Equal(400, DaumCockpit.MaxWattForType(null));
    }

    [Fact]
    public void MissingReply_IsATimeout()
    {
        var (cockpit, sim) = Connect();
        sim.Inject(Fault.Silent);
        Assert.Throws<TimeoutException>(() => cockpit.ReadRunData());
    }

    [Fact]
    public void ReplyOutOfStep_IsRejected()
    {
        var (cockpit, sim) = Connect();
        sim.Inject(Fault.WrongReply);
        var ex = Assert.Throws<CockpitReplyException>(() => cockpit.ReadRunData());
        Assert.Contains("0x40", ex.Message);
        // The next request is fine again (stale bytes are discarded).
        Assert.Equal(100, cockpit.ReadRunData().Watt);
    }

    [Fact]
    public void CockpitWithoutEcho_IsNotChecked()
    {
        // An older cockpit that doesn't echo the command keeps working as before.
        var (cockpit, _) = Connect(echoes: false);
        Assert.Equal(100, cockpit.ReadRunData().Watt);
    }
}
