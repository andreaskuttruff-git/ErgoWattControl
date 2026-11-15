namespace ErgoWattControl.Tests;

/// <summary>
/// <see cref="ErgoController"/>, the app's single entry to whatever device is in use: limits, power steps and device
/// changes – checked with a scripted device instead of hardware.
/// </summary>
public class ErgoControllerTests
{
    /// <summary>A device that only records what it is told and raises events when the test says so.</summary>
    private sealed class ScriptedErgometer(int maxWatt = 400, int wattStep = 5) : IErgometer
    {
        public ErgometerCapabilities Capabilities { get; set; } = new(maxWatt, wattStep, null, $"Testgerät: max. {maxWatt} W");
        public string DisplayName => "Testgerät";
        public bool IsConnected { get; private set; }
        public bool Started { get; private set; }
        public bool Disposed { get; private set; }
        public List<int> Targets { get; } = [];

        public event Action<ErgometerData>? DataReceived;
        public event Action<ErgometerStatus>? StatusChanged;

        public void Start() => Started = true;
        public void SetTargetPower(int watt) => Targets.Add(watt);
        public void Dispose() => Disposed = true;

        public void Connect()
        {
            IsConnected = true;
            StatusChanged?.Invoke(new ErgometerStatus("Verbunden", true));
        }

        public void Send(int watt) => DataReceived?.Invoke(new ErgometerData { Watt = watt, Rpm = 80 });
    }

    [Fact]
    public void WithoutDevice_UsesSafeDefaults()
    {
        using var ergo = new ErgoController();
        Assert.Null(ergo.Device);
        Assert.False(ergo.IsConnected);
        Assert.Equal(400, ergo.MaxWatt);
        Assert.Equal(100, ergo.TargetWatt);
    }

    [Theory]
    [InlineData(5, 133, 135)]
    [InlineData(10, 133, 130)]
    [InlineData(10, 136, 140)]
    [InlineData(1, 133, 133)]
    [InlineData(5, 10, ErgoController.MinWatt)]
    [InlineData(5, 999, 400)]
    public void Target_FollowsTheDevicesStepsAndLimits(int step, int requested, int expected)
    {
        using var ergo = new ErgoController();
        var device = new ScriptedErgometer(maxWatt: 400, wattStep: step);
        ergo.Use(device);

        Assert.Equal(expected, ergo.SetTargetWatt(requested));
        Assert.Equal(expected, device.Targets[^1]);
    }

    [Fact]
    public void UserLimit_LowersTheTargetAtOnce()
    {
        using var ergo = new ErgoController();
        var device = new ScriptedErgometer(maxWatt: 800);
        ergo.Use(device);
        ergo.SetTargetWatt(300);

        ergo.SetUserLimit(200);

        Assert.Equal(200, ergo.MaxWatt);
        Assert.Equal(200, ergo.TargetWatt);
        Assert.Equal(200, device.Targets[^1]);
    }

    [Fact]
    public void Use_StartsTheDevice_AndSendsTheCurrentTarget()
    {
        using var ergo = new ErgoController();
        ergo.SetTargetWatt(180);
        var device = new ScriptedErgometer();

        ergo.Use(device);

        Assert.True(device.Started);
        Assert.Equal(180, device.Targets[^1]);
    }

    [Fact]
    public void ChangingDevice_ClosesTheOldOne_AndIgnoresItsEvents()
    {
        using var ergo = new ErgoController();
        var readings = new List<int>();
        ergo.DataReceived += d => readings.Add(d.Watt);
        var first = new ScriptedErgometer();
        var second = new ScriptedErgometer();
        ergo.Use(first);
        ergo.SetTargetWatt(160);

        ergo.Use(second);
        first.Send(111);
        second.Send(160);

        Assert.True(first.Disposed);
        Assert.Same(second, ergo.Device);
        Assert.Equal(160, second.Targets[^1]); // the target carries over
        Assert.Equal([160], readings);
    }

    [Fact]
    public void DeviceKnowingALowerLimitOnConnect_LowersTheTarget()
    {
        // Like the Daum cockpit: 800 W until the cockpit type says 400 W.
        using var ergo = new ErgoController();
        var device = new ScriptedErgometer(maxWatt: 800);
        ergo.Use(device);
        ergo.SetTargetWatt(600);
        var statuses = new List<ErgometerStatus>();
        ergo.StatusChanged += statuses.Add;

        device.Capabilities = device.Capabilities with { MaxWatt = 400, Info = "Testgerät: max. 400 W" };
        device.Connect();

        Assert.Equal(400, ergo.TargetWatt);
        Assert.Equal(400, device.Targets[^1]);
        Assert.Single(statuses, s => s.Connected);
    }

    [Fact]
    public void Dispose_ClosesTheDevice()
    {
        var ergo = new ErgoController();
        var device = new ScriptedErgometer();
        ergo.Use(device);

        ergo.Dispose();

        Assert.True(device.Disposed);
        Assert.Null(ergo.Device);
    }
}
