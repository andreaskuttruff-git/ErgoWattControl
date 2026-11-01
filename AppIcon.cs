namespace ErgoWattControl;

/// <summary>The exe's icon (ErgoWattControl.ico, an elevation profile) for the app's own windows.</summary>
public static class AppIcon
{
    private static readonly Lazy<Icon?> Icon = new(() => System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath));

    public static Icon? Value => Icon.Value;
}
