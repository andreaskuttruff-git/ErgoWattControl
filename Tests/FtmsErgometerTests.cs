namespace ErgoWattControl.Tests;

/// <summary>
/// The connection loop of <see cref="FtmsErgometer"/> against a simulated Bluetooth trainer: control, target power,
/// the cockpit behind the ErgoBridge, lost connections and refused control.
/// </summary>
public class FtmsErgometerTests
{
    private sealed class Rig : IDisposable
    {
        private readonly List<ErgometerData> _data = [];
        private readonly List<ErgometerStatus> _status = [];
        public readonly SimulatedFtmsLink Link;
        public readonly FtmsErgometer Device;

        public Rig(SimulatedFtmsLink? link = null)
        {
            Link = link ?? new SimulatedFtmsLink();
            Device = new FtmsErgometer(Link, retryDelay: TimeSpan.FromMilliseconds(200));
            Device.DataReceived += d => { lock (_data) _data.Add(d); };
            Device.StatusChanged += s => { lock (_status) _status.Add(s); };
        }

        public SimulatedFtmsLink.SimulatedTrainer Trainer => Link.Current!;
        public int Received { get { lock (_data) return _data.Count; } }
        public ErgometerData? Last { get { lock (_data) return _data.Count > 0 ? _data[^1] : null; } }
        public ErgometerStatus? LastStatus { get { lock (_status) return _status.Count > 0 ? _status[^1] : null; } }
        public bool StatusSeen(string text) { lock (_status) return _status.Any(s => s.Text == text); }

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
    public void Connects_TakesControl_AndDeliversData()
    {
        using var rig = new Rig();
        rig.Start();

        Assert.Equal(1, rig.Trainer.Count(FtmsProtocol.OpRequestControl));
        Assert.Equal(1, rig.Trainer.Count(FtmsProtocol.OpStartOrResume));
        Assert.Equal(L.F("Ftms_InfoBridge", "ErgoBridge", "8008", 800), rig.Device.Capabilities.Info);
        Assert.Equal(800, rig.Device.Capabilities.MaxWatt);
        Assert.NotNull(rig.Device.Capabilities.CadenceRange); // the Daum brake behind the bridge
        Assert.Equal(L.F("Ftms_DisplayNameOf", "ErgoBridge"), rig.Device.DisplayName);
        Assert.Equal(L.F("Ftms_Connected", "ErgoBridge"), rig.LastStatus!.Text);

        var d = rig.Last!;
        Assert.Equal(85, d.Rpm);
        Assert.True(d.Pedaling);
        Assert.Equal(120, d.Pulse);
        Assert.Equal(29.75, d.SpeedKmh!.Value, 2);
        Assert.Equal(10.0, d.DistanceKm!.Value, 3);
        Assert.Equal(600, d.TimeSeconds);
    }

    [Fact]
    public void TargetPower_ReachesTheTrainer_OnlyWhenChanged()
    {
        using var rig = new Rig();
        rig.Device.SetTargetPower(150);
        rig.Start();
        Rig.WaitFor(() => rig.Last?.Watt == 150, "150 W");

        rig.Device.SetTargetPower(150);
        rig.Device.SetTargetPower(230);
        Rig.WaitFor(() => rig.Last?.Watt == 230, "230 W");
        Thread.Sleep(700); // a few loop rounds without a change
        Assert.Equal(2, rig.Trainer.Count(FtmsProtocol.OpSetTargetPower));
    }

    [Fact]
    public void CockpitBehindTheBridge_Missing_MeansNotConnected_AndNoData()
    {
        var link = new SimulatedFtmsLink { CockpitConnected = false, CockpitType = -1 };
        using var rig = new Rig(link);
        rig.Device.Start();
        Rig.WaitFor(() => rig.StatusSeen(L.F("Ftms_CockpitMissing", "ErgoBridge")), "Cockpit fehlt");
        Thread.Sleep(400);
        Assert.False(rig.Device.IsConnected);
        Assert.Equal(0, rig.Received);
        Assert.Equal(L.F("Ftms_InfoBridgeUnknown", "ErgoBridge", 800), rig.Device.Capabilities.Info);

        link.CockpitType = 40;
        link.CockpitConnected = true;
        Rig.WaitFor(() => rig.Device.IsConnected && rig.Received > 0, "Cockpit wieder da");
        Assert.Equal(L.F("Ftms_Connected", "ErgoBridge"), rig.LastStatus!.Text);
    }

