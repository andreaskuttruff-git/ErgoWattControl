namespace ErgoWattControl;

/// <summary>
/// "Dateiname: [….] .csv" row for the start dialogs. Pre-filled with the name the configured pattern would give;
/// <see cref="CustomName"/> is null while the rider leaves it unchanged, so the name is then built at the first
/// pedal stroke and its time stamp stays exact.
/// </summary>
public sealed class RecordingNameBox : FlowLayoutPanel
{
    private readonly TextBox _text = new() { Width = 520 };
    private readonly string _default;

    public RecordingNameBox(string defaultName)
    {
        _default = defaultName;
        _text.Text = defaultName;
        AutoSize = true;
        WrapContents = false;
        Margin = Padding.Empty;
        Controls.Add(_text);
        Controls.Add(new Label { Text = ".csv", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(2, 6, 0, 0) });
    }

    public string? CustomName => _text.Text.Trim() == _default ? null : AppSettings.SanitizeFileName(_text.Text);
}
