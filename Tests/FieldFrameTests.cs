namespace ErgoWattControl.Tests;

/// <summary>The rounded frame around text, number and list fields (<see cref="FieldFrame"/>).</summary>
public class FieldFrameTests
{
    [Fact]
    public void TextField_LosesItsBorder_AndTheFrameTakesItsPlace()
    {
        var text = new TextBox { Width = 300, Margin = new Padding(1, 2, 3, 4), Anchor = AnchorStyles.Left };
        var outer = FieldFrame.Wrap(text);

        var frame = Assert.IsType<FieldFrame>(outer);
        Assert.Same(text, frame.Field);
        Assert.Same(frame, text.Parent);
        Assert.Equal(BorderStyle.None, text.BorderStyle);
        Assert.Equal(DockStyle.Fill, text.Dock);
        Assert.Equal(300, frame.Width);
        Assert.Equal(new Padding(1, 2, 3, 4), frame.Margin);
        Assert.Equal(Padding.Empty, text.Margin);
        Assert.Equal(AnchorStyles.Left, frame.Anchor);
        Assert.True(frame.Height > text.Height); // room for the frame line above and below
    }

    [Fact]
    public void DockedList_KeepsItsDock() =>
        Assert.Equal(DockStyle.Fill, FieldFrame.Wrap(new ListBox { Dock = DockStyle.Fill }).Dock);

    [Fact]
    public void NumberField_IsFramed() =>
        Assert.IsType<FieldFrame>(FieldFrame.Wrap(new NumericUpDown()));

    [Fact]
    public void DropDownsAndBorderlessFields_StayAsTheyAre()
    {
        var combo = new ComboBox();
        var plain = new TextBox { BorderStyle = BorderStyle.None };
        Assert.Same(combo, FieldFrame.Wrap(combo));
        Assert.Same(plain, FieldFrame.Wrap(plain));
        Assert.Same(plain, FieldFrame.Outer(plain));
    }

    [Fact]
    public void WrappingTwice_GivesTheSameFrame()
    {
        var text = new TextBox();
        var frame = FieldFrame.Wrap(text);
        Assert.Same(frame, FieldFrame.Wrap(text));
        Assert.Same(frame, FieldFrame.Outer(text));
    }

    [Fact]
    public void FrameIsDark_WhileTheFieldIsDisabled()
    {
        var text = new TextBox();
        var frame = (FieldFrame)FieldFrame.Wrap(text);
        var normal = frame.FrameColor;
        text.Enabled = false;
        Assert.NotEqual(normal, frame.FrameColor);
        Assert.True(frame.FrameColor.GetBrightness() < normal.GetBrightness());
    }
}
