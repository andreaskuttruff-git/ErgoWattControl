namespace ErgoWattControl;

/// <summary>
/// Cadence window in which the eddy-current brake can hold a given power, read off the diagram in the
/// 8008 TRS manual ("Technische Daten", p. 36). ergo_win 2003 marks the same window on its RPM bar
/// (checked: 100 W → 40–100, 120 W → 40–160).
/// </summary>
public static class RpmRange
{
    // (up to and including watt, min RPM, max RPM)
    private static readonly (int Watt, int Min, int Max)[] Steps =
    [
        (50, 40, 60),
        (100, 40, 100),
        (150, 40, 160),
        (250, 50, 160),
        (350, 60, 160),
        (400, 70, 160),
        (450, 80, 160),
        (500, 90, 160),
        (580, 100, 160),
        (640, 110, 160),
        (700, 120, 160),
        (750, 130, 160),
        (int.MaxValue, 140, 160),
    ];

    public static (int Min, int Max) For(int watt)
    {
        foreach (var (limit, min, max) in Steps)
        {
            if (watt <= limit)
                return (min, max);
        }
        throw new InvalidOperationException("unreachable: last step has no upper limit");
    }
}