    [Fact]
    public void OldCockpitBehindTheBridge_LimitsTo400W()
    {
        using var rig = new Rig(new SimulatedFtmsLink { MaxWatt = 400, CockpitType = 10 });
        rig.Device.SetTargetPower(600);
        rig.Start();
        Assert.Equal(400, rig.Device.Capabilities.MaxWatt);
        Rig.WaitFor(() => rig.Last?.Watt == 400, "auf 400 W begrenzt");
    }

    [Fact]
    public void LostConnection_IsNoted_ReconnectsAndSendsTheTargetAgain()
    {
        var mark = TestFolder.LogMark();
        using var rig = new Rig();
        rig.Device.SetTargetPower(180);
        rig.Start();
        Rig.WaitFor(() => rig.Last?.Watt == 180, "180 W");

        rig.Trainer.Drop();
        Rig.WaitFor(() => !rig.Device.IsConnected, "Verbindung weg");
        rig.Link.Available = true;
        Rig.WaitFor(() => rig.Link.Connects == 2 && rig.Device.IsConnected, "neu verbunden");
        Rig.WaitFor(() => rig.Trainer.TargetWatt == 180, "Ziel erneut gesendet");
        Rig.WaitFor(() => TestFolder.LogSince(mark).Contains("reconnected"), "Wiederverbindung im Log");
        Assert.Contains("Bluetooth: connection to ErgoBridge lost", TestFolder.LogSince(mark));
    }

    [Fact]
    public void TrainerMissing_IsReported_AndFoundLater()
    {
        using var rig = new Rig(new SimulatedFtmsLink { Available = false });
        rig.Device.Start();
        Rig.WaitFor(() => rig.StatusSeen(L.T("Ftms_NotFound")), "nicht gefunden");
        Assert.False(rig.Device.IsConnected);

        rig.Link.Available = true;
        Rig.WaitFor(() => rig.Device.IsConnected, "gefunden");
    }

    [Fact]
    public void ControlRefused_IsReported_AndTriedAgain()
    {
        var mark = TestFolder.LogMark();
        using var rig = new Rig(new SimulatedFtmsLink { RefuseControl = true });
        rig.Device.Start();
        Rig.WaitFor(() => rig.StatusSeen(L.F("Ftms_Retrying", L.F("Ftms_ControlRefused", "ErgoBridge"))), "Kontrolle verweigert");
        Rig.WaitFor(() => rig.Link.Connects >= 3, "mehrere Versuche");
        Assert.False(rig.Device.IsConnected);
        // Refused again and again: one line in the log, in English, and no "lost" - it never rode.
        var log = TestFolder.LogSince(mark);
        Assert.Single(log.Split('\n'), line => line.Contains("ErgoBridge refuses control"));
        Assert.DoesNotContain("connection to ErgoBridge lost", log);

        rig.Link.RefuseControl = false;
        Rig.WaitFor(() => rig.Device.IsConnected, "Kontrolle erhalten", seconds: 15);
    }

    [Fact]
    public void ControlLostWhileRiding_IsRequestedAgain()
    {
        using var rig = new Rig();
        rig.Start();
        rig.Trainer.ForgetControl();

        rig.Device.SetTargetPower(210);
        Rig.WaitFor(() => rig.Last?.Watt == 210, "210 W nach erneuter Kontrolle");
        Assert.Equal(2, rig.Trainer.Count(FtmsProtocol.OpRequestControl));
        Assert.Equal(1, rig.Link.Connects);
    }

    [Fact]
    public void PlainFtmsTrainer_UsesItsPowerRange_WithoutCadenceWindow()
    {
        using var rig = new Rig(new SimulatedFtmsLink { IsErgoBridge = false, Name = "KICKR", MaxWatt = 700 });
        rig.Start();
        Assert.Equal(700, rig.Device.Capabilities.MaxWatt);
        Assert.Equal(5, rig.Device.Capabilities.WattStep);
        Assert.Null(rig.Device.Capabilities.CadenceRange);
        Assert.Equal(L.F("Ftms_Info", "KICKR", 700, 5), rig.Device.Capabilities.Info);
    }
}
