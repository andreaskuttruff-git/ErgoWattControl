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
    private const string PowerHint = "[↑↓ + − / Mausrad ±5  Bild ±25]";
    private const string GearHint = "[↑↓ + − / Mausrad Gang ±1  Bild ±5]";

    private readonly AppSettings _settings;
    private readonly ErgoController _controller = new();
    private CsvRecorder? _recorder;
    private RouteSession? _route;
    private readonly RouteProfile _profile = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Label _powerCaption = CreateValueCaption("LEISTUNG");
    private ToolStripMenuItem _menuRouteStart = null!;
    private ToolStripMenuItem _menuRouteEnd = null!;

    private readonly Label _powerHint = CreateCaption(PowerHint);
    private readonly Label _power = CreateValue(88, PowerColor);
    private readonly Label _pulse = CreateValue(88, PulseColor);
    private static readonly Font SecondaryFont = new("Segoe UI", 30, FontStyle.Bold);
    // Fixed-width, right-aligned value fields (sized for the widest expected value) so captions and units never shift.
    private readonly Label _rpm = CreateSecondaryValue(RpmColor, "188");
    private readonly Label _speed = CreateSecondaryValue(Color.White, "88");
    private readonly Label _time = CreateSecondaryValue(Color.White, "8:88:88");
    private readonly Label _timeCaption = CreateSecondaryText("ZEIT", Color.White);
    private readonly TrendGraph _powerGraph = new([new("Leistung (W)", PowerColor), new("RPM", RpmColor)], bottom: 0, minTop: 200, gridStep: 50, bandColor: RpmColor) { Dock = DockStyle.Fill, Margin = new Padding(10, 4, 10, 4) };
    private readonly Label _rpmCaption = CreateSecondaryText("RPM", RpmColor);
    // Fixed width even when empty, so the RPM group does not jump when the arrow appears.
    private readonly Label _rpmHint = CreateSecondaryValue(Color.OrangeRed, "▲");
    private readonly TrendGraph _pulseGraph = new([new("Puls (bpm)", PulseColor)], bottom: 60, minTop: 160, gridStep: 20) { Dock = DockStyle.Fill, Margin = new Padding(10, 4, 10, 4) };
    // Right-aligned in equal cells: the unit stays put and extra digits grow leftwards into reserved space.
    private readonly Label _km = CreateTotal();
    private readonly Label _kj = CreateTotal();
    private readonly Label _revs = CreateTotal();
    private readonly Button _btnRecord = CreateButton("● Aufnahme", 16);
    private readonly ToolTip _toolTip = new() { InitialDelay = 300, AutoPopDelay = 15000 };
    private readonly Label _status = new() { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 18), TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true };

    private readonly DemoRider? _demo;

    /// <param name="demo">Simulate the cockpit with a virtual rider instead of using the serial port (UI testing without the bike).</param>
    /// <param name="demoRoute">In demo mode: GPX file to start riding immediately.</param>
    public RideForm(AppSettings settings, bool demo = false, string? demoRoute = null)
    {
        _settings = settings;
        Text = "Ergo Watt Control";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        BackColor = Background;
        MaximumSize = Monitors.MaxRideViewSize(RiderScreen());
        FitTo(RiderScreen());

        BuildLayout();
        EnableMouseDrag(this);
        WattWheel.Attach(this, StepWatt);

        _btnRecord.Click += (_, _) =>
        {
            if (_route is not null) EndRoute(completed: false);
            else if (_recorder is null) StartRecording();
            else StopRecording();
        };
        _controller.DataReceived += d => PostToUi(() => ShowData(d));
        _controller.StatusChanged += (text, ok, detail) => PostToUi(() => ShowStatus(text, ok, detail));
        if (demo)
        {
            _demo = new DemoRider();
            var timer = new System.Windows.Forms.Timer { Interval = 1300 };
            timer.Tick += (_, _) => ShowData(_demo.Next(_controller.TargetWatt));
            Shown += (_, _) =>
            {
                ShowStatus("Demo-Modus (kein Cockpit)", true);
                if (demoRoute is not null)
                    StartRoute(demoRoute, confirm: false);
                timer.Start();
            };
        }
        else
        {
            Shown += (_, _) => _controller.Start(_settings.PortName);
        }
        FormClosing += (_, _) =>
        {
            StopRecording();
            _controller.Dispose();
        };

        ShowTarget(_controller.TargetWatt);
        ShowStatus("Verbinde …", false);
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
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 9));

        grid.Controls.Add(CreatePowerCell(), 0, 0);
        var closeButton = CreateIconButton("Beenden", (g, pen, c) =>
        {
            const int r = 8;
            g.DrawLine(pen, c.X - r, c.Y - r, c.X + r, c.Y + r);
            g.DrawLine(pen, c.X - r, c.Y + r, c.X + r, c.Y - r);
        });
        closeButton.Dock = DockStyle.Right;
        closeButton.Width = 48;
        closeButton.Click += (_, _) => Close();
        var pulseHeader = new Panel { Dock = DockStyle.Top, Height = 36 };
        pulseHeader.Controls.Add(closeButton);
        _pulse.Dock = DockStyle.Fill;
        var pulseCell = new Panel { Dock = DockStyle.Fill, Margin = new Padding(10, 0, 10, 0) };
        pulseCell.Controls.Add(_pulse);
        pulseCell.Controls.Add(CreateValueCaption("PULS"));
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
        var lowerGraph = new Panel { Dock = DockStyle.Fill, Margin = new Padding(10, 4, 10, 4) };
        _pulseGraph.Margin = Padding.Empty;
        lowerGraph.Controls.Add(_pulseGraph);
        lowerGraph.Controls.Add(_profile);
        grid.Controls.Add(lowerGraph, 0, 3);
        grid.SetColumnSpan(lowerGraph, 2);

        var btnRideView = CreateButton("Fahransicht", 16);
        btnRideView.Click += (_, _) => FitTo(RiderScreen());
        foreach (var b in new[] { _btnRecord, btnRideView })
        {
            b.AutoSize = true;
            b.Margin = new Padding(6, 3, 6, 0);
            ApplyOutlineStyle(b);
        }
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.None, Margin = Padding.Empty };
        buttons.Controls.AddRange([_btnRecord, btnRideView]);

        // Equal side columns keep the button panel centred however long the texts left and right get.
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var totals = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 0, 30, 0) };
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
        var menuButton = CreateIconButton("Menü", (g, pen, c) =>
        {
            const int halfWidth = 10, gap = 7;
            for (var dy = -gap; dy <= gap; dy += gap)
                g.DrawLine(pen, c.X - halfWidth, c.Y + dy, c.X + halfWidth, c.Y + dy);
        });
        menuButton.Dock = DockStyle.Left;
        menuButton.Width = 48;
        var menu = new ContextMenuStrip { Font = new Font("Segoe UI", 14) };
        _menuRouteStart = new ToolStripMenuItem("Strecke fahren …", null, (_, _) => StartRoute());
        _menuRouteEnd = new ToolStripMenuItem("Strecke beenden", null, (_, _) => EndRoute(completed: false)) { Enabled = false };
        menu.Items.Add(_menuRouteStart);
        menu.Items.Add(_menuRouteEnd);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Einstellungen …", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => Close());
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
            case Keys.Enter when _route is null:
                if (_recorder is null) StartRecording(); else StopRecording();
                return true;
            case Keys.NumPad0:
            case Keys.Insert:
                FitTo(RiderScreen());
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
        if (_route is { } route)
        {
            route.Shift(step / 5);
            ShowGear();
            return;
        }
        ShowTarget(_controller.SetTargetWatt(_controller.TargetWatt + step));
    }

    private void ShowTarget(int watt) => _power.Text = $"{watt} W";

    private void ShowGear()
    {
        if (_route is not { } route)
            return;
        _power.Text = $"G {route.Gear}";
        _powerCaption.Text = $"GANG   {route.TargetWatt} W";
    }

    private void ShowData(RunData d)
    {
        if (_route is { } route)
        {
            if (route.Update(d, DateTime.Now) is { } watt)
                _controller.SetTargetWatt(watt);
            _powerHint.Text = GearHint;
            ShowGear();
            _profile.SetPosition(route.Position, route.GradePercent);
            // The recording starts with the first pedal stroke so it contains no time spent getting on the bike.
            if (_recorder is null && d.Rpm > 0 && !route.Finished)
                StartRecording(route.Route.Name);
        }
        else
        {
            // The big number is the target the rider controls; show the cockpit's actual value only when it deviates.
            _powerHint.Text = Math.Abs(d.Watt - _controller.TargetWatt) > 5 ? $"[Ist {d.Watt} W]" : PowerHint;
        }
        _pulse.Text = d.Pulse > 0 ? d.Pulse.ToString() : "--";
        _rpm.Text = d.Rpm.ToString();
        // Outside this window the brake cannot hold the power; only flag it while actually pedalling.
        var rpmRange = RpmRange.For(d.Watt);
        var tooSlow = d.Rpm > 0 && d.Rpm < rpmRange.Min;
        var tooFast = d.Rpm > rpmRange.Max;
        _rpm.ForeColor = _rpmCaption.ForeColor = tooSlow || tooFast ? Color.OrangeRed : RpmColor;
        _rpmHint.Text = tooSlow ? "▲" : tooFast ? "▼" : "";
        _speed.Text = _route is { } r ? $"{r.SpeedKmh:0}" : d.SpeedKmh.ToString();
        // While recording, show the recording's own duration (free training: keeps running through pauses;
        // route: pedalling time only); otherwise the cockpit's pedaling time.
        var time = _recorder?.Elapsed ?? (_route is null ? TimeSpan.FromSeconds(d.TimeSeconds) : TimeSpan.Zero);
        _time.Text = $"{time:h\\:mm\\:ss}";
        _powerGraph.Add([d.Watt, d.Rpm], rpmRange);
        _pulseGraph.Add(d.Pulse);

        // Distance/energy only count within a recording; the cockpit's own totals since power-on are not shown.
        if (_recorder is null)
            return;
        try
        {
            if (_route is { } ride)
                _recorder.Write(d, _controller.TargetWatt, counting: d.Rpm > 0,
                    new RouteRow(ride.Position / 1000, ride.GradePercent, ride.Gear, ride.SpeedKmh));
            else
                _recorder.Write(d, _controller.TargetWatt);
            ShowTotals(_recorder.DistanceKm, _recorder.EnergyKJ, _recorder.Revolutions, Color.OrangeRed);
            if (_route is { Finished: true })
                EndRoute(completed: true);
        }
        catch (IOException ex)
        {
            StopRecording();
            MessageBox.Show(this, $"Aufzeichnung abgebrochen:\n{ex.Message}", "Aufzeichnung", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowTotals(double km, double kj, double revolutions, Color color)
    {
        _km.Text = $"{km:0.0} km";
        _kj.Text = $"{kj:0} kJ";
        _revs.Text = $"{revolutions:0} Umdr.";
        _km.ForeColor = _kj.ForeColor = _revs.ForeColor = color;
    }

    private void ShowStatus(string text, bool ok, string? detail = null)
    {
        _status.Text = text;
        _status.ForeColor = ok ? Color.LimeGreen : Color.OrangeRed;
        // The label may be cut off with "…" and keeps errors short, so the tooltip carries the full text.
        _toolTip.SetToolTip(_status, detail is null ? text : $"{text}\n{detail}");
    }

    /// <param name="routeName">Name of the route being ridden; null for free training.</param>
    private void StartRecording(string? routeName = null)
    {
        if (!_controller.IsConnected && _demo is null)
        {
            MessageBox.Show(this, "Das Cockpit ist noch nicht verbunden.", "Aufzeichnung", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            var pattern = routeName is null ? _settings.FileNameFreeTraining : _settings.FileNameRoute;
            var fileName = AppSettings.BuildFileName(pattern, DateTime.Now, routeName);
            _recorder = new CsvRecorder(_settings.RecordingDirectory, fileName, routeColumns: routeName is not null);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Datei konnte nicht angelegt werden:\n{ex.Message}", "Aufzeichnung", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        UpdateRecordButton();
        _time.Text = "0:00:00";
        _time.ForeColor = _timeCaption.ForeColor = Color.OrangeRed;
        ShowTotals(0, 0, 0, Color.OrangeRed);
    }

    private void StopRecording()
    {
        if (_recorder is null)
            return;
        _recorder.Dispose();
        _recorder = null;
        UpdateRecordButton();
        _time.ForeColor = _timeCaption.ForeColor = Color.White;
        ShowTotals(0, 0, 0, Caption);
    }

    /// <summary>Free training: start/stop recording. Route: recording runs automatically, the button ends the route.</summary>
    private void UpdateRecordButton()
    {
        _btnRecord.Text = _route is not null ? "■ Strecke beenden" : _recorder is null ? "● Aufnahme" : "■ Stop";
        _btnRecord.ForeColor = _recorder is null ? Color.White : Color.OrangeRed;
    }

    private void StartRoute()
    {
        if (_route is not null)
            return;
        Directory.CreateDirectory(_settings.GpxDirectory);
        using var picker = new OpenFileDialog
        {
            Title = "Strecke auswählen",
            InitialDirectory = _settings.GpxDirectory,
            Filter = "GPX-Strecken (*.gpx)|*.gpx",
        };
        if (picker.ShowDialog(this) == DialogResult.OK)
            StartRoute(picker.FileName, confirm: true);
    }

    private void StartRoute(string gpxPath, bool confirm)
    {
        GpxRoute route;
        try
        {
            route = GpxRoute.Load(gpxPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Die Strecke konnte nicht geladen werden:\n{ex.Message}", "Strecke fahren", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (confirm)
        {
            using var overview = new RouteOverviewForm(route, _settings);
            if (overview.ShowDialog(this) != DialogResult.OK)
                return;
        }

        StopRecording();
        _route = new RouteSession(route, _settings, ErgoController.MinWatt, ErgoController.MaxWatt);
        _controller.SetTargetWatt(_route.TargetWatt);
        _profile.SetRoute(route);
        _profile.Visible = true;
        _pulseGraph.Visible = false;
        UpdateRecordButton();
        _menuRouteStart.Enabled = false;
        _menuRouteEnd.Enabled = true;
        _powerHint.Text = GearHint;
        _time.Text = "0:00:00";
        ShowGear();
    }

    private void EndRoute(bool completed)
    {
        if (_route is not { } route)
            return;
        var recorded = _recorder;
        StopRecording();
        _route = null;
        _profile.Visible = false;
        _pulseGraph.Visible = true;
        UpdateRecordButton();
        _menuRouteStart.Enabled = true;
        _menuRouteEnd.Enabled = false;
        _powerCaption.Text = "LEISTUNG";
        _powerHint.Text = PowerHint;
        ShowTarget(_controller.TargetWatt);

        if (recorded is null)
            return;
        var title = completed ? "Ziel erreicht" : "Strecke beendet";
        MessageBox.Show(this,
            $"{route.Route.Name}\n\n" +
            $"Strecke:\t{route.Position / 1000:0.0} von {route.Route.TotalDistance / 1000:0.0} km\n" +
            $"Fahrzeit:\t{route.MovingTime:h\\:mm\\:ss}\n" +
            $"Ø Leistung:\t{route.AverageWatt:0} W\n" +
            (route.AveragePulse > 0 ? $"Ø Puls:\t{route.AveragePulse:0}\n" : "") +
            $"Energie:\t{recorded.EnergyKJ:0} kJ\n\n" +
            $"Gespeichert: {Path.GetFileName(recorded.FilePath)}",
            title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_settings, _controller);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        var monitorChanged = dialog.RideMonitor != _settings.RideMonitor;
        _settings.PortName = dialog.PortName;
        _settings.RecordingDirectory = dialog.RecordingDirectory;
        _settings.RideMonitor = dialog.RideMonitor;
        _settings.RiderWeightKg = dialog.RiderWeightKg;
        _settings.BikeWeightKg = dialog.BikeWeightKg;
        _settings.GradeFactorPercent = dialog.GradeFactorPercent;
        _settings.StartGear = dialog.StartGear;
        _settings.Gearing = dialog.Gearing;
        try
        {
            _settings.Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Einstellungen konnten nicht gespeichert werden:\n{ex.Message}", "Einstellungen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        _controller.ChangePort(_settings.PortName);
        if (monitorChanged)
        {
            MaximumSize = Monitors.MaxRideViewSize(RiderScreen());
            FitTo(RiderScreen());
        }
    }

    private Screen RiderScreen() => Monitors.RideScreen(_settings.RideMonitor);

    private void FitTo(Screen screen)
    {
        var area = screen.Bounds;
        var size = new Size(Math.Min(area.Width, MaximumSize.Width), Math.Min(area.Height, MaximumSize.Height));
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
