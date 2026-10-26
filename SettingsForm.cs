using System.IO.Ports;

namespace ErgoWattControl;

public sealed class SettingsForm : Form
{
    private readonly ErgoController _controller;
    private readonly ComboBox _cmbPorts = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly Label _lblTest = new() { AutoSize = true, MaximumSize = new Size(420, 0), Anchor = AnchorStyles.Left, Margin = new Padding(3) };
    private readonly TextBox _txtFolder = new() { Width = 330 };
    private readonly ComboBox _cmbMonitor = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };

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

    private readonly ComboBox _cmbGearing = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    public string Gearing => (string)_cmbGearing.SelectedItem!;

    private sealed record MonitorItem(string? DeviceName, string Text)
    {
        public override string ToString() => Text;
    }

    public SettingsForm(AppSettings settings, ErgoController controller)
    {
        _controller = controller;
        Text = "Einstellungen";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 10);
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
        grid.Controls.Add(RowLabel("COM-Port:"), 0, 0);
        grid.Controls.Add(Row(_cmbPorts, btnRefresh, btnTest), 1, 0);
        grid.Controls.Add(_lblTest, 1, 1);
        grid.Controls.Add(RowLabel("Ablageordner:"), 0, 2);
        grid.Controls.Add(Row(_txtFolder, btnBrowse, btnOpen), 1, 2);
        grid.Controls.Add(RowLabel("Fahransicht auf:"), 0, 3);
        grid.Controls.Add(Row(_cmbMonitor, btnIdentify), 1, 3);
        grid.Controls.Add(RowLabel("Strecke fahren:"), 0, 4);
        grid.Controls.Add(Row(
            Unit("Fahrer"), _numRider, Unit("kg   Rad"), _numBike, Unit("kg   Steigung"), _numGradeFactor, Unit("%")), 1, 4);
        grid.Controls.Add(Row(Unit("Übersetzung"), _cmbGearing, Unit("   Startgang"), _numStartGear), 1, 5);
        var buttons = Row(btnOk, btnCancel);
        buttons.Anchor = AnchorStyles.Right;
        buttons.Margin = new Padding(3, 18, 3, 3);
        grid.Controls.Add(buttons, 1, 6);
        Controls.Add(grid);

        _txtFolder.Text = settings.RecordingDirectory;
        _numRider.Value = (decimal)Math.Clamp(settings.RiderWeightKg, 30, 250);
        _numBike.Value = (decimal)Math.Clamp(settings.BikeWeightKg, 5, 40);
        _numGradeFactor.Value = Math.Clamp(settings.GradeFactorPercent, 10, 100);
        _numStartGear.Value = Math.Clamp(settings.StartGear, RouteSession.MinGear, RouteSession.MaxGear);
        _cmbGearing.Items.AddRange([RouteSession.GearingMtb, RouteSession.GearingRoad]);
        _cmbGearing.SelectedItem = settings.Gearing == RouteSession.GearingRoad ? RouteSession.GearingRoad : RouteSession.GearingMtb;
        _cmbMonitor.Items.Add(new MonitorItem(null, "Automatisch (erster Nebenmonitor)"));
        foreach (var screen in Screen.AllScreens.OrderBy(Monitors.Number))
            _cmbMonitor.Items.Add(new MonitorItem(screen.DeviceName, Monitors.Describe(screen)));
        _cmbMonitor.SelectedItem = _cmbMonitor.Items.Cast<MonitorItem>().FirstOrDefault(m => m.DeviceName == settings.RideMonitor)
            ?? _cmbMonitor.Items[0]; // saved monitor no longer connected
        btnIdentify.Click += (_, _) => Monitors.Identify(TimeSpan.FromSeconds(3));
        RefreshPorts(settings.PortName);
        _lblTest.Text = controller.IsConnected ? $"Aktuell verbunden auf {controller.PortName}" : "Aktuell nicht verbunden";

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
            _lblTest.Text = await Task.Run(() => _controller.TestPort(port));
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
