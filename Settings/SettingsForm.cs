using System.IO.Ports;

namespace ErgoWattControl;

public sealed class SettingsForm : Form
{
    private readonly ErgoController _controller;
    /// <summary>The saved settings (addresses for "Anmelden" and the sign-in check).</summary>
    private readonly AppSettings _appSettings;
    private readonly ComboBox _cmbDevice = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly ComboBox _cmbPorts = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly Label _lblTest = new() { AutoSize = true, MaximumSize = new Size(420, 0), Anchor = AnchorStyles.Left, Margin = new Padding(3) };
    private readonly TextBox _txtFolder = new() { Width = 330 };
    private readonly ComboBox _cmbMonitor = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };

    /// <summary>Id of the chosen kind of ergometer (<see cref="ErgometerTypes"/>).</summary>
    public string Device => ((DeviceItem)_cmbDevice.SelectedItem!).Id;
    public string? PortName => _cmbPorts.SelectedItem as string;
    public string RecordingDirectory => _txtFolder.Text.Trim();
    public string? RideMonitor => (_cmbMonitor.SelectedItem as MonitorItem)?.DeviceName;

    private readonly NumericUpDown _numRider = new() { Minimum = 30, Maximum = 250, Width = 80 };
    private readonly NumericUpDown _numBike = new() { Minimum = 5, Maximum = 40, Width = 80 };
    private readonly NumericUpDown _numGradeFactor = new() { Minimum = 10, Maximum = 100, Increment = 10, Width = 80 };
    private readonly NumericUpDown _numStartGear = new() { Minimum = RouteSession.MinGear, Maximum = RouteSession.MaxGear, Width = 80 };

    public double RiderWeightKg => (double)_numRider.Value;
    public double BikeWeightKg => (double)_numBike.Value;
    public int GradeFactorPercent => (int)_numGradeFactor.Value;
    public int StartGear => (int)_numStartGear.Value;

    private readonly NumericUpDown _numMaxWatt = new() { Minimum = ErgoController.MinWatt, Maximum = ErgoController.AbsoluteMaxWatt, Increment = 25, Width = 80 };
    public int MaxWattLimit => (int)_numMaxWatt.Value / 5 * 5;

    private readonly ComboBox _cmbGearing = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    public string Gearing => (string)_cmbGearing.SelectedItem!;

    private readonly TextBox _txtGarminLogin = new() { Width = 560 };
    private readonly TextBox _txtGarminImport = new() { Width = 560 };
    private readonly TextBox _txtStravaLogin = new() { Width = 560 };
    private readonly TextBox _txtStravaUpload = new() { Width = 560 };
    /// <summary>Empty field = the platform's default address again.</summary>
    public string GarminLoginUrl => OrDefault(_txtGarminLogin, UploadService.GarminDefaultLoginUrl);
    public string GarminImportUrl => OrDefault(_txtGarminImport, UploadService.GarminDefaultImportUrl);
    public string StravaLoginUrl => OrDefault(_txtStravaLogin, UploadService.StravaDefaultLoginUrl);
    public string StravaUploadUrl => OrDefault(_txtStravaUpload, UploadService.StravaDefaultUploadUrl);

    private static string OrDefault(TextBox box, string fallback) => string.IsNullOrWhiteSpace(box.Text) ? fallback : box.Text.Trim();

    /// <summary>Switched-on upload platforms: only these get a checkbox in the export dialog.</summary>
    private readonly CheckBox _chkGarmin = new() { Text = "Garmin Connect", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 3, 12, 3) };
    private readonly CheckBox _chkStrava = new() { Text = "Strava", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 3, 12, 3) };
    public bool GarminEnabled => _chkGarmin.Checked;
    public bool StravaEnabled => _chkStrava.Checked;

    /// <summary>
    /// Three rows per upload platform: the switch (first column) with the sign-in address, the upload address,
    /// Standard/Abmelden with the sign-in state. The addresses are greyed out while the platform is off.
    /// </summary>
    private void AddUploadRows(TableLayoutPanel grid, int row, UploadService service, CheckBox enabled, bool isEnabled, TextBox login, TextBox upload,
        string uploadLabel, string loginUrl, string uploadUrl, string defaultLogin, string defaultUpload, string uploadPage)
    {
        var btnDefault = new Button { Text = "Standard", AutoSize = true };
        var btnLogout = new Button { Text = "Abmelden", AutoSize = true };
        var btnLogin = new Button { Text = "Anmelden", AutoSize = true };
        var status = Unit("");
        enabled.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        enabled.Margin = new Padding(3, 8, 12, 3);
        grid.Controls.Add(enabled, 0, row);
        enabled.Checked = isEnabled;
        // Labels and fields in a table of their own so the fields line up at any display scaling.
        var block = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 4, 0, 4), Anchor = AnchorStyles.Left };
        block.Controls.Add(Unit("Anmeldung"), 0, 0);
        block.Controls.Add(login, 1, 0);
        block.Controls.Add(Unit(uploadLabel), 0, 1);
        block.Controls.Add(upload, 1, 1);
        var buttons = Row(btnDefault, btnLogout, btnLogin, status);
        block.Controls.Add(buttons, 0, 2);
        block.SetColumnSpan(buttons, 2);
        login.Anchor = upload.Anchor = AnchorStyles.Left;
        grid.Controls.Add(block, 1, row);
        login.Text = loginUrl;
        upload.Text = uploadUrl;

        // "Anmelden" only makes sense without a (valid) saved sign-in.
        void ShowState(string text)
        {
            status.Text = text;
            btnLogin.Enabled = enabled.Checked && !service.HasSession;
        }
        void Enable()
        {
            login.Enabled = upload.Enabled = btnDefault.Enabled = enabled.Checked;
            btnLogin.Enabled = enabled.Checked && !service.HasSession;
        }
        enabled.CheckedChanged += (_, _) => Enable();
        Enable();
        ShowState(service.HasSession ? "Anmeldung gespeichert" : "Nicht angemeldet");
        Tip(enabled, $"Upload zu {service.Name} verwenden: Der Exportdialog bietet dann „Danach hochladen bei {service.Name}“ an.");

        // Is the saved sign-in still valid? Checked in an invisible browser once the dialog is up.
        Shown += async (_, _) =>
        {
            if (!service.HasSession)
                return;
            ShowState("Anmeldung gespeichert – wird geprüft …");
            var valid = await UploadWindow.CheckSessionAsync(service, _appSettings, Handle);
            if (IsDisposed)
                return;
            ShowState(valid switch
            {
                true => "Anmeldung gespeichert und gültig",
                false => "Anmeldung abgelaufen – bitte neu anmelden",
                null => "Anmeldung gespeichert (Prüfung nicht möglich)",
            });
        };
        btnDefault.Click += (_, _) =>
        {
            login.Text = defaultLogin;
            upload.Text = defaultUpload;
        };
        btnLogin.Click += (_, _) =>
        {
            // Modal: the settings wait until the sign-in window has closed.
            ShowState(UploadWindow.SignIn(service, _appSettings, this) ? "Anmeldung gespeichert und gültig" : "Nicht angemeldet");
        };
        btnLogout.Click += (_, _) => ShowState(UploadWindow.SignOut(service)
            ? "Abgemeldet – gespeicherte Anmeldung gelöscht"
            : $"Abmelden nicht möglich, solange das {service.Name}-Fenster offen ist");
        Tip(btnLogin, $"Öffnet die Anmeldeseite von {service.Name} im Browserfenster.\nNur ohne gespeicherte (gültige) Anmeldung.");
        Tip(login, $"Anmeldeseite von {service.Name}. Wird geöffnet, solange keine Anmeldung gespeichert ist.");
        Tip(upload, $"{uploadPage}. Wird nach der Anmeldung bzw. bei gespeicherter Anmeldung direkt geöffnet.");
        Tip(btnDefault, $"Beide Adressen auf den Standard von {service.Name} zurücksetzen.");
        Tip(btnLogout, $"Löscht die im {service.Name}-Fenster gespeicherte Anmeldung (Cookies) dieses Rechners.\nBeim nächsten Mal musst du dich neu anmelden.");
    }

    private sealed record MonitorItem(string? DeviceName, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed record DeviceItem(string Id, string Text)
    {
        public override string ToString() => Text;
    }

    public SettingsForm(AppSettings settings, ErgoController controller)
    {
        _controller = controller;
        _appSettings = settings;
        Text = "Einstellungen";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 10);
        HelpWindow.Attach(this, "einstellungen");
        Padding = new Padding(12);

        var btnRefresh = new Button { Text = "Aktualisieren", AutoSize = true };
        var btnTest = new Button { Text = "Verbindung testen", AutoSize = true };
        var btnBrowse = new Button { Text = "Durchsuchen…", AutoSize = true };
        var btnOpen = new Button { Text = "Öffnen", AutoSize = true };
        var btnIdentify = new Button { Text = "Identifizieren", AutoSize = true };
        var btnOk = new Button { Text = "OK", AutoSize = true, DialogResult = DialogResult.OK, MinimumSize = new Size(90, 0) };
        var btnCancel = new Button { Text = "Abbrechen", AutoSize = true, DialogResult = DialogResult.Cancel, MinimumSize = new Size(90, 0) };
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        // Anchoring only Left (not Top) centres each control vertically in its row, so labels, fields and buttons line up.
        grid.Controls.Add(RowLabel("Gerät:"), 0, 0);
        grid.Controls.Add(Row(_cmbDevice), 1, 0);
        // Connection fields of the Daum cockpit; other kinds of device bring their own (none for the demo).
        var portLabel = RowLabel("COM-Port:");
        var portRow = Row(_cmbPorts, btnRefresh, btnTest);
        grid.Controls.Add(portLabel, 0, 1);
        grid.Controls.Add(portRow, 1, 1);
        grid.Controls.Add(_lblTest, 1, 2);
        grid.Controls.Add(RowLabel("Ablageordner:"), 0, 3);
        grid.Controls.Add(Row(_txtFolder, btnBrowse, btnOpen), 1, 3);
        grid.Controls.Add(RowLabel("Fahransicht auf:"), 0, 4);
        grid.Controls.Add(Row(_cmbMonitor, btnIdentify), 1, 4);
        grid.Controls.Add(RowLabel("Strecke fahren:"), 0, 5);
        grid.Controls.Add(Row(
            Unit("Fahrer"), _numRider, Unit("kg   Rad"), _numBike, Unit("kg   Steigung"), _numGradeFactor, Unit("%")), 1, 5);
        grid.Controls.Add(Row(Unit("Übersetzung"), _cmbGearing, Unit("   Startgang"), _numStartGear), 1, 6);
        grid.Controls.Add(RowLabel("Wattgrenze:"), 0, 7);
        grid.Controls.Add(Row(_numMaxWatt, Unit($"W   ({controller.Capabilities.Info})")), 1, 7);
        AddUploadRows(grid, 8, UploadService.Garmin, _chkGarmin, settings.GarminEnabled, _txtGarminLogin, _txtGarminImport, "Import",
            settings.GarminLoginUrl, settings.GarminImportUrl, UploadService.GarminDefaultLoginUrl, UploadService.GarminDefaultImportUrl,
            "Seite \"Daten importieren\" von Garmin Connect");
        AddUploadRows(grid, 9, UploadService.Strava, _chkStrava, settings.StravaEnabled, _txtStravaLogin, _txtStravaUpload, "Upload",
            settings.StravaLoginUrl, settings.StravaUploadUrl, UploadService.StravaDefaultLoginUrl, UploadService.StravaDefaultUploadUrl,
            "Upload-Seite von Strava (\"Datei hochladen\")");
        var buttons = Row(btnOk, btnCancel);
        buttons.Anchor = AnchorStyles.Right;
        buttons.Margin = new Padding(3, 18, 3, 3);
        grid.Controls.Add(buttons, 1, 10);

        _cmbDevice.Items.AddRange([.. ErgometerTypes.All.Select(t => new DeviceItem(t.Id, t.Name))]);
        _cmbDevice.SelectedItem = _cmbDevice.Items.Cast<DeviceItem>().FirstOrDefault(d => d.Id == settings.Device) ?? _cmbDevice.Items[0];
        void ShowDeviceFields() => portLabel.Visible = portRow.Visible = _lblTest.Visible = Device == ErgometerTypes.DaumRs232;
        _cmbDevice.SelectedIndexChanged += (_, _) => ShowDeviceFields();
        ShowDeviceFields();
        Controls.Add(grid);

        _txtFolder.Text = settings.RecordingDirectory;
        _numRider.Value = (decimal)Math.Clamp(settings.RiderWeightKg, 30, 250);
        _numBike.Value = (decimal)Math.Clamp(settings.BikeWeightKg, 5, 40);
        _numGradeFactor.Value = Math.Clamp(settings.GradeFactorPercent, 10, 100);
        _numStartGear.Value = Math.Clamp(settings.StartGear, RouteSession.MinGear, RouteSession.MaxGear);
        _numMaxWatt.Value = Math.Clamp(settings.MaxWattLimit, ErgoController.MinWatt, ErgoController.AbsoluteMaxWatt);
        _cmbGearing.Items.AddRange([RouteSession.GearingMtb, RouteSession.GearingRoad]);
        _cmbGearing.SelectedItem = settings.Gearing == RouteSession.GearingRoad ? RouteSession.GearingRoad : RouteSession.GearingMtb;
        _cmbMonitor.Items.Add(new MonitorItem(null, "Automatisch (erster Nebenmonitor)"));
        foreach (var screen in Screen.AllScreens.OrderBy(Monitors.Number))
            _cmbMonitor.Items.Add(new MonitorItem(screen.DeviceName, Monitors.Describe(screen)));
        _cmbMonitor.SelectedItem = _cmbMonitor.Items.Cast<MonitorItem>().FirstOrDefault(m => m.DeviceName == settings.RideMonitor)
            ?? _cmbMonitor.Items[0]; // saved monitor no longer connected
        btnIdentify.Click += (_, _) => Monitors.Identify(TimeSpan.FromSeconds(3));

        Tip(_cmbDevice, "Welches Ergometer die App steuert.\nDemo: ein simulierter Fahrer zum Ausprobieren ohne Ergometer.\nNach dem Speichern verbindet sich die App sofort neu.");
        Tip(_cmbPorts, "Serielle Schnittstelle, an der das Cockpit hängt (Update-Kabel bzw. USB-RS232-Adapter).\nNach dem Speichern verbindet sich die App sofort neu.");
        Tip(btnRefresh, "Liste der COM-Ports neu einlesen, z. B. nach dem Einstecken eines USB-Adapters.");
        Tip(btnTest, "Prüft, ob am gewählten Port ein Cockpit antwortet. Ändert nichts am Cockpit.");
        Tip(_txtFolder, "Ordner für die Aufzeichnungen.\nDie Dateinamen legen FileNameTraining, FileNameRoute und FileNameProgram in der settings.json fest.");
        Tip(btnBrowse, "Ablageordner auswählen.");
        Tip(btnOpen, "Ablageordner im Explorer öffnen.");
        Tip(_cmbMonitor, "Monitor, auf den der Button \"Fahransicht\" das Fenster legt.\n\"Automatisch\" nimmt den ersten Monitor, der nicht der Hauptmonitor ist.");
        Tip(btnIdentify, "Zeigt auf jedem Monitor 3 Sekunden lang groß seine Nummer.");
        Tip(_numRider, "Dein Körpergewicht. Bestimmt im Streckenmodus, wie viel Leistung ein Anstieg kostet.");
        Tip(_numBike, "Gewicht des Rads, mit dem du die Strecke virtuell fährst. Zählt wie das Fahrergewicht.");
        Tip(_numGradeFactor, "Wie steil die GPX-Strecke wirkt: 100 % = wie in echt, 50 % = alle Steigungen und Gefälle halb so steil.\nMacht bergige Strecken fahrbar, ohne an die Wattgrenze zu stoßen.");
        Tip(_cmbGearing, "Übersetzung der 28 virtuellen Gänge im Streckenmodus.\nMTB: kleinere Gänge (2,0–7,4 m pro Kurbelumdrehung), am Berg langsam kurbeln möglich.\nRennrad: Daum-Tabelle (3,7–9,3 m pro Kurbelumdrehung).");
        Tip(_numStartGear, "Gang, in dem eine Strecke beginnt (1 = leichtester, 28 = schwerster).");
        Tip(_numMaxWatt, "Höchste Leistung, die die App einstellt – auch im Streckenmodus.\nDas Gerät selbst begrenzt zusätzlich (Daum 8008/8080: 800 W, andere Cockpits: 400 W); es gilt der kleinere Wert.");
        RefreshPorts(settings.PortName);
        _lblTest.Text = controller.IsConnected ? $"Aktuell verbunden: {controller.Device?.DisplayName}" : "Aktuell nicht verbunden";

        btnRefresh.Click += (_, _) => RefreshPorts(PortName);
        var testRunning = false;
        btnTest.Click += async (_, _) =>
        {
            if (testRunning)
                return;
            if (PortName is not { } port)
            {
                _lblTest.Text = "Bitte einen Port auswählen.";
                return;
            }
            // Not disabling the button: that would hand focus to the folder box, which then selects its whole text.
            testRunning = true;
            _lblTest.Text = $"Teste {port} …";
            _lblTest.Text = await Task.Run(() => DaumErgometer.TestPort(port, _controller.Device as DaumErgometer));
            testRunning = false;
        };
        btnBrowse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { InitialDirectory = RecordingDirectory, UseDescriptionForTitle = true, Description = "Ablageordner für Aufzeichnungen" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                _txtFolder.Text = dialog.SelectedPath;
        };
        btnOpen.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(RecordingDirectory);
                System.Diagnostics.Process.Start("explorer.exe", RecordingDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ordner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        FormClosing += (_, e) =>
        {
            if (DialogResult != DialogResult.OK)
                return;
            try
            {
                Directory.CreateDirectory(RecordingDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ablageordner ungültig:\n{ex.Message}", "Einstellungen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
            }
        };
    }

    private readonly ToolTip _toolTip = new() { InitialDelay = 400, AutoPopDelay = 20000 };

    /// <summary>Also sets the tip on child controls: a NumericUpDown's inner text box would otherwise show none.</summary>
    private void Tip(Control control, string text)
    {
        _toolTip.SetToolTip(control, text);
        foreach (Control child in control.Controls)
            Tip(child, text);
    }

    private void RefreshPorts(string? select)
    {
        _cmbPorts.Items.Clear();
        _cmbPorts.Items.AddRange(SerialPort.GetPortNames());
        if (select is not null && !_cmbPorts.Items.Contains(select))
            _cmbPorts.Items.Add(select); // keep the saved port visible even if currently unplugged
        _cmbPorts.SelectedItem = select;
        if (_cmbPorts.SelectedIndex < 0 && _cmbPorts.Items.Count > 0)
            _cmbPorts.SelectedIndex = 0;
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 4), Anchor = AnchorStyles.Left };
        foreach (var control in controls)
            control.Anchor = AnchorStyles.Left;
        row.Controls.AddRange(controls);
        return row;
    }

    private static Label Unit(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 3, 3, 3) };

    private static Label RowLabel(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 3, 12, 3) };
}
