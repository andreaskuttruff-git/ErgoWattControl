using Fault = ErgoWattControl.Tests.SimulatedCockpit.Fault;

namespace ErgoWattControl.Tests;

/// <summary>
/// The connection loop of <see cref="DaumErgometer"/> (on its own thread, as in the app) against a simulated cockpit:
/// retries, reply check, cockpit reset and power corrections.
/// </summary>
public class DaumErgometerTests
{
    private sealed class Rig : IDisposable
    {
        private readonly List<SimulatedCockpit> _opened = [];
        private readonly List<ErgometerData> _data = [];
        public readonly DaumErgometer Device;

        private int _asleep;

        /// <param name="asleepAttempts">Connection attempts the cockpit doesn't answer (asleep/off at start).</param>
        public Rig(int asleepAttempts = 0)
        {
            _asleep = asleepAttempts;
            Device = new DaumErgometer("COM-TEST", _ =>
            {
                if (Interlocked.Decrement(ref _asleep) >= 0)
                    throw new TimeoutException("The operation has timed out.");
                var sim = new SimulatedCockpit();
                lock (_opened)
                    _opened.Add(sim);
                return new DaumCockpit(sim);
            });
            Device.DataReceived += d => { lock (_data) _data.Add(d); };
        }

        public int Opened { get { lock (_opened) return _opened.Count; } }
        public SimulatedCockpit Sim { get { lock (_opened) return _opened[^1]; } }
        public SimulatedCockpit First { get { lock (_opened) return _opened[0]; } }
        public int Received { get { lock (_data) return _data.Count; } }
        public ErgometerData[] Data { get { lock (_data) return [.. _data]; } }

        /// <summary>Waits until <paramref name="condition"/> holds (the device runs on its own thread).</summary>
        public static void WaitFor(Func<bool> condition, string what, int seconds = 10)
        {
            var until = DateTime.Now.AddSeconds(seconds);
            while (!condition())
            {
                if (DateTime.Now > until)
                    throw new TimeoutException($"Nicht eingetreten: {what}");
                Thread.Sleep(20);
            }
        }

        public void Start()
        {
            Device.Start();
            WaitFor(() => Device.IsConnected && Received >= 1, "verbunden mit ersten Daten");
        }

        public void Dispose() => Device.Dispose();
    }

    [Fact]
    public void Connects_SelectsFreeTraining_AndDeliversData()
    {
        using var rig = new Rig();
        rig.Start();

        Assert.Equal(1, rig.Sim.Count(0x23)); // free training
        Assert.Equal(1, rig.Sim.Count(0x21)); // started
        Assert.Equal("Cockpit 8008: max. 800 W", rig.Device.Capabilities.Info);
        Assert.Equal(80, rig.Data[0].Rpm);
    }

    [Fact]
    public void CockpitConnected_IsNoted()
    {
        var mark = TestFolder.LogMark();
        using var rig = new Rig();
        rig.Start();
        Rig.WaitFor(() => TestFolder.LogSince(mark).Contains("Cockpit on COM-TEST connected"), "Verbindung im Log");
        Assert.Matches(@"Cockpit on COM-TEST connected \(address \d+, max\. 800 W\)\r?\n", TestFolder.LogSince(mark));
    }

    [Fact]
    public void CockpitAsleepAtStart_IsNotedOnce_AndTheWaitWhenItAnswers()
    {
        var mark = TestFolder.LogMark();
        using var rig = new Rig(asleepAttempts: 2);
        rig.Device.Start();
        Rig.WaitFor(() => rig.Device.IsConnected && TestFolder.LogSince(mark).Contains("connected"), "verbunden nach dem Aufwachen", seconds: 15);

        var log = TestFolder.LogSince(mark);
        // Two failed attempts with the same reason: one line, not one every 3 s.
        Assert.Single(log.Split('\n'), line => line.Contains("Cockpit on COM-TEST not answering (TimeoutException"));
        Assert.Matches(@"Cockpit on COM-TEST connected \(address \d+, max\. 800 W\) after [5-9] s", log);
        Assert.DoesNotContain("reconnected", log);
    }

