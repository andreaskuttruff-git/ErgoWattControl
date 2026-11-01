namespace ErgoWattControl;

/// <summary>
/// "Dateiname: [….] .json" row for the start dialogs. Pre-filled with the name the configured pattern would give;
/// <see cref="CustomName"/> is null while the rider leaves it unchanged, so the name is then built at the first
/// pedal stroke and its time stamp stays exact.
/// </summary>
public sealed class RecordingNameBox : FlowLayoutPanel
{
    private readonly TextBox _text = new() { Width = 520 };
    private string _default;

    public RecordingNameBox(string defaultName)
    {
        _default = defaultName;
        _text.Text = defaultName;
        AutoSize = true;
        WrapContents = false;
        Margin = Padding.Empty;
        Controls.Add(_text);
        Controls.Add(new Label { Text = JsonRecorder.Extension, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(2, 6, 0, 0) });
    }

    /// <summary>New default (another program was picked); replaces whatever was typed.</summary>
    public void Reset(string defaultName)
    {
        _default = defaultName;
        _text.Text = defaultName;
    }

    /// <summary>The text field, for dialogs that want it focused.</summary>
    public TextBox Input => _text;

    public string? CustomName => _text.Text.Trim() == _default ? null : AppSettings.SanitizeFileName(_text.Text);
}
