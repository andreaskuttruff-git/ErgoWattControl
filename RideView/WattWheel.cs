namespace ErgoWattControl;

/// <summary>Turns mouse-wheel notches over a control tree into ±5 W steps, at most one step per <see cref="MinStepInterval"/>.</summary>
public static class WattWheel
{
    private const int WattPerNotch = 5;

    // A fast spin while riding must not ramp the resistance up abruptly; notches arriving faster are dropped, not queued.
    private static readonly TimeSpan MinStepInterval = TimeSpan.FromMilliseconds(200);

    public static void Attach(Control root, Action<int> onStep)
    {
        // Precision touchpads/wheels report fractions of a notch, so accumulate until a full notch.
        var accumulated = 0;
        var lastStep = DateTime.MinValue;
        void OnWheel(object? sender, MouseEventArgs e)
        {
            // Unhandled wheel messages bubble up to every parent we also listen on; stop that so a notch counts once.
            if (e is HandledMouseEventArgs handled)
                handled.Handled = true;
            accumulated += e.Delta;
            var notches = accumulated / SystemInformation.MouseWheelScrollDelta;
            if (notches == 0)
                return;
            accumulated -= notches * SystemInformation.MouseWheelScrollDelta;

            var now = DateTime.Now;
            if (now - lastStep < MinStepInterval)
                return;
            lastStep = now;
            onStep(Math.Sign(notches) * WattPerNotch);
        }

        void AttachTo(Control control)
        {
            // NumericUpDown already uses the wheel to change its own value.
            if (control is NumericUpDown)
                return;
            control.MouseWheel += OnWheel;
            foreach (Control child in control.Controls)
                AttachTo(child);
        }

        AttachTo(root);
    }
}
