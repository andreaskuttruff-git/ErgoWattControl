namespace ErgoWattControl.Tests;

/// <summary>The dark message box answers like Windows' MessageBox (<see cref="DarkMessageBox"/>).</summary>
public class DarkMessageBoxTests
{
    [Theory]
    [InlineData(MessageBoxButtons.OK, DialogResult.OK, DialogResult.OK)]
    [InlineData(MessageBoxButtons.OKCancel, DialogResult.OK, DialogResult.Cancel)]
    [InlineData(MessageBoxButtons.YesNo, DialogResult.Yes, DialogResult.No)]
    [InlineData(MessageBoxButtons.YesNoCancel, DialogResult.Yes, DialogResult.Cancel)]
    [InlineData(MessageBoxButtons.RetryCancel, DialogResult.Retry, DialogResult.Cancel)]
    public void FirstButtonIsTheDefault_EscGivesNoOrCancel(MessageBoxButtons buttons, DialogResult enter, DialogResult esc) =>
        OnSta(() =>
        {
            using var box = new DarkMessageBox("Text", "Titel", buttons, MessageBoxIcon.Question);
            Assert.Equal(enter, ((Button)box.AcceptButton!).DialogResult);
            Assert.Equal(esc, ((Button)box.CancelButton!).DialogResult);
        });

    [Fact]
    public void ButtonsAreInTheUsualOrder_AndInTheAppLanguage() =>
        OnSta(() =>
        {
            using var box = new DarkMessageBox("Text", "Titel", MessageBoxButtons.YesNo, MessageBoxIcon.None);
            // The footer flows right to left: the last control added is the leftmost button.
            var buttons = Descendants(box).OfType<Button>().Reverse().ToList();
            Assert.Equal([DialogResult.Yes, DialogResult.No], buttons.Select(b => b.DialogResult));
            Assert.Equal([L.T("Button_Yes"), L.T("Button_No")], buttons.Select(b => b.Text));
        });

    [Fact]
    public void IconImage_IsDisposedWithTheBox() =>
        OnSta(() =>
        {
            var box = new DarkMessageBox("Text", "Titel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            var image = Descendants(box).OfType<PictureBox>().Single().Image!;
            box.Dispose();
            Assert.Throws<ArgumentException>(() => image.Width); // GDI+ image already released
        });

    [Fact]
    public void LongPaths_CanWrap() =>
        Assert.Equal(@"C:\" + "\u200B" + @"Users\" + "\u200B" + "a.log https:/\u200B/\u200Bx.org",
            DarkMessageBox.Breakable(@"C:\Users\a.log https://x.org"));

    private static IEnumerable<Control> Descendants(Control c) =>
        c.Controls.Cast<Control>().SelectMany(child => Descendants(child).Prepend(child));

    private static void OnSta(Action test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { test(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
            throw new Xunit.Sdk.XunitException(failure.ToString());
    }
}
