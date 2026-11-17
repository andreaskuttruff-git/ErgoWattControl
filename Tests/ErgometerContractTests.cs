namespace ErgoWattControl.Tests;

/// <summary>
/// The contract of <see cref="IErgometer"/> (see its summary), the same for every device. A new kind of ergometer gets
/// its own subclass at the bottom of this file – ideally against a simulation of its protocol, like the Daum cockpit.
/// </summary>
public abstract class ErgometerContractTests
{
    /// <summary>A new, not yet started device.</summary>
    protected abstract IErgometer CreateDevice();

    /// <summary>Longest time between two readings while connected.</summary>
    protected abstract TimeSpan ReadingInterval { get; }

    /// <summary>How the device and its events are watched in a test.</summary>
    private sealed class Probe : IDisposable
    {
        private readonly List<ErgometerData> _data = [];
        private readonly List<ErgometerStatus> _status = [];
        public IErgometer Device { get; }

        public Probe(IErgometer device)
        {
            Device = device;
            device.DataReceived += d => { lock (_data) _data.Add(d); };
            device.StatusChanged += s => { lock (_status) _status.Add(s); };
        }

        public int Readings { get { lock (_data) return _data.Count; } }
        public ErgometerData? Last { get { lock (_data) return _data.Count > 0 ? _data[^1] : null; } }
        public bool ReportedConnected { get { lock (_status) return _status.Any(s => s.Connected); } }
        public int Events => Readings + StatusCount;
        private int StatusCount { get { lock (_status) return _status.Count; } }

        public void Dispose() => Device.Dispose();
    }

    private static void WaitFor(Func<bool> condition, string what, double seconds = 10)
    {
        var until = DateTime.Now.AddSeconds(seconds);
        while (!condition())
        {
            if (DateTime.Now > until)
                throw new TimeoutException($"Nicht eingetreten: {what}");
            Thread.Sleep(20);
        }
    }

    [Fact]
    public void Creating_DoesNotConnect()
    {
        using var probe = new Probe(CreateDevice());
        Thread.Sleep(300);
        Assert.False(probe.Device.IsConnected);
        Assert.Equal(0, probe.Events);
    }

    [Fact]
    public void Capabilities_AreValid_BeforeAndAfterConnecting()
    {
        using var probe = new Probe(CreateDevice());
        void Check(ErgometerCapabilities c)
        {
            Assert.InRange(c.MaxWatt, ErgoController.MinWatt, ErgoController.AbsoluteMaxWatt);
            Assert.True(c.WattStep >= 1);
            Assert.False(string.IsNullOrWhiteSpace(c.Info));
            if (c.CadenceRange is { } range)
            {
                var (min, max) = range(150);
                Assert.True(min < max);
            }
        }
        Check(probe.Device.Capabilities);
        probe.Device.Start();
        WaitFor(() => probe.Device.IsConnected, "verbunden");
        Check(probe.Device.Capabilities);
        Assert.False(string.IsNullOrWhiteSpace(probe.Device.DisplayName));
    }

    [Fact]
    public void Start_ReportsConnected_ThenDeliversReadings()
    {
        using var probe = new Probe(CreateDevice());
        probe.Device.Start();

        WaitFor(() => probe.ReportedConnected && probe.Device.IsConnected, "Status verbunden");
        WaitFor(() => probe.Readings >= 2, "zwei Messwerte", seconds: 2 + 3 * ReadingInterval.TotalSeconds);

        var d = probe.Last!;
        Assert.True(d.Rpm >= 0);
        Assert.True(d.Pulse >= 0);
        Assert.True(d.Watt >= 0);
    }

    [Fact]
    public void TargetSetBeforeConnecting_IsAppliedOnConnect()
    {
        using var probe = new Probe(CreateDevice());
        probe.Device.SetTargetPower(150);
        probe.Device.Start();

        WaitFor(() => probe.Last?.Watt == 150, "150 W nach dem Verbinden", seconds: 2 + 3 * ReadingInterval.TotalSeconds);
    }

    [Fact]
    public void TargetSetWhileRiding_IsApplied()
    {
        using var probe = new Probe(CreateDevice());
        probe.Device.Start();
        WaitFor(() => probe.Device.IsConnected, "verbunden");

        probe.Device.SetTargetPower(205);

        WaitFor(() => probe.Last?.Watt == 205, "205 W", seconds: 2 + 3 * ReadingInterval.TotalSeconds);
    }

    [Fact]
    public void Dispose_WithoutStart_AndTwice_IsHarmless()
    {
        var device = CreateDevice();
        device.Dispose();
        device.Dispose();
    }

    [Fact]
    public void Dispose_ReturnsSoon_AndNoEventsFollow()
    {
        var probe = new Probe(CreateDevice());
        probe.Device.Start();
        WaitFor(() => probe.Readings >= 1, "erster Messwert", seconds: 2 + 3 * ReadingInterval.TotalSeconds);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        probe.Dispose();
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"Dispose dauerte {watch.Elapsed.TotalSeconds:0.0} s");

        var events = probe.Events;
        Thread.Sleep(ReadingInterval * 2);
        Assert.Equal(events, probe.Events);
        Assert.False(probe.Device.IsConnected);
    }
}

public class DemoErgometerContractTests : ErgometerContractTests
{
    protected override IErgometer CreateDevice() => new DemoErgometer();
    protected override TimeSpan ReadingInterval => TimeSpan.FromSeconds(1.3);
}

/// <summary>The Daum ergo_bike with its protocol against <see cref="SimulatedCockpit"/> instead of the RS232 port.</summary>
public class DaumErgometerContractTests : ErgometerContractTests
{
    protected override IErgometer CreateDevice() =>
        new DaumErgometer("COM-TEST", _ => new DaumCockpit(new SimulatedCockpit()));
    protected override TimeSpan ReadingInterval => TimeSpan.FromSeconds(0.6);
}

/// <summary>A Bluetooth FTMS trainer (an ErgoBridge) against <see cref="SimulatedFtmsLink"/> instead of Windows' Bluetooth stack.</summary>
public class FtmsErgometerContractTests : ErgometerContractTests
{
    protected override IErgometer CreateDevice() => new FtmsErgometer(new SimulatedFtmsLink(), TimeSpan.FromMilliseconds(200));
    protected override TimeSpan ReadingInterval => TimeSpan.FromSeconds(0.6);
}
