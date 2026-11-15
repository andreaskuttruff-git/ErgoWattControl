namespace ErgoWattControl;

/// <summary>
/// A list box keeps the mouse captured from button-down to button-up and meanwhile selects (and scrolls) along with
/// the mouse. If the button-up gets lost – over RDP, or while the selection change still loads a preview – it stays
/// in that mode: merely moving the mouse over the list then selects other entries and scrolls it.
/// </summary>
public static class ListBoxCapture
{
    /// <summary>Ends a selection by mouse that outlived its button: no mouse button is down, but the list still holds the capture.</summary>
    public static void ReleaseStuckCapture(ListBox list) =>
        list.MouseMove += (_, _) =>
        {
            if (Control.MouseButtons == MouseButtons.None && list.Capture)
                list.Capture = false;
        };

    /// <summary>
    /// Runs <paramref name="action"/> after the list's selection changed – once the list has finished handling the
    /// mouse or key press that changed it, so a slow preview never delays its button-up. Several quick changes
    /// (holding the arrow key) run it once for the last selection.
    /// </summary>
    public static void OnSelectionSettled(ListBox list, Action action)
    {
        var pending = false;
        list.SelectedIndexChanged += (_, _) =>
        {
            if (!list.IsHandleCreated)
            {
                action(); // while the window is being built (first selection): right away
                return;
            }
            if (pending)
                return;
            pending = true;
            list.BeginInvoke(() =>
            {
                pending = false;
                action();
            });
        };
    }
}