    [Fact]
    public void TargetPower_ReachesTheCockpit()
    {
        using var rig = new Rig();
        rig.Start();
        rig.Device.SetTargetPower(185);
        Rig.WaitFor(() => rig.Sim.WattSteps == 37, "185 W im Cockpit");
    }

    [Fact]
    public void TwoMissingReplies_AreAskedAgain_WithoutReconnecting()
    {
        using var rig = new Rig();
        rig.Start();
        var mark = TestFolder.LogMark();
        var before = rig.Received;
        rig.Sim.FaultOnlyFor = 0x40;
        rig.Sim.Inject(Fault.Silent, Fault.Silent);

        Rig.WaitFor(() => rig.Received >= before + 2, "Daten nach dem Aussetzer");

        Assert.Equal(1, rig.Opened);
        Assert.Contains("answered after 2 retry/retries", TestFolder.LogSince(mark));
    }

    [Fact]
    public void ThreeMissingReplies_CountAsLostConnection_AndReconnect()
    {
        using var rig = new Rig();
        rig.Start();
        var mark = TestFolder.LogMark();
        rig.Sim.FaultOnlyFor = 0x40;
        rig.Sim.Inject(Fault.Silent, Fault.Silent, Fault.Silent);

        // The note is written just after IsConnected turns true: wait for the note itself, not only the flag.
        Rig.WaitFor(() => rig.Opened >= 2 && TestFolder.LogSince(mark).Contains("reconnected"), "neu verbunden", seconds: 15);

        Assert.True(rig.First.Disposed);
        Assert.True(rig.Device.IsConnected);
        Assert.Contains("lost (TimeoutException)", TestFolder.LogSince(mark));
    }

    [Fact]
    public void ReplyOutOfStep_IsDiscarded_AndAskedAgain()
    {
        using var rig = new Rig();
        rig.Start();
        var mark = TestFolder.LogMark();
        var before = rig.Received;
        rig.Sim.FaultOnlyFor = 0x40;
        rig.Sim.Inject(Fault.WrongReply);

        Rig.WaitFor(() => rig.Received >= before + 2, "Daten nach der verrutschten Antwort");

        Assert.Equal(1, rig.Opened);
        Assert.All(rig.Data, d => Assert.Equal(100, d.Watt)); // no garbage values passed on
        Assert.Contains("Reply does not match request 0x40", TestFolder.LogSince(mark));
    }

    [Fact]
    public void CockpitReset_IsNoted_AndFreeTrainingRestored()
    {
        using var rig = new Rig();
        rig.Start();
        rig.Device.SetTargetPower(150);
        Rig.WaitFor(() => rig.Sim.WattSteps == 30, "150 W");
        var mark = TestFolder.LogMark();

        // Static discharge: the cockpit restarts in its own program 3 with its default power.
        rig.Sim.Program = 3;
        rig.Sim.WattSteps = 5;

        Rig.WaitFor(() => rig.Sim.Program == 0 && rig.Sim.WattSteps == 30, "freies Training mit 150 W wiederhergestellt");
        Assert.Equal(1, rig.Opened);
        Assert.Contains("Cockpit in program 3 instead of free training", TestFolder.LogSince(mark));
    }

    [Fact]
    public void CockpitStuckInProgram_LeadsToReconnect()
    {
        using var rig = new Rig();
        rig.Start();
        var mark = TestFolder.LogMark();

        rig.Sim.StuckInProgram = 4;
        rig.Sim.Program = 4;

        Rig.WaitFor(() => rig.Opened >= 2, "neue Verbindung", seconds: 15);
        var log = TestFolder.LogSince(mark);
        Assert.Contains("(attempt 3)", log);
        Assert.Contains("stays in program 4", log);
    }

    [Fact]
    public void CockpitDriftingFromTarget_IsCorrected()
    {
        using var rig = new Rig();
        rig.Start();
        var setWatt = rig.Sim.Count(0x51);

        rig.Sim.WattSteps = 10; // cockpit shows 50 W instead of 100 W

        Rig.WaitFor(() => rig.Sim.WattSteps == 20, "Ziel wieder 100 W");
        Assert.True(rig.Sim.Count(0x51) > setWatt);
    }
}
