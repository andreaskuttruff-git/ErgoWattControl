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

    /// <summary>The chosen kind of ergometer (<see cref="ErgometerTypes"/>).</summary>
    public ErgometerType DeviceType => (ErgometerType)_cmbDevice.SelectedItem!;
    public string Device => DeviceType.Id;
    public string? PortName => _cmbPorts.SelectedItem as string;
    public string RecordingDirectory => _txtFolder.Text.Trim();
    public string? RideMonitor => (_cmbMonitor.SelectedItem as MonitorItem)?.DeviceName;

    /// <summary>Size of the ride view: "auto", "screen" or e.g. "1280x1024" (<see cref="Monitors"/>).</summary>
    private readonly ComboBox _cmbSize = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    public string RideViewSize => (_cmbSize.SelectedItem as SizeItem)?.Setting ?? Monitors.SizeAuto;

    /// <summary>A size choice: the setting it stands for, shown under its name.</summary>
    private sealed record SizeItem(string Setting, string Text)
    {
        public override string ToString() => Text;
    }

    /// <summary>
    /// The sizes for the chosen monitor: as before, the whole monitor, and the fixed sizes that fit it. A saved size
    /// larger than this monitor stays in the list (the window is then simply as large as the monitor).
    /// </summary>
    private void FillSizes(string selected)
    {
        var screen = Monitors.RideScreen(RideMonitor);
        var physical = Monitors.PhysicalSize(screen);
        _cmbSize.Items.Clear();
        _cmbSize.Items.Add(new SizeItem(Monitors.SizeAuto, L.T("Settings_SizeAuto")));
        _cmbSize.Items.Add(new SizeItem(Monitors.SizeScreen, L.F("Settings_SizeScreen", physical.Width, physical.Height)));
        var sizes = Monitors.FixedRideViewSizes.Where(s => s.Width <= physical.Width && s.Height <= physical.Height).ToList();
        if (Monitors.ParseSize(selected) is { } saved && !sizes.Contains(saved))
            sizes.Add(saved);
        foreach (var size in sizes.OrderBy(s => s.Width).ThenBy(s => s.Height))
            _cmbSize.Items.Add(new SizeItem(Monitors.FormatSize(size), L.F("Settings_SizeFixed", size.Width, size.Height)));
        _cmbSize.SelectedItem = _cmbSize.Items.Cast<SizeItem>().FirstOrDefault(i => i.Setting == selected) ?? _cmbSize.Items[0];
    }

    private readonly NumericUpDown _numRider = new() { Minimum = 30, Maximum = 250, Width = 80 };
    private readonly NumericUpDown _numBike = new() { Minimum = 5, Maximum = 40, Width = 80 };
    private readonly NumericUpDown _numGradeFactor = new() { Minimum = 10, Maximum = 100, Increment = 10, Width = 80 };
    private readonly NumericUpDown _numStartGear = new() { Minimum = RouteSession.MinGear, Maximum = RouteSession.MaxGear, Width = 80 };

    private readonly NumericUpDown _numMaxWatt = new() { Minimum = ErgoController.MinWatt, Maximum = ErgoController.AbsoluteMaxWatt, Increment = 25, Width = 80 };
    private readonly ComboBox _cmbGearing = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };

    /// <summary>The ride values as edited here; the resistances, only editable in settings.json, stay as they were.</summary>
    public RideValues Ride
    {
        get
        {
            var ride = _appSettings.Ride.Copy();
            ride.RiderWeightKg = (double)_numRider.Value;
            ride.BikeWeightKg = (double)_numBike.Value;
            ride.GradeFactorPercent = (int)_numGradeFactor.Value;
            ride.StartGear = (int)_numStartGear.Value;
            ride.Gearing = ((RideSettingsPanel.GearingItem)_cmbGearing.SelectedItem!).Id;
            ride.MaxWattLimit = (int)_numMaxWatt.Value / 5 * 5;
            return ride;
        }
    }

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
        var btnDefault = new Button { Text = L.T("Button_Default"), AutoSize = true };
        var btnLogout = new Button { Text = L.T("Button_SignOut"), AutoSize = true };
        var btnLogin = new Button { Text = L.T("Button_SignIn"), AutoSize = true };
        var status = Unit("");
        enabled.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        enabled.Margin = new Padding(3, 8, 12, 3);
        grid.Controls.Add(enabled, 0, row);
        enabled.Checked = isEnabled;
        // Labels and fields in a table of their own so the fields line up at any display scaling.
        var block = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 4, 0, 4), Anchor = AnchorStyles.Left };
        block.Controls.Add(Unit(L.T("Upload_SignInLabel")), 0, 0);
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
        ShowState(service.HasSession ? L.T("Upload_SessionSaved") : L.T("Upload_NotSignedIn"));
        Tip(enabled, L.F("Upload_EnableTip", service.Name));

        // Is the saved sign-in still valid? Checked in an invisible browser once the dialog is up.
        Shown += async (_, _) =>
        {
            if (!service.HasSession)
                return;
            ShowState(L.T("Upload_SessionChecking"));
            var valid = await UploadWindow.CheckSessionAsync(service, _appSettings, Handle);
            if (IsDisposed)
                return;
            ShowState(valid switch
            {
                true => L.T("Upload_SessionValid"),
                false => L.T("Upload_SessionExpired"),
                null => L.T("Upload_SessionUnknown"),
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
            ShowState(UploadWindow.SignIn(service, _appSettings, this) ? L.T("Upload_SessionValid") : L.T("Upload_NotSignedIn"));
        };
        btnLogout.Click += (_, _) => ShowState(UploadWindow.SignOut(service)
            ? L.T("Upload_SignedOut")
            : L.F("Upload_SignOutBlocked", service.Name));
        Tip(btnLogin, L.F("Upload_SignInTip", service.Name));
        Tip(login, L.F("Upload_LoginUrlTip", service.Name));
        Tip(upload, L.F("Upload_UploadUrlTip", uploadPage));
        Tip(btnDefault, L.F("Upload_DefaultTip", service.Name));
        Tip(btnLogout, L.F("Upload_SignOutTip", service.Name));
    }

    private sealed record MonitorItem(string? DeviceName, string Text)
    {
        public override string ToString() => Text;
    }

    /// <summary>A language in the list; Code "auto" = as Windows.</summary>
    private sealed record LanguageItem(string Code, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly ComboBox _cmbLanguage = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };

    /// <summary>"auto", "de" or "en" (<see cref="ErgoWattControl.Language"/>).</summary>
    public string Language => ((LanguageItem)_cmbLanguage.SelectedItem!).Code;


    public SettingsForm(AppSettings settings, ErgoController controller)
    {
        _controller = controller;
        _appSettings = settings;
        Text = L.T("Settings_Title");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 10);
        HelpWindow.Attach(this, "einstellungen");
        Padding = new Padding(12);

        var btnRefresh = new Button { Text = L.T("Button_Refresh"), AutoSize = true };
        var btnTest = new Button { Text = L.T("Button_TestConnection"), AutoSize = true };
        var btnBrowse = new Button { Text = L.T("Button_Browse"), AutoSize = true };
        var btnOpen = new Button { Text = L.T("Button_Open"), AutoSize = true };
        var btnIdentify = new Button { Text = L.T("Button_Identify"), AutoSize = true };
        var btnOk = new Button { Text = L.T("Button_Ok"), AutoSize = true, DialogResult = DialogResult.OK, MinimumSize = new Size(90, 0) };
        var btnCancel = new Button { Text = L.T("Button_Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel, MinimumSize = new Size(90, 0) };
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        // Anchoring only Left (not Top) centres each control vertically in its row, so labels, fields and buttons line up.
        // The language label is always in both languages, so it is found even in a language one doesn't read.
        grid.Controls.Add(RowLabel(L.T("Settings_Language")), 0, 0);
        grid.Controls.Add(Row(_cmbLanguage), 1, 0);
        grid.Controls.Add(RowLabel(L.T("Settings_Device")), 0, 1);
        grid.Controls.Add(Row(_cmbDevice), 1, 1);
        // Connection fields of the Daum cockpit; other kinds of device bring their own (none for the demo).
        var portLabel = RowLabel(L.T("Settings_ComPort"));
        var portRow = Row(_cmbPorts, btnRefresh, btnTest);
        grid.Controls.Add(portLabel, 0, 2);
        grid.Controls.Add(portRow, 1, 2);
        grid.Controls.Add(_lblTest, 1, 3);
        grid.Controls.Add(RowLabel(L.T("Settings_Folder")), 0, 4);
        grid.Controls.Add(Row(_txtFolder, btnBrowse, btnOpen), 1, 4);
        grid.Controls.Add(RowLabel(L.T("Settings_Monitor")), 0, 5);
        grid.Controls.Add(Row(_cmbMonitor, btnIdentify), 1, 5);
        grid.Controls.Add(RowLabel(L.T("Settings_RideViewSize")), 0, 6);
        grid.Controls.Add(Row(_cmbSize), 1, 6);
        grid.Controls.Add(RowLabel(L.T("Settings_Route")), 0, 7);
        grid.Controls.Add(Row(
            Unit(L.T("Settings_Rider")), _numRider, Unit(L.T("Settings_KgBike")), _numBike, Unit(L.T("Settings_KgGrade")), _numGradeFactor, Unit("%")), 1, 7);
        grid.Controls.Add(Row(Unit(L.T("Settings_Gearing")), _cmbGearing, Unit(L.T("Settings_StartGear")), _numStartGear), 1, 8);
        grid.Controls.Add(RowLabel(L.T("Settings_PowerLimit")), 0, 9);
        grid.Controls.Add(Row(_numMaxWatt, Unit(L.F("Settings_PowerLimitUnit", controller.Capabilities.Info))), 1, 9);
        AddUploadRows(grid, 10, UploadService.Garmin, _chkGarmin, settings.GarminEnabled, _txtGarminLogin, _txtGarminImport, L.T("Upload_ImportLabel"),
            settings.GarminLoginUrl, settings.GarminImportUrl, UploadService.GarminDefaultLoginUrl, UploadService.GarminDefaultImportUrl,
            L.T("Upload_GarminPage"));
        AddUploadRows(grid, 11, UploadService.Strava, _chkStrava, settings.StravaEnabled, _txtStravaLogin, _txtStravaUpload, L.T("Upload_UploadLabel"),
            settings.StravaLoginUrl, settings.StravaUploadUrl, UploadService.StravaDefaultLoginUrl, UploadService.StravaDefaultUploadUrl,
            L.T("Upload_StravaPage"));
        var buttons = Row(btnOk, btnCancel);
        buttons.Anchor = AnchorStyles.Right;
        buttons.Margin = new Padding(3, 18, 3, 3);
        grid.Controls.Add(buttons, 1, 12);

        // Languages: "automatic" names the one Windows gives; each language under its own name.
        _cmbLanguage.Items.Add(new LanguageItem(ErgoWattControl.Language.Auto,
            L.F("Settings_LanguageAuto", ErgoWattControl.Language.Available.First(l => l.Code == ErgoWattControl.Language.Resolve(null).TwoLetterISOLanguageName).Name)));
        foreach (var (code, name) in ErgoWattControl.Language.Available)
            _cmbLanguage.Items.Add(new LanguageItem(code, name));
        // Pseudo-localization (set by hand in settings.json) stays selected instead of falling back to "automatic".
        if (settings.Language == ErgoWattControl.Language.PseudoCode)
            _cmbLanguage.Items.Add(new LanguageItem(ErgoWattControl.Language.PseudoCode, "Pseudo (test)"));
        _cmbLanguage.SelectedItem = _cmbLanguage.Items.Cast<LanguageItem>().FirstOrDefault(l => l.Code == settings.Language) ?? _cmbLanguage.Items[0];
        Tip(_cmbLanguage, L.T("Settings_LanguageTip"));

        _cmbDevice.Items.AddRange([.. ErgometerTypes.All]);
        _cmbDevice.SelectedItem = ErgometerTypes.Find(settings.Device);
        // Only the connection fields the chosen kind of device uses.
        void ShowDeviceFields()
        {
            portLabel.Visible = portRow.Visible = _lblTest.Visible = DeviceType.UsesSerialPort;
            btnTest.Visible = DeviceType.TestConnection is not null;
        }
        _cmbDevice.SelectedIndexChanged += (_, _) => ShowDeviceFields();
        ShowDeviceFields();
        Controls.Add(grid);

        _txtFolder.Text = settings.RecordingDirectory;
        var ride = settings.Ride;
        _numRider.Value = (decimal)Math.Clamp(ride.RiderWeightKg, 30, 250);
        _numBike.Value = (decimal)Math.Clamp(ride.BikeWeightKg, 5, 40);
        _numGradeFactor.Value = Math.Clamp(ride.GradeFactorPercent, 10, 100);
        _numStartGear.Value = Math.Clamp(ride.StartGear, RouteSession.MinGear, RouteSession.MaxGear);
        _numMaxWatt.Value = Math.Clamp(ride.MaxWattLimit, ErgoController.MinWatt, ErgoController.AbsoluteMaxWatt);
        _cmbGearing.Items.AddRange([.. RouteSession.Gearings.Select(g => new RideSettingsPanel.GearingItem(g))]);
        _cmbGearing.SelectedItem = new RideSettingsPanel.GearingItem(ride.Gearing == RouteSession.GearingRoad ? RouteSession.GearingRoad : RouteSession.GearingMtb);
        _cmbMonitor.Items.Add(new MonitorItem(null, L.T("Settings_MonitorAuto")));
        foreach (var screen in Screen.AllScreens.OrderBy(Monitors.Number))
            _cmbMonitor.Items.Add(new MonitorItem(screen.DeviceName, Monitors.Describe(screen)));
        _cmbMonitor.SelectedItem = _cmbMonitor.Items.Cast<MonitorItem>().FirstOrDefault(m => m.DeviceName == settings.RideMonitor)
            ?? _cmbMonitor.Items[0]; // saved monitor no longer connected
        btnIdentify.Click += (_, _) => Monitors.Identify(TimeSpan.FromSeconds(3));
        FillSizes(settings.RideViewSize);
        // Another monitor: offer the sizes that fit it, keeping the choice where possible.
        _cmbMonitor.SelectedIndexChanged += (_, _) => FillSizes(RideViewSize);

        Tip(_cmbDevice, L.T("Settings_DeviceTip"));
        Tip(_cmbPorts, L.T("Settings_PortTip"));
        Tip(btnRefresh, L.T("Settings_RefreshTip"));
        Tip(btnTest, L.T("Settings_TestTip"));
        Tip(_txtFolder, L.T("Settings_FolderTip"));
        Tip(btnBrowse, L.T("Settings_BrowseTip"));
        Tip(btnOpen, L.T("Settings_OpenTip"));
        Tip(_cmbMonitor, L.T("Settings_MonitorTip"));
        Tip(_cmbSize, L.T("Settings_SizeTip"));
        Tip(btnIdentify, L.T("Settings_IdentifyTip"));
        Tip(_numRider, L.T("Settings_RiderTip"));
        Tip(_numBike, L.T("Settings_BikeTip"));
        Tip(_numGradeFactor, L.T("Settings_GradeTip"));
        Tip(_cmbGearing, L.T("Settings_GearingTip"));
        Tip(_numStartGear, L.T("Settings_StartGearTip"));
        Tip(_numMaxWatt, L.T("Settings_PowerLimitTip"));
        RefreshPorts(settings.PortName);
        _lblTest.Text = controller.IsConnected ? L.F("Settings_Connected", controller.Device?.DisplayName) : L.T("Settings_NotConnected");

        btnRefresh.Click += (_, _) => RefreshPorts(PortName);
        var testRunning = false;
        btnTest.Click += async (_, _) =>
        {
            if (testRunning)
                return;
            if (PortName is not { } port)
            {
                _lblTest.Text = L.T("Settings_ChoosePort");
                return;
            }
            // Not disabling the button: that would hand focus to the folder box, which then selects its whole text.
            testRunning = true;
            _lblTest.Text = L.F("Settings_Testing", port);
            if (DeviceType.TestConnection is { } test)
                _lblTest.Text = await Task.Run(() => test(port, _controller.Device));
            testRunning = false;
        };
        btnBrowse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { InitialDirectory = RecordingDirectory, UseDescriptionForTitle = true, Description = L.T("Settings_FolderDialog") };
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
                MessageBox.Show(this, ex.Message, L.T("Caption_Folder"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show(this, L.F("Settings_FolderInvalid", ex.Message), L.T("Settings_Title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
