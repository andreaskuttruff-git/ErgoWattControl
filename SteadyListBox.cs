namespace ErgoWattControl;

/// <summary>
/// A list box that ignores the horizontal mouse wheel (WM_MOUSEHWHEEL). Windows' list box scrolls its rows
/// <i>vertically</i> on it – and some mouse/touchpad drivers (seen on the KellerPC: Synaptics on a desktop) send a
/// stream of them while the mouse merely moves, so the list ran along with the pointer. Long names can still be
/// scrolled sideways with the scroll bar.
/// </summary>
public class SteadyListBox : ListBox
{
    internal const int WM_MOUSEHWHEEL = 0x020E;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEHWHEEL)
        {
            m.Result = 1; // handled: not passed on to the list box (nor to its parent)
            return;
        }
        base.WndProc(ref m);
    }
}
