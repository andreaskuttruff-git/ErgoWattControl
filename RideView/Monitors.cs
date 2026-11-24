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

    /// <summary>
    /// <see cref="MaxRideViewSize"/> in real screen pixels, for the settings list: the smallest connected monitor's
    /// resolution, at most that of <paramref name="rideScreen"/>.
    /// </summary>
    public static Size SmallestPhysicalSize(Screen rideScreen)
    {
        var smallest = PhysicalSize(Screen.AllScreens.MinBy(s => s.Bounds.Width * s.Bounds.Height)!);
        var ride = PhysicalSize(rideScreen);
        return new Size(Math.Min(ride.Width, smallest.Width), Math.Min(ride.Height, smallest.Height));
    }

    /// <summary>Ride view size "smallest monitor" (the default, formerly "as before"): the rider monitor, at most the smallest connected screen (the demo: 1280×1024).</summary>
    public const string SizeAuto = "auto";
    /// <summary>Ride view size: the whole monitor it is on.</summary>
    public const string SizeScreen = "screen";

    /// <summary>The fixed sizes offered (real screen pixels); the settings list those that fit the chosen monitor.</summary>
    public static readonly IReadOnlyList<Size> FixedRideViewSizes =
    [
        new(1024, 768), new(1280, 1024), new(1366, 768), new(1440, 900), new(1600, 900),
        new(1680, 1050), new(1920, 1080), new(1920, 1200), new(2560, 1440), new(3840, 2160),
    ];

    /// <summary>"1280x1024" → 1280×1024; null for "auto", "screen" or anything unreadable.</summary>
    public static Size? ParseSize(string? setting)
    {
        var match = SizeSetting().Match(setting ?? "");
        return match.Success ? new Size(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value)) : null;
    }

    public static string FormatSize(Size size) => $"{size.Width}x{size.Height}";

    /// <summary>
    /// Largest size of the ride view window, in app pixels, for the size setting: <see cref="SizeAuto"/> = as before
    /// (<paramref name="autoMax"/>), <see cref="SizeScreen"/> = the whole screen, "WxH" = that many real screen pixels
    /// (converted by <paramref name="fromPhysical"/> for a differently scaled monitor). The window never grows beyond
    /// the screen it is on.
    /// </summary>
    public static Size RideViewMax(string? setting, Size screen, Size autoMax, Func<Size, Size> fromPhysical)
    {
        var max = setting == SizeScreen ? screen
            : ParseSize(setting) is { } fixedSize ? fromPhysical(fixedSize)
            : autoMax;
        return new Size(Math.Min(max.Width, screen.Width), Math.Min(max.Height, screen.Height));
    }

    /// <summary>Resolution of the monitor in real pixels (its display mode), or its bounds if that can't be read.</summary>
    public static Size PhysicalSize(Screen screen)
    {
        var mode = new DevMode { dmSize = (short)System.Runtime.InteropServices.Marshal.SizeOf<DevMode>() };
        return EnumDisplaySettings(screen.DeviceName, -1 /* ENUM_CURRENT_SETTINGS */, ref mode) && mode.dmPelsWidth > 0 && mode.dmPelsHeight > 0
            ? new Size(mode.dmPelsWidth, mode.dmPelsHeight)
            : screen.Bounds.Size;
    }

    /// <summary>Number as Windows shows it in display settings (taken from \\.\DISPLAYn), falling back to list position.</summary>
    public static int Number(Screen screen)
    {
        var match = DisplayNumber().Match(screen.DeviceName);
        return match.Success ? int.Parse(match.Groups[1].Value) : Array.IndexOf(Screen.AllScreens, screen) + 1;
    }

    public static string Describe(Screen screen) =>
        L.F("Monitor_Describe", Number(screen), screen.Bounds.Width, screen.Bounds.Height) + (screen.Primary ? L.T("Monitor_Primary") : "");

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

    [GeneratedRegex(@"^\s*(\d{3,5})\s*[x×]\s*(\d{3,5})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SizeSetting();
}
