using System.Runtime.InteropServices;

namespace ErgoWattControl.Tests;

/// <summary>
/// The pick lists must not run along with the mouse: on the KellerPC a driver sends a stream of horizontal wheel
/// messages while the mouse only moves, and Windows' list box scrolls its rows vertically on them.
/// </summary>
public class SteadyListBoxTests
{
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    /// <summary>Top row after 20 horizontal wheel messages (tilt right) over a long list shown off-screen.</summary>
    private static int TopAfterHorizontalWheel(ListBox list)
    {
        var top = -1;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000), Size = new Size(400, 500), ShowInTaskbar = false };
                list.Dock = DockStyle.Fill;
                list.IntegralHeight = false;
                list.HorizontalScrollbar = true;
                for (var i = 1; i <= 100; i++)
                    list.Items.Add($"Programm {i}");
                form.Controls.Add(list);
                form.Show();
                list.SelectedIndex = 0;
                var point = list.PointToScreen(new Point(50, 100));
                for (var i = 0; i < 20; i++)
                    SendMessage(list.Handle, SteadyListBox.WM_MOUSEHWHEEL, 30 << 16, ((point.Y & 0xFFFF) << 16) | (point.X & 0xFFFF));
                top = list.TopIndex;
                Assert.Equal(0, list.SelectedIndex); // the selection never moves – only the view
                form.Hide();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
            throw new InvalidOperationException(error.Message, error);
        return top;
    }

    [Fact]
    public void PlainListBox_ScrollsVertically_OnTheHorizontalWheel() =>
        // The cause, kept as a check: if Windows ever stops doing this, the workaround is no longer needed.
        Assert.True(TopAfterHorizontalWheel(new ListBox()) > 0, "Die normale Liste scrollt nicht mehr beim seitlichen Mausrad.");

    [Fact]
    public void SteadyListBox_StaysPut_OnTheHorizontalWheel() =>
        Assert.Equal(0, TopAfterHorizontalWheel(new SteadyListBox()));
}
