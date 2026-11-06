using System.Text.RegularExpressions;

namespace ErgoWattControl;

public static partial class Monitors
{
    /// <summary>The configured ride-view monitor, or the first non-primary one when unset or no longer connected.</summary>
    public static Screen RideScreen(string? deviceName) =>
        Screen.AllScreens.FirstOrDefault(s => s.DeviceName == deviceName)
        ?? Screen.AllScreens.FirstOrDefault(s => !s.Primary)
        ?? Screen.PrimaryScreen!;

    /// <summary>The ride view is laid out for the small rider monitor, so it never grows beyond the smallest connected screen.</summary>
    public static Size MaxRideViewSize(Screen rideScreen)
    {
        var smallest = Screen.AllScreens.MinBy(s => s.Bounds.Width * s.Bounds.Height)!.Bounds.Size;
        return new Size(Math.Min(rideScreen.Bounds.Width, smallest.Width), Math.Min(rideScreen.Bounds.Height, smallest.Height));
    }

    /// <summary>Number as Windows shows it in display settings (taken from \\.\DISPLAYn), falling back to list position.</summary>
    public static int Number(Screen screen)
    {
        var match = DisplayNumber().Match(screen.DeviceName);
        return match.Success ? int.Parse(match.Groups[1].Value) : Array.IndexOf(Screen.AllScreens, screen) + 1;
    }

    public static string Describe(Screen screen) =>
        $"Monitor {Number(screen)} \u2013 {screen.Bounds.Width}\u00d7{screen.Bounds.Height}" + (screen.Primary ? " (Hauptmonitor)" : "");

    /// <summary>Shows each monitor's number in large digits for a few seconds, like Windows' "Identify".</summary>
    public static void Identify(TimeSpan duration)
    {
        foreach (var screen in Screen.AllScreens)
        {
            var size = new Size(260, 260);
            var form = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(
                    screen.Bounds.X + (screen.Bounds.Width - size.Width) / 2,
                    screen.Bounds.Y + (screen.Bounds.Height - size.Height) / 2,
                    size.Width, size.Height),
                BackColor = Color.FromArgb(30, 30, 30),
                TopMost = true,
                ShowInTaskbar = false,
            };
            var label = new Label
            {
                Text = Number(screen).ToString(),
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 120, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            label.Click += (_, _) => form.Close();
            form.Controls.Add(label);

            var timer = new System.Windows.Forms.Timer { Interval = (int)duration.TotalMilliseconds };
            timer.Tick += (_, _) => form.Close();
            form.FormClosed += (_, _) => timer.Dispose(); // a closed modeless form disposes itself
            form.Show();
            timer.Start();
        }
    }

    /// <summary>
    /// App pixels for the given number of real screen pixels on that monitor. The app is system-DPI aware, so on a
    /// monitor with a different scaling Windows stretches it: 1280 app pixels are only 1024 real ones on a 100 %
    /// monitor next to a 125 % main monitor.
    /// </summary>
    public static Size FromPhysical(Screen screen, Size physical)
    {
        var mode = new DevMode { dmSize = (short)System.Runtime.InteropServices.Marshal.SizeOf<DevMode>() };
        if (!EnumDisplaySettings(screen.DeviceName, -1 /* ENUM_CURRENT_SETTINGS */, ref mode) || mode.dmPelsWidth <= 0 || mode.dmPelsHeight <= 0)
            return physical;
        return new Size(physical.Width * screen.Bounds.Width / mode.dmPelsWidth, physical.Height * screen.Bounds.Height / mode.dmPelsHeight);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DevMode devMode);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct DevMode
    {
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency, dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [GeneratedRegex(@"DISPLAY(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex DisplayNumber();
}
