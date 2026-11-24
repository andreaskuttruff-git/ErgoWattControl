namespace ErgoWattControl;

/// <summary>
/// The application's only window: borderless, sized for the 1280x1024 monitor above the bike and readable from the saddle.
/// Settings, recording and power control are all reachable from here.
/// </summary>
public sealed class RideForm : Form
{
    private static readonly Color Background = Color.FromArgb(16, 16, 16);
    private static readonly Color ButtonBack = Color.FromArgb(40, 40, 40);
    private static readonly Color Caption = Color.FromArgb(150, 150, 150);
    private static readonly Color PowerColor = Color.Gold;
    private static readonly Color PulseColor = Color.FromArgb(255, 110, 110);
    private static readonly Color RpmColor = Color.FromArgb(80, 200, 255);
    private static string PowerHint => L.T("Ride_PowerHint");
    private static string GearHint => L.T("Ride_GearHint");
    private static string ReplayHint => L.T("Ride_ReplayHint");

    private readonly AppSettings _settings;
    private readonly ErgoController _controller = new();
    /// <summary>Mode (free, route, replay/program), recording and run settings; this window only shows them.</summary>
    private readonly TrainingController _training;
    // The "ErgoWattControl beenden" dialog is open while the ride keeps running behind it (closing via ✕ or "Beenden").
    private bool _closeDialogOpen;
    private readonly RouteProfile _profile = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Label _powerCaption = CreateValueCaption(L.T("Ride_Power"));
    private ToolStripMenuItem _menuRouteStart = null!;
    private readonly WorkoutProfile _workoutProfile = new() { Dock = DockStyle.Fill, Visible = false };
    private ToolStripMenuItem _menuReplayStart = null!;
    private ToolStripMenuItem _menuProgramStart = null!;
    /// <summary>Ends whatever runs: route, replay, program or the recording of a free training.</summary>
    private ToolStripMenuItem _menuTrainingEnd = null!;
    private ToolStripMenuItem _menuRouteRecent = null!, _menuReplayRecent = null!, _menuProgramRecent = null!;

    /// <summary>A route or a replayed training sets the power; free training is controlled by the rider.</summary>
    private bool Guided => _training.Guided;

