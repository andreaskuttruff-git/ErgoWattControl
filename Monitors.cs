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

    [GeneratedRegex(@"DISPLAY(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex DisplayNumber();
}
