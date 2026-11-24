namespace ErgoWattControl.Tests;

/// <summary>The size of the ride view from the setting "Größe der Fahransicht" (<see cref="Monitors.RideViewMax"/>).</summary>
public class RideViewSizeTests
{
    private static readonly Size FullHd = new(1920, 1080);
    /// <summary>What "as before" gives on a 1920×1080 monitor next to a 1280×1024 one.</summary>
    private static readonly Size AsBefore = new(1280, 1024);

    /// <summary>A monitor at 100 % scaling: real pixels = app pixels.</summary>
    private static Size Unscaled(Size physical) => physical;

    [Fact]
    public void Default_IsAsBefore()
    {
        Assert.Equal(Monitors.SizeAuto, new AppSettings().RideViewSize);
        Assert.Equal(AsBefore, Monitors.RideViewMax(Monitors.SizeAuto, FullHd, AsBefore, Unscaled));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("riesig")]
    public void UnknownValue_MeansAsBefore(string? setting) =>
        Assert.Equal(AsBefore, Monitors.RideViewMax(setting, FullHd, AsBefore, Unscaled));

    [Fact]
    public void WholeMonitor_FillsTheScreen() =>
        Assert.Equal(FullHd, Monitors.RideViewMax(Monitors.SizeScreen, FullHd, AsBefore, Unscaled));

    [Theory]
    [InlineData("1600x900", 1600, 900)]
    [InlineData("1024×768", 1024, 768)]
    [InlineData(" 1366 X 768 ", 1366, 768)]
    public void FixedSize_IsUsed(string setting, int width, int height) =>
        Assert.Equal(new Size(width, height), Monitors.RideViewMax(setting, FullHd, AsBefore, Unscaled));

    [Fact]
    public void FixedSize_NeverExceedsTheScreen() =>
        Assert.Equal(FullHd, Monitors.RideViewMax("2560x1440", FullHd, AsBefore, Unscaled));

    [Fact]
    public void FixedSize_IsInRealScreenPixels_OnAScaledMonitor()
    {
        // A 125 % monitor next to a 100 % main monitor: 1280 real pixels are 1600 app pixels … or the other way
        // round – whatever the conversion says, the result is its answer.
        static Size Scaled(Size physical) => new(physical.Width * 4 / 5, physical.Height * 4 / 5);
        Assert.Equal(new Size(1024, 819), Monitors.RideViewMax("1280x1024", FullHd, AsBefore, Scaled));
    }

    [Fact]
    public void SmallestMonitor_IsTheRideMonitor_AtMostTheSmallestScreen() =>
        Assert.Equal(new Size(1280, 1024), Monitors.AutoRideViewSize(FullHd, new Size(1280, 1024), demoLimit: null));

    [Fact]
    public void SmallestMonitor_LimitsEachSideOnItsOwn() =>
        Assert.Equal(new Size(1680, 1050), Monitors.AutoRideViewSize(new Size(1920, 1200), new Size(1680, 1050), demoLimit: null));

    [Fact]
    public void SmallestMonitor_InTheDemo_IsAtMostTheErgometerMonitor()
    {
        var big = new Size(1920, 1200);
        Assert.Equal(big, Monitors.AutoRideViewSize(big, big, demoLimit: null));
        Assert.Equal(Monitors.ErgometerMonitorSize, Monitors.AutoRideViewSize(big, big, Monitors.ErgometerMonitorSize));
    }

    [Fact]
    public void ScaledMonitor_ConvertsThereAndBackToTheSameSize()
    {
        // 2560×1440 at 125 %: Windows reports 2048×1152 for the system-DPI-aware app.
        var mode = new Size(2560, 1440);
        var bounds = new Size(2048, 1152);
        var app = Monitors.Scale(Monitors.ErgometerMonitorSize, bounds, mode);
        Assert.Equal(new Size(1024, 819), app);
        Assert.Equal(Monitors.ErgometerMonitorSize, Monitors.Scale(app, mode, bounds));
    }

    [Theory]
    [InlineData("1280x1024", 1280, 1024)]
    [InlineData("auto", null, null)]
    [InlineData("screen", null, null)]
    [InlineData("12x34", null, null)]
    public void ParseSize_ReadsWidthAndHeight(string setting, int? width, int? height) =>
        Assert.Equal(width is null ? null : new Size(width.Value, height!.Value), Monitors.ParseSize(setting));

    [Fact]
    public void FormatSize_IsReadBackTheSame()
    {
        foreach (var size in Monitors.FixedRideViewSizes)
            Assert.Equal(size, Monitors.ParseSize(Monitors.FormatSize(size)));
    }

    [Fact]
    public void Setting_IsSavedAndLoaded()
    {
        File.Delete(AppPaths.SettingsFile);
        try
        {
            new AppSettings { RideViewSize = "1600x900" }.Save();
            Assert.Equal("1600x900", AppSettings.Load().RideViewSize);
        }
        finally
        {
            File.Delete(AppPaths.SettingsFile);
        }
    }
}