    private readonly Label _powerHint = CreateCaption(PowerHint);
    private readonly Label _power = CreateValue(88, PowerColor);
    private readonly Label _pulse = CreateValue(88, PulseColor);
    private static readonly Font SecondaryFont = new("Segoe UI", 30, FontStyle.Bold);
    // Fixed-width, right-aligned value fields (sized for the widest expected value) so captions and units never shift.
    private readonly Label _rpm = CreateSecondaryValue(RpmColor, "188");
    private readonly Label _speed = CreateSecondaryValue(Color.White, "88");
    private readonly Label _time = CreateSecondaryValue(Color.White, "8:88:88");
    private readonly Label _timeCaption = CreateSecondaryText(L.T("Ride_Time"), Color.White);
    private readonly TrendGraph _powerGraph = new([new(L.T("Ride_GraphPower"), PowerColor), new("RPM", RpmColor)], bottom: 0, minTop: 200, gridStep: 50, bandColor: RpmColor) { Dock = DockStyle.Fill, Margin = new Padding(10, 4, 10, 4) };
    private readonly Label _rpmCaption = CreateSecondaryText("RPM", RpmColor);
    // Fixed width even when empty, so the RPM group does not jump when the arrow appears.
    private readonly Label _rpmHint = CreateSecondaryValue(Color.OrangeRed, "▲");
    private readonly TrendGraph _pulseGraph = new([new(L.T("Ride_GraphPulse"), PulseColor)], bottom: 60, minTop: 160, gridStep: 20) { Dock = DockStyle.Fill, Margin = new Padding(10, 4, 10, 4) };
    // Right-aligned in equal cells: the unit stays put and extra digits grow leftwards into reserved space.
    private readonly Label _km = CreateTotal();
    private readonly Label _kj = CreateTotal();
    private readonly Label _revs = CreateTotal();
    private readonly Button _btnRecord = CreateButton(L.T("Ride_Record"), 16);
    /// <summary>Switches between ride monitor and main monitor; named after where it moves the window.</summary>
    private readonly Button _btnScreen = CreateButton(L.T("Ride_ToRideView"), 16);
    private readonly ToolTip _toolTip = new() { InitialDelay = 300, AutoPopDelay = 15000 };
    private readonly Label _status = new() { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 18), TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true };

    /// <param name="demo">Use the demo ergometer (a simulated rider) whatever device the settings name.</param>
    /// <param name="demoRoute">In demo mode: GPX file to start riding immediately, or a recording/program to replay.</param>
    /// <param name="startOnMainScreen">Open on the primary monitor instead of the ride monitor; "Fahransicht" still moves it there.</param>
    public RideForm(AppSettings settings, bool demo = false, string? demoRoute = null, bool startOnMainScreen = false)
    {
        _settings = settings;
        _training = new TrainingController(settings, _controller);
        _demoStart = demo;
        Text = "Ergo Watt Control";
        Icon = AppIcon.Value;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        BackColor = Background;
        FitTo(startOnMainScreen ? Screen.PrimaryScreen! : RiderScreen());

        BuildLayout();
        EnableMouseDrag(this);
        WattWheel.Attach(this, StepWatt);
        _controller.SetUserLimit(_settings.Ride.MaxWattLimit);

        _btnRecord.Click += (_, _) => RecordOrEnd();
        _controller.DataReceived += d => PostToUi(() => ShowData(d));
        _controller.StatusChanged += s => PostToUi(() => ShowStatus(s.Text, s.Connected, s.Detail));
        Shown += (_, _) =>
        {
            _controller.Use(demo ? new DemoErgometer() : ErgometerTypes.Create(_settings.Device, _settings));
            if (demo && demoRoute is not null)
            {
                // A FIT activity without power data (e.g. an MTB tour) is ridden as a route, like a GPX file.
                if (demoRoute.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase) || (FitImport.IsFit(demoRoute) && IsRouteOnly(demoRoute)))
                    StartRoute(demoRoute);
                else
                    StartReplay(demoRoute);
            }
        };
        FormClosing += (_, e) =>
        {
            // The ✕ sits right next to the pulse display: don't let a stray click end a running training.
            // Free training, route and replay all get the same dialog: save/rename, discard, or carry on riding.
            if (e.CloseReason == CloseReason.UserClosing && (_training.Recorder is not null || Guided))
            {
                // A route or replay records from the first pedal stroke on; before that the dialog shows the name it would get.
                using var dialog = new RenameRecordingForm(_training.RecordingPath, closingApp: true);
                DialogResult result;
                _closeDialogOpen = true;
                try { result = dialog.ShowDialog(this); }
                finally { _closeDialogOpen = false; }
                if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                var recorder = StopRecording();
                if (recorder is not null)
                    RenameRecordingForm.Apply(this, recorder.FilePath, result, dialog.NewName);
            }
            StopRecording();
            _controller.Dispose();
        };

        ShowTarget(_controller.TargetWatt);
        ShowStatus(L.T("Ride_Connecting"), false);
    }

    private void BuildLayout()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(16) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 8));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 29));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 29));
        // The footer only needs the height of its buttons; everything else goes to the graphs.
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        grid.Controls.Add(CreatePowerCell(), 0, 0);
        var closeButton = CreateIconButton(L.T("Ride_Close"), (g, pen, c) =>
        {
            const int r = 8;
            g.DrawLine(pen, c.X - r, c.Y - r, c.X + r, c.Y + r);
            g.DrawLine(pen, c.X - r, c.Y + r, c.X + r, c.Y - r);
        });
        closeButton.Dock = DockStyle.Right;
        closeButton.Width = 48;
        closeButton.Click += (_, _) => Close();
        var pulseHeader = new Panel { Dock = DockStyle.Top, Height = 36 };
        // What is being ridden (route, program, recording) – above the pulse, next to the close button.
        pulseHeader.Controls.Add(_runName);
        pulseHeader.Controls.Add(closeButton);
        _runNameTip.SetToolTip(_runName, "");
        _pulse.Dock = DockStyle.Fill;
        var pulseCell = new Panel { Dock = DockStyle.Fill, Margin = new Padding(10, 0, 10, 0) };
        pulseCell.Controls.Add(_pulse);
        // Right next to the big number: the pulse of the original ride at this point, green (positioned in ShowPulseThen).
        _pulse.Controls.Add(_pulseThen);
        _pulse.Resize += (_, _) => PlacePulseThen();
        pulseCell.Controls.Add(CreateValueCaption(L.T("Ride_Pulse")));
        pulseCell.Controls.Add(pulseHeader);
        grid.Controls.Add(pulseCell, 1, 0);

        var secondary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        for (var i = 0; i < 3; i++)
            secondary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        _rpmHint.TextAlign = ContentAlignment.MiddleCenter;
        _rpmHint.Margin = new Padding(8, 0, 0, 0);
        secondary.Controls.Add(SecondaryGroup(_rpmCaption, _rpm, _rpmHint), 0, 0);
        secondary.Controls.Add(SecondaryGroup(_speed, CreateSecondaryText("km/h", Color.White)), 1, 0);
        secondary.Controls.Add(SecondaryGroup(_timeCaption, _time), 2, 0);
        grid.Controls.Add(secondary, 0, 1);
        grid.SetColumnSpan(secondary, 2);

        grid.Controls.Add(_powerGraph, 0, 2);
        grid.SetColumnSpan(_powerGraph, 2);
        // Riding a route swaps the pulse graph for the elevation profile; the pulse stays visible as the big number.
        var lowerGraph = new Panel { Dock = DockStyle.Fill, Margin = new Padding(10, 4, 10, 0) };
        _pulseGraph.Margin = Padding.Empty;
        lowerGraph.Controls.Add(_pulseGraph);
        lowerGraph.Controls.Add(_profile);
        lowerGraph.Controls.Add(_workoutProfile);
        grid.Controls.Add(lowerGraph, 0, 3);
        grid.SetColumnSpan(lowerGraph, 2);

        _btnScreen.Click += (_, _) => ToggleScreen();
        // Also at start and after dragging the window to another monitor.
        Shown += (_, _) => UpdateScreenButton();
        Move += (_, _) => UpdateScreenButton();
        foreach (var b in new[] { _btnRecord, _btnScreen })
        {
            b.AutoSize = true;
            // Buttons only grow by default; the record button must shrink back after "■ Training beenden".
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.Margin = new Padding(6, 8, 6, 0);
            ApplyOutlineStyle(b);
        }
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.None, Margin = Padding.Empty };
        buttons.Controls.AddRange([_btnRecord, _btnScreen]);

        // Equal side columns keep the button panel centred however long the texts left and right get.
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var totals = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 0, 50, 0) };
        for (var i = 0; i < 3; i++)
            totals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        totals.Controls.Add(_km, 0, 0);
        totals.Controls.Add(_kj, 1, 0);
        totals.Controls.Add(_revs, 2, 0);
        footer.Controls.Add(totals, 0, 0);
        footer.Controls.Add(buttons, 1, 0);
        footer.Controls.Add(_status, 2, 0);
        grid.Controls.Add(footer, 0, 4);
        grid.SetColumnSpan(footer, 2);

        Controls.Add(grid);

        _rpm.Text = "--";
        _speed.Text = "--";
        _time.Text = "-:--:--";
        _pulse.Text = "--";
        ShowTotals(0, 0, 0, Caption);
    }

    private Control CreatePowerCell()
    {
        var menuButton = CreateIconButton(L.T("Ride_Menu"), (g, pen, c) =>
        {
            const int halfWidth = 10, gap = 7;
            for (var dy = -gap; dy <= gap; dy += gap)
                g.DrawLine(pen, c.X - halfWidth, c.Y + dy, c.X + halfWidth, c.Y + dy);
        });
        menuButton.Dock = DockStyle.Left;
        menuButton.Width = 48;
        var menu = new ContextMenuStrip { Font = new Font("Segoe UI", 14) };
        _menuRouteStart = new ToolStripMenuItem(L.T("Menu_Route"), null, (_, _) => PickRoute());
        _menuReplayStart = new ToolStripMenuItem(L.T("Menu_Replay"), null, (_, _) => PickReplay());
        _menuProgramStart = new ToolStripMenuItem(L.T("Menu_Program"), null, (_, _) => PickProgram());
        _menuTrainingEnd = new ToolStripMenuItem(L.T("Menu_EndTraining"), null, (_, _) => EndTraining()) { Enabled = false };
        _menuRouteRecent = RecentMenu(PickRoute);
        _menuReplayRecent = RecentMenu(PickReplay);
        _menuProgramRecent = RecentMenu(PickProgram);
        menu.Items.Add(_menuRouteStart);
        menu.Items.Add(_menuRouteRecent);
        menu.Items.Add(_menuReplayStart);
        menu.Items.Add(_menuReplayRecent);
        menu.Items.Add(_menuProgramStart);
        menu.Items.Add(_menuProgramRecent);
        menu.Items.Add(_menuTrainingEnd);
        menu.Opening += (_, _) => FillRecentMenus();
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.T("Menu_Export"), null, (_, _) => OpenExport());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.T("Menu_Settings"), null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripMenuItem(L.T("Menu_Help"), null, (_, _) => HelpWindow.Show(this, "start")) { ShortcutKeyDisplayString = "F1" });
        menu.Items.Add(L.T("Menu_Info"), null, (_, _) =>
        {
            using var info = new InfoForm();
            info.ShowDialog(this);
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.T("Menu_Exit"), null, (_, _) => Close());
        menuButton.Click += (_, _) => menu.Show(menuButton, new Point(0, menuButton.Height));

        var header = new Panel { Dock = DockStyle.Top, Height = 36 };
        header.Controls.Add(menuButton);

        var minus = CreateButton("−", 40);
        var plus = CreateButton("+", 40);
        ApplyOutlineStyle(minus);
        ApplyOutlineStyle(plus);
        minus.Click += (_, _) => StepWatt(-5);
        plus.Click += (_, _) => StepWatt(+5);
        var valueRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        valueRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        valueRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        valueRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        minus.Dock = plus.Dock = _power.Dock = DockStyle.Fill;
        minus.Margin = plus.Margin = new Padding(0, 30, 0, 30);
        valueRow.Controls.Add(minus, 0, 0);
        _powerHint.Dock = DockStyle.Top;
        _powerHint.Height = 28;
        _powerHint.Font = new Font("Segoe UI", 13, FontStyle.Bold);
        _powerHint.TextAlign = ContentAlignment.MiddleCenter;
        var valueCell = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        valueCell.Controls.Add(_power);
        valueCell.Controls.Add(_powerHint);
        valueCell.Controls.Add(_powerCaption);
        valueRow.Controls.Add(valueCell, 1, 0);
        valueRow.Controls.Add(plus, 2, 0);

        var panel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(10, 0, 10, 0) };
        panel.Controls.Add(valueRow);
        panel.Controls.Add(header);
        return panel;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Handled here rather than in KeyDown so the keys work regardless of which control has focus.
        // Numpad digits mirror the arrow keys printed on them, so a wireless keypad on the handlebar works with NumLock on or off.
        switch (keyData)
        {
            // Enter = the record button: free training starts/stops the recording; route, replay and program open
            // "Training beenden" (Enter again = OK there, Esc = Weiterfahren).
            case Keys.Enter:
                RecordOrEnd();
                return true;
            case Keys.NumPad0:
            case Keys.Insert:
            case Keys.D0:
                ToggleScreen();
                return true;
            case Keys.F1:
                HelpWindow.Show(this, HelpSection);
                return true;
        }
        var step = keyData switch
        {
            Keys.Up or Keys.NumPad8 or Keys.Oemplus or Keys.Add => 5,
            Keys.Down or Keys.NumPad2 or Keys.OemMinus or Keys.Subtract => -5,
            Keys.PageUp or Keys.NumPad9 => 25,
            Keys.PageDown or Keys.NumPad3 => -25,
            _ => 0,
        };
        if (step == 0)
            return base.ProcessCmdKey(ref msg, keyData);
        StepWatt(step);
        return true;
    }

    /// <summary>All power inputs (keys, wheel, buttons) arrive here; on a route they shift gears instead (5 W ≙ 1 gear).</summary>
    private void StepWatt(int step)
    {
        _training.Step(step);
        switch (_training.Mode)
        {
            case RouteTraining:
                ShowGear();
                break;
            case ReplayTraining:
                ShowReplay();
                break;
            default:
                ShowTarget(_controller.TargetWatt);
                break;
        }
    }

    /// <summary>The record button and Enter: free training starts/stops the recording; route, replay and program open "Training beenden".</summary>
    private void RecordOrEnd()
    {
        if (Guided) EndGuided(completed: false);
        else if (_training.Recorder is null) StartRecording();
        else FinishFreeRecording();
    }

    private void ShowTarget(int watt) => _power.Text = $"{watt} W";

    private void ShowGear()
    {
        if (_training.Mode is not RouteTraining { Session: var route })
            return;
        _power.Text = $"G {route.Gear}";
        _powerCaption.Text = L.F("Ride_GearCaption", route.TargetWatt);
    }

    private void ShowReplay()
    {
        if (_training.Mode is not ReplayTraining { Session: var replay })
            return;
        _power.Text = $"{replay.TargetWatt} W";
        _powerCaption.Text = replay.Offset == 0 ? L.T("Ride_Profile") : L.F("Ride_ProfileShifted", replay.Offset);
        var next = replay.NextChange() is { } change
            ? L.F("Ride_NextChange", WorkoutProfile.Format(change.In.TotalSeconds), change.Watt)
            : L.T("Ride_NoMoreChange");
        _workoutProfile.SetPosition(replay.Elapsed.TotalSeconds,
            $"{next}      {WorkoutProfile.Format(replay.Elapsed.TotalSeconds)} / {WorkoutProfile.Format(replay.Workout.Duration)}");
    }

    private void ShowData(ErgometerData d)
    {
        // A route or replay records from the first pedal stroke on, so getting on the bike isn't in the recording.
        if (_training.Update(d, DateTime.Now))
            StartRecording();
        switch (_training.Mode)
        {
            case ReplayTraining { Session: var replay }:
                _powerHint.Text = ReplayHint;
                _workoutProfile.AddPulse(replay.Elapsed.TotalSeconds, d.Pulse);
                ShowReplay();
                break;
            case RouteTraining { Session: var route }:
                _powerHint.Text = GearHint;
                ShowGear();
                _profile.SetPosition(route.Position, route.GradePercent);
                _profile.AddPulse(route.Position, d.Pulse);
                break;
            default:
                // The big number is the target the rider controls; show the cockpit's actual value only when it deviates.
                _powerHint.Text = Math.Abs(d.Watt - _controller.TargetWatt) > 5 ? L.F("Ride_Actual", d.Watt) : PowerHint;
                break;
        }
        _pulse.Text = d.Pulse > 0 ? d.Pulse.ToString() : "--";
        ShowPulseThen();
        _rpm.Text = d.Rpm.ToString();
        // Outside this window the brake cannot hold the power; only flag it while actually pedalling.
        // Devices without such a window (CadenceRange null) show no band and no arrows.
        (int Min, int Max)? rpmRange = _controller.Capabilities.CadenceRange?.Invoke(d.Watt);
        var tooSlow = rpmRange is { } slow && d.Rpm > 0 && d.Rpm < slow.Min;
        var tooFast = rpmRange is { } fast && d.Rpm > fast.Max;
        _rpm.ForeColor = _rpmCaption.ForeColor = tooSlow || tooFast ? Color.OrangeRed : RpmColor;
        _rpmHint.Text = tooSlow ? "▲" : tooFast ? "▼" : "";
        _speed.Text = (_training.Mode.SpeedKmh ?? d.SpeedKmh) is { } kmh ? $"{kmh:0}" : "--";
        // While recording, show the recording's own duration (pedalling time only); otherwise the cockpit's pedaling time.
        var time = _training.Recorder?.Elapsed ?? (Guided ? TimeSpan.Zero : TimeSpan.FromSeconds(d.TimeSeconds ?? 0));
        _time.Text = $"{time:h\\:mm\\:ss}";
        _powerGraph.Add([d.Watt, d.Rpm], rpmRange);
        _pulseGraph.Add(d.Pulse);

        // Distance/energy only count within a recording; the cockpit's own totals since power-on are not shown.
        if (_training.Recorder is not { } recorder)
            return;
        try
        {
            _training.Record(d);
            ShowTotals(recorder.DistanceKm, recorder.EnergyKJ, recorder.Revolutions, Color.OrangeRed);
            if (_training.ReachedEnd)
                EndGuided(completed: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppPaths.Log("Recording stopped", ex);
            StopRecording();
            DarkMessageBox.Show(this, L.F("Rec_Stopped", ex.Message), L.T("Caption_Recording"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowTotals(double km, double kj, double revolutions, Color color)
    {
        _km.Text = $"{km:0.0} km";
        _kj.Text = $"{kj:0} kJ";
        _revs.Text = L.F("Ride_Revolutions", revolutions);
        _km.ForeColor = _kj.ForeColor = _revs.ForeColor = color;
    }

    private void ShowStatus(string text, bool ok, string? detail = null)
    {
        _status.Text = text;
        _status.ForeColor = ok ? Color.LimeGreen : Color.OrangeRed;
        // The label may be cut off with "…" and keeps errors short, so the tooltip carries the full text.
        _toolTip.SetToolTip(_status, detail is null ? text : $"{text}\n{detail}");
    }

    private void StartRecording()
    {
        if (!_controller.IsConnected)
        {
            DarkMessageBox.Show(this, L.T("Rec_NotConnected"), L.T("Caption_Recording"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            _training.StartRecording();
        }
        catch (Exception ex)
        {
            AppPaths.Log("Creating the recording", ex);
            // Route, replay and program: shown once – the controller doesn't try again in this run, the ride goes on.
            var text = Guided
                ? L.F("Rec_CreateFailedGuided", ex.Message, _settings.RecordingDirectory)
                : L.F("Rec_CreateFailed", ex.Message);
            DarkMessageBox.Show(this, text, L.T("Caption_Recording"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        UpdateRecordButton();
        _time.Text = "0:00:00";
        _time.ForeColor = _timeCaption.ForeColor = Color.OrangeRed;
        ShowTotals(0, 0, 0, Color.OrangeRed);
        // Recording already runs (a guided ride goes on behind the message); just say where it ends up.
        if (_training.RecordingInFallbackFolder && _training.Recorder is { } recorder)
            DarkMessageBox.Show(this, L.F("Rec_InFallbackFolder", _settings.RecordingDirectory, Path.GetDirectoryName(recorder.FilePath)),
                L.T("Caption_Recording"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private JsonRecorder? StopRecording()
    {
        if (_training.StopRecording() is not { } recorder)
            return null;
        UpdateRecordButton();
        _time.ForeColor = _timeCaption.ForeColor = Color.White;
        ShowTotals(0, 0, 0, Caption);
        return recorder;
    }

    /// <summary>"Stop" in free training: close the file, then offer to rename it.</summary>
    private void FinishFreeRecording()
    {
        if (_training.Recorder is not { } recorder || _closeDialogOpen)
            return;
        // One dialog before the recording stops: OK, Verwerfen, or Weiterfahren (keeps recording).
        using var dialog = new RenameRecordingForm(recorder.FilePath, endTraining: true);
        var answer = ShowEndDialog(dialog);
        if (answer == DialogResult.Cancel)
            return;
        StopRecording();
        if (File.Exists(recorder.FilePath))
            RenameRecordingForm.Apply(this, recorder.FilePath, answer, dialog.NewName);
    }

    /// <summary>Free training: start/stop recording. Route, replay, program: recording runs automatically, the button ends the training.</summary>
    private void UpdateRecordButton()
    {
        _btnRecord.Text = Guided ? L.T("Ride_EndTraining")
            : _training.Recorder is null ? L.T("Ride_Record") : L.T("Ride_Stop");
        _btnRecord.ForeColor = _training.Recorder is null ? Color.White : Color.OrangeRed;
        _menuTrainingEnd.Enabled = Guided || _training.Recorder is not null;
    }

    private void SetGuidedMenus() =>
        _menuRouteStart.Enabled = _menuReplayStart.Enabled = _menuProgramStart.Enabled = !Guided;

    /// <summary>"Kürzlich" below a start item: indented and grey; its submenu lists the last files ridden from that dialog.</summary>
    private static ToolStripMenuItem RecentMenu(Action<string?> start) => new("      " + L.T("Menu_Recent"))
    {
        ForeColor = Color.FromArgb(170, 170, 170),
        Tag = start,
    };

    /// <summary>Fills the three "Kürzlich" submenus when the menu opens: files that no longer exist are left out.</summary>
    private void FillRecentMenus()
    {
        var recent = _settings.Recent;
        Fill(_menuRouteRecent, recent.Routes, FitImport.DisplayName);
        Fill(_menuReplayRecent, recent.Recordings, RecentRecordingName);
        Fill(_menuProgramRecent, recent.Programs, RecentProgramName);

        void Fill(ToolStripMenuItem item, List<string> files, Func<string, string> name)
        {
            var start = (Action<string?>)item.Tag!;
            item.DropDownItems.Clear();
            foreach (var file in RecentRides.Existing(files))
            {
                var entry = new ToolStripMenuItem(Safe(name, file), null, (_, _) => start(file)) { ToolTipText = file };
                item.DropDownItems.Add(entry);
            }
            // No submenu without entries: greyed out, without the arrow. Nothing new during a guided training.
            item.Enabled = item.DropDownItems.Count > 0 && !Guided;
        }

        static string Safe(Func<string, string> name, string file)
        {
            try { return name(file); }
            catch (Exception) { return Path.GetFileNameWithoutExtension(file); }
        }
    }

    /// <summary>A recording in "Kürzlich": date first, the names often look alike.</summary>
    private static string RecentRecordingName(string file) =>
        $"{File.GetLastWriteTime(file):d}  " + (FitImport.IsFit(file) ? FitImport.DisplayName(file) : Path.GetFileNameWithoutExtension(file));

    private static string RecentProgramName(string file) => TrainingProgram.Load(file).DisplayName;

    /// <summary>Remembers a file just started from a dialog for "Kürzlich" and saves the settings.</summary>
    private void RememberRecent(List<string> list, string? file)
    {
        if (file is null)
            return;
        RecentRides.Remember(list, file);
        try { _settings.Save(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Menu "Training beenden": ends the route or replay, or stops the free training's recording like "■ Stop".</summary>
    private void EndTraining()
    {
        if (Guided)
            EndGuided(completed: false);
        else if (_training.Recorder is not null)
            FinishFreeRecording();
    }

    private void PickReplay(string? preselect = null)
    {
        if (Guided)
            return;
        using var picker = ReplayPickerForm.ForRecordings(_settings, _training.SaveRideDefaults);
        if (preselect is not null)
            picker.Preselect(preselect);
        if (picker.ShowDialog(this) != DialogResult.OK || picker.Workout is not { } workout)
            return;
        RememberRecent(_settings.Recent.Recordings, picker.SelectedFile);
        // The checkbox is the setting: remember the choice for the next replay.
        if (workout.HasPulse && picker.ShowOriginalPulse != _settings.ReplayShowOriginalPulse)
        {
            _settings.ReplayShowOriginalPulse = picker.ShowOriginalPulse;
            try { _settings.Save(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        if (picker.Route is { } route)
            BeginRoute(route, picker.RecordingFileName, picker.RideValues);
        else
            BeginReplay(workout, picker.RecordingFileName, programFile: null, picker.RideValues, picker.PowerOffset);
    }

    private static bool IsRouteOnly(string fit)
    {
        try { var f = FitImport.Load(fit); return !f.HasPower && f.HasTrack; }
        catch (InvalidDataException) { return false; }
        catch (IOException) { return false; }
    }

    /// <summary>Replays a file given on the command line (demo), without the start dialog.</summary>
    private void StartReplay(string csvPath)
    {
        WorkoutRecording workout;
        try
        {
            workout = WorkoutRecording.Load(csvPath);
        }
        catch (Exception ex)
        {
            ShowLoadError("Load_RecordingFailed", L.T("Caption_Replay"), csvPath, ex);
            return;
        }
        // A program given on the command line is recorded like one started from the dialog (Prog_… file name).
        BeginReplay(workout, customName: null, programFile: workout.IsProgram && csvPath.Length > 0 ? Path.GetFileNameWithoutExtension(csvPath) : null);
    }

    private void PickProgram(string? preselect = null)
    {
        if (Guided)
            return;
        using var picker = ReplayPickerForm.ForPrograms(ProgramFiles.Folder, ProgramFiles.CustomFolder, _settings, _training.SaveRideDefaults);
        if (preselect is not null)
            picker.Preselect(preselect);
        if (picker.ShowDialog(this) != DialogResult.OK || picker.Workout is not { } workout)
            return;
        RememberRecent(_settings.Recent.Programs, picker.SelectedFile);
        BeginReplay(workout, picker.RecordingFileName, picker.ProgramFile, picker.RideValues, picker.PowerOffset);
    }

    /// <summary>The pulse of the original ride at the same point (route: same kilometre, recording: same time), just the number.</summary>
    private readonly Label _pulseThen = new()
    {
        AutoSize = true,
        ForeColor = Color.FromArgb(110, 210, 120),
        BackColor = Background,
        Font = new Font("Segoe UI", 36, FontStyle.Bold),
        Visible = false,
    };

    /// <summary>Right of the big pulse number on the same baseline (bottoms of the digits flush); the big number stays where it is.</summary>
    private void PlacePulseThen()
    {
        if (!_pulseThen.Visible)
            return;
        var number = TextRenderer.MeasureText(_pulse.Text, _pulse.Font);
        // Baseline of the big number: its text block is centred in the label, the baseline lies one ascent below its top.
        float Ascent(Font f) => f.FontFamily.GetCellAscent(f.Style) * f.GetHeight() / f.FontFamily.GetLineSpacing(f.Style);
        var baseline = (_pulse.Height - number.Height) / 2f + Ascent(_pulse.Font);
        var small = TextRenderer.MeasureText(_pulseThen.Text, _pulseThen.Font);
        // −2 px: GDI places the smaller text marginally lower than the font metrics say (measured).
        var top = baseline - Ascent(_pulseThen.Font) - (_pulseThen.Height - small.Height) / 2f - 2 * DeviceDpi / 96f;
        var x = _pulse.Width / 2 + number.Width / 2 + 6;
        _pulseThen.Location = new Point(Math.Min(x, _pulse.Width - _pulseThen.Width), (int)Math.Round(top));
    }

    /// <summary>Only when the original pulse is switched on and the ride being repeated has one; programs have none.</summary>
    private void ShowPulseThen()
    {
        var then = _training.Mode.PulseThen(_settings.ReplayShowOriginalPulse);
        _pulseThen.Visible = then is not null;
        if (then is { } value)
        {
            _pulseThen.Text = value.ToString();
            PlacePulseThen();
        }
    }

    /// <summary>Name of the route, program or recording being ridden; empty in free training.</summary>
    private readonly Label _runName = new()
    {
        Dock = DockStyle.Fill,
        ForeColor = Color.FromArgb(200, 200, 200),
        Font = new Font("Segoe UI", 13, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleCenter,
        AutoEllipsis = true,
    };

    private readonly ToolTip _runNameTip = new();

    private void ShowRunName()
    {
        var (kind, name, file) = _training.Mode.RunName;
        _runName.Text = name.Length == 0 ? "" : $"{kind}: {name}";
        _runNameTip.SetToolTip(_runName, file is null ? _runName.Text : $"{_runName.Text}\n{file}");
    }

    private void BeginReplay(WorkoutRecording workout, string? customName, string? programFile, RideValues? runValues = null, int powerOffset = 0)
    {
        StopRecording();
        var replay = _training.BeginReplay(workout, customName, programFile, runValues, powerOffset);
        ShowRunName();
        _workoutProfile.SetWorkout(workout, replay.Session.WattAt, _settings.ReplayShowOriginalPulse);
        _workoutProfile.Visible = true;
        _pulseGraph.Visible = false;
        UpdateRecordButton();
        SetGuidedMenus();
        _powerHint.Text = ReplayHint;
        _time.Text = "0:00:00";
        ShowReplay();
    }

    private void OpenExport()
    {
        using var dialog = new ExportForm(_settings);
        dialog.ShowDialog(this);
    }

    /// <summary>F1: the help section for what is on screen now.</summary>
    private string HelpSection => _training.Mode.HelpSection;

    /// <summary>Shows why a route or training could not be loaded and keeps the full exception in the log.</summary>
    /// <param name="messageKey">Text with the placeholders {0} = reason, {1} = log file.</param>
    private void ShowLoadError(string messageKey, string caption, string path, Exception ex)
    {
        AppPaths.Log(path, ex);
        var reason = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : $"{ex.GetType().Name}: {ex.Message}";
        DarkMessageBox.Show(this, L.F(messageKey, reason, AppPaths.LogFile), caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void PickRoute(string? preselect = null)
    {
        if (Guided)
            return;
        Directory.CreateDirectory(_settings.GpxDirectory);
        using var picker = new RoutePickerForm(_settings, _controller.MaxWatt);
        if (preselect is not null)
            picker.Preselect(preselect);
        if (picker.ShowDialog(this) != DialogResult.OK || picker.Route is not { } route)
            return;
        RememberRecent(_settings.Recent.Routes, picker.SelectedFile);
        BeginRoute(route, picker.RecordingFileName);
    }

    /// <summary>Rides a file given on the command line (demo), without the start dialog.</summary>
    private void StartRoute(string gpxPath)
    {
        GpxRoute route;
        try
        {
            route = GpxRoute.Load(gpxPath);
        }
        catch (Exception ex)
        {
            ShowLoadError("Load_RouteFailed", L.T("Caption_Route"), gpxPath, ex);
            return;
        }
        BeginRoute(route, customName: null);
    }

    private void BeginRoute(GpxRoute route, string? customName, RideValues? runValues = null)
    {
        StopRecording();
        _training.BeginRoute(route, customName, runValues);
        ShowRunName();
        _profile.SetRoute(route, _settings.ReplayShowOriginalPulse);
        _profile.Visible = true;
        _pulseGraph.Visible = false;
        UpdateRecordButton();
        SetGuidedMenus();
        _powerHint.Text = GearHint;
        _time.Text = "0:00:00";
        ShowGear();
    }

    private void EndGuided(bool completed)
    {
        // While the close dialog is open the ride keeps running; reaching the end then would stack a second dialog.
        var mode = _training.Mode;
        if (!mode.Guided || _closeDialogOpen)
            return;
        var recorded = _training.Recorder;
        // Ended by the rider: one dialog before anything ends – OK, Verwerfen, or Weiterfahren (keeps riding).
        if (!completed && recorded is not null)
        {
            using var ask = new SummaryForm(L.T("End_Ask"), mode.Name, mode.Facts(recorded), recorded.FilePath, canResume: true);
            var answer = ShowEndDialog(ask);
            if (answer == DialogResult.Cancel)
                return;
            FinishGuided();
            RenameRecordingForm.Apply(this, recorded.FilePath, answer, ask.NewName);
            return;
        }
        FinishGuided();
        if (recorded is null)
            return;
        var (title, heading) = completed ? mode.CompletedTitles : (L.T("End_Ended"), mode.Name);
        using var summary = new SummaryForm(title, heading, mode.Facts(recorded), recorded.FilePath);
        var result = ShowEndDialog(summary);
        RenameRecordingForm.Apply(this, recorded.FilePath, result == DialogResult.Abort ? DialogResult.Abort : DialogResult.OK, summary.NewName);
    }

    /// <summary>The end-of-training dialog; meanwhile the ride goes on but cannot end a second time (no stacked dialogs).</summary>
    private DialogResult ShowEndDialog(Form dialog)
    {
        _closeDialogOpen = true;
        try { return dialog.ShowDialog(this); }
        finally { _closeDialogOpen = false; }
    }

    /// <summary>Leaves route, replay or program: recording stopped, free training with the normal ride view back.</summary>
    private void FinishGuided()
    {
        StopRecording();
        _training.EndMode();
        ShowRunName();
        _profile.Visible = false;
        _workoutProfile.Visible = false;
        _pulseGraph.Visible = true;
        UpdateRecordButton();
        SetGuidedMenus();
        _powerCaption.Text = L.T("Ride_Power");
        _powerHint.Text = PowerHint;
        ShowTarget(_controller.TargetWatt);
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_settings, _controller);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        var monitorChanged = dialog.RideMonitor != _settings.RideMonitor || dialog.RideViewSize != _settings.RideViewSize;
        _settings.RideViewSize = dialog.RideViewSize;
        var languageChanged = dialog.Language != _settings.Language;
        _settings.Language = dialog.Language;
        var before = _settings.Snapshot();
        _settings.Device = dialog.Device;
        _settings.PortName = dialog.PortName;
        var deviceChanged = before.Device != _settings.Device || dialog.DeviceType.ConnectionChanged(before, _settings);
        _settings.RecordingDirectory = dialog.RecordingDirectory;
        _settings.RideMonitor = dialog.RideMonitor;
        _settings.Ride = dialog.Ride;
        _settings.GarminLoginUrl = dialog.GarminLoginUrl;
        _settings.GarminImportUrl = dialog.GarminImportUrl;
        _settings.StravaLoginUrl = dialog.StravaLoginUrl;
        _settings.StravaUploadUrl = dialog.StravaUploadUrl;
        // Switching a platform on ticks its checkbox in the export dialog (it can be unticked there per export).
        if (dialog.GarminEnabled && !_settings.GarminEnabled) _settings.ExportOpenGarmin = true;
        if (dialog.StravaEnabled && !_settings.StravaEnabled) _settings.ExportOpenStrava = true;
        _settings.GarminEnabled = dialog.GarminEnabled;
        _settings.StravaEnabled = dialog.StravaEnabled;
        // The new limit applies at once – unless the running route or replay was started with its own values.
        _controller.SetUserLimit(_training.Ride.MaxWattLimit);
        if (_training.Mode is not RouteTraining)
            ShowTarget(_controller.TargetWatt);
        try
        {
            _settings.Save();
        }
        catch (Exception ex)
        {
            DarkMessageBox.Show(this, L.F("Settings_SaveFailed", ex.Message), L.T("Settings_Title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        // A new device (or port) takes over at once; the target power carries over.
        if (deviceChanged)
            _controller.Use(ErgometerTypes.Create(_settings.Device, _settings));
        if (monitorChanged)
            FitTo(RiderScreen());
        if (languageChanged)
            OfferRestart();
    }

    /// <summary>
    /// The language applies from the next start (windows are built once). Restart right away only when nothing runs –
    /// never in the middle of a training or recording.
    /// </summary>
    private void OfferRestart()
    {
        if (Guided || _training.Recorder is not null)
        {
            DarkMessageBox.Show(this, L.T("Settings_RestartLater"), L.T("Settings_Title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (DarkMessageBox.Show(this, L.T("Settings_RestartNow"), L.T("Settings_Title"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            Application.Restart();
    }

    private Screen RiderScreen() => Monitors.RideScreen(_settings.RideMonitor);

    private bool OnRiderScreen => Screen.FromControl(this).DeviceName == RiderScreen().DeviceName;

    /// <summary>Key 0 and the button: from the ride monitor to the main monitor and back (nothing to switch with a single monitor).</summary>
    private void ToggleScreen()
    {
        FitTo(OnRiderScreen ? Screen.PrimaryScreen! : RiderScreen());
        UpdateScreenButton();
    }

    /// <summary>The button says where it takes the window.</summary>
    private void UpdateScreenButton()
    {
        var single = RiderScreen().DeviceName == Screen.PrimaryScreen!.DeviceName;
        _btnScreen.Text = OnRiderScreen && !single ? L.T("Ride_ToMainScreen") : L.T("Ride_ToRideView");
    }

    private readonly bool _demoStart;

    /// <summary>Demo: started with -demo or the demo device chosen (before the device is created at Shown, from the settings).</summary>
    private bool IsDemo => _controller.Device is { } device ? device is DemoErgometer : _demoStart || _settings.Device == ErgometerTypes.Demo;

    /// <summary>Centres the window on <paramref name="screen"/> in the size chosen in the settings ("Größe der Fahransicht").</summary>
    private void FitTo(Screen screen)
    {
        var area = screen.Bounds;
        // Smallest monitor (default): the same calculation the settings list shows; the demo limit only on the rider monitor.
        var autoMax = Monitors.AutoRideViewSize(RiderScreen(), demo: IsDemo && screen.DeviceName == RiderScreen().DeviceName);
        var size = Monitors.RideViewMax(_settings.RideViewSize, area.Size, autoMax, physical => Monitors.FromPhysical(screen, physical));
        // The limit follows the setting (dragging a window edge isn't possible anyway, it has none).
        MaximumSize = size;
        Bounds = new Rectangle(area.X + (area.Width - size.Width) / 2, area.Y + (area.Height - size.Height) / 2, size.Width, size.Height);
    }

    private void PostToUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        try { BeginInvoke(action); }
        catch (InvalidOperationException) { /* window is closing */ }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    // Borderless window has no title bar, so every non-button surface acts as one: drag to move, double-click to fit the current screen.
    private void EnableMouseDrag(Control control)
    {
        if (control is Button)
            return;
        control.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || e.Clicks > 1)
                return;
            const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 2;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
        };
        control.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                FitTo(Screen.FromControl(this));
        };
        foreach (Control child in control.Controls)
            EnableMouseDrag(child);
    }

    private static Button CreateButton(string text, float size)
    {
        var button = new Button
        {
            Text = text,
            Font = new Font("Segoe UI", size, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = ButtonBack,
            FlatStyle = FlatStyle.Flat,
            TabStop = false,
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
        return button;
    }

    /// <summary>Button whose symbol is drawn as lines around its exact centre (font glyphs like ☰/✕ sit off-centre).</summary>
    private static Button CreateIconButton(string accessibleName, Action<Graphics, Pen, Point> drawIcon)
    {
        var button = CreateButton(string.Empty, 16);
        button.AccessibleName = accessibleName;
        button.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(button.ForeColor, 2.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            var c = new Point(button.ClientSize.Width / 2, button.ClientSize.Height / 2);
            drawIcon(e.Graphics, pen, c);
        };
        return button;
    }

    /// <summary>Transparent-looking button: background colour fill, only the thin border visible, slight highlight on hover/press.</summary>
    private static void ApplyOutlineStyle(Button button)
    {
        button.BackColor = Background;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(32, 32, 32);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(55, 55, 55);
    }

    private static Label CreateCaption(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Top,
        Height = 36,
        ForeColor = Caption,
        Font = new Font("Segoe UI", 18, FontStyle.Bold),
    };

    private static Label CreateTotal() => new()
    {
        Dock = DockStyle.Fill,
        ForeColor = Caption,
        Font = new Font("Segoe UI", 18),
        TextAlign = ContentAlignment.MiddleRight,
        AutoEllipsis = true,
        Margin = new Padding(0, 0, 8, 0),
    };

    private static Label CreateValueCaption(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Bottom,
        Height = 36,
        ForeColor = Caption,
        Font = new Font("Segoe UI", 18, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleCenter,
    };

    private static Label CreateValue(float size, Color color) => new()
    {
        Text = "--",
        ForeColor = color,
        Font = new Font("Segoe UI", size, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleCenter,
        AutoSize = false,
    };

    private static Label CreateSecondaryValue(Color color, string widestValue)
    {
        var size = TextRenderer.MeasureText(widestValue, SecondaryFont);
        return new Label
        {
            ForeColor = color,
            Font = SecondaryFont,
            AutoSize = false,
            Size = size,
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Left,
            Margin = Padding.Empty,
        };
    }

    private static Label CreateSecondaryText(string text, Color color) => new()
    {
        Text = text,
        ForeColor = color,
        Font = SecondaryFont,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(6, 0, 6, 0),
    };

    /// <summary>Caption/value/unit kept together and centred in its cell; its width is constant thanks to the fixed value field.</summary>
    private static Control SecondaryGroup(params Control[] parts)
    {
        var group = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.None, Margin = Padding.Empty };
        group.Controls.AddRange(parts);
        return group;
    }
}
