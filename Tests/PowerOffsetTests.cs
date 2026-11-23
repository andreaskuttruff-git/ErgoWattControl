using Xunit;

namespace ErgoWattControl.Tests;

/// <summary>The „Leistungsoffset“ of the start dialog: its range and how the ride starts with it.</summary>
public class PowerOffsetTests
{
    private static WorkoutRecording Profile(params int[] watts) =>
        WorkoutRecording.FromProfile("Test", watts.Select((w, i) => (i * 60.0, w)).Append((watts.Length * 60.0, watts[^1])));

    [Fact]
    public void Range_KeepsTheLowestPointAtZero_AndTheHighestAtTheLimit()
    {
        // Lowest 70 W, highest 130 W, limit 800 W: down by 70, up by 670.
        Assert.Equal((-70, 670), ReplayPickerForm.OffsetRange(Profile(70, 90, 130, 80), 800));
    }

    [Fact]
    public void Range_IsInFiveWattSteps()
    {
        // 73 W lowest: only -70 keeps it at or above 0; 800 - 132 = 668: only +665 stays below the limit.
        Assert.Equal((-70, 665), ReplayPickerForm.OffsetRange(Profile(73, 132), 800));
    }

    [Fact]
    public void Range_WithAZeroPoint_CannotGoDown()
    {
        Assert.Equal((0, 650), ReplayPickerForm.OffsetRange(Profile(0, 150), 800));
    }

    [Fact]
    public void Range_ProfileAboveTheLimit_OnlyDown()
    {
        // Highest 300 W over a 250-W limit: at most -50 (and never below the lowest point's -100).
        Assert.Equal((-100, -50), ReplayPickerForm.OffsetRange(Profile(100, 300), 250));
    }

    [Fact]
    public void Range_ProfileReachingTheLimit_CannotGoUp()
    {
        Assert.Equal((-50, 0), ReplayPickerForm.OffsetRange(Profile(50, 800), 800));
    }

    [Fact]
    public void Range_ProfileFarAboveTheLimit_StopsAtTheLowestPoint()
    {
        // Would need -500 to get under the limit, but the lowest point (100 W) allows only -100: both ends -100.
        Assert.Equal((-100, -100), ReplayPickerForm.OffsetRange(Profile(100, 900), 400));
    }

    [Fact]
    public void Range_LowestPointBelowFive_CannotGoDown()
    {
        Assert.Equal((0, 795), ReplayPickerForm.OffsetRange(Profile(3, 5), 800));
    }

    [Fact]
    public void Range_HighestPointJustUnderAStep_RoundsDown()
    {
        // 800 - 101 = 699: +695 is the last 5-W step that stays at or below the limit.
        Assert.Equal((-100, 695), ReplayPickerForm.OffsetRange(Profile(100, 101), 800));
    }

    [Fact]
    public void Range_FlatProfile()
    {
        Assert.Equal((-150, 650), ReplayPickerForm.OffsetRange(Profile(150), 800));
    }

    [Theory]
    [InlineData(25)]
    [InlineData(250)]
    [InlineData(800)]
    public void Range_ShiftedProfile_StaysBetweenZeroAndTheLimit(int limit)
    {
        var profile = Profile(35, 80, 140, 60);
        var (min, max) = ReplayPickerForm.OffsetRange(profile, limit);
        Assert.True(min <= max);
        Assert.Equal(0, min % 5);
        Assert.Equal(0, max % 5);
        Assert.True(profile.MinWatt + min >= 0);
        // Above the limit only when even the lowest allowed shift can't get it under (here: never below 35 W).
        if (max > min)
            Assert.True(profile.MaxWatt + max <= limit);
    }

    [Fact]
    public void ShiftedProfile_HighestPointIncludesTheOffset()
    {
        // P01-like: 25 to 400 W, shifted by +175: the graph's scale must reach 575 W, not stop at the original 400.
        var profile = Profile(25, 200, 400);
        Assert.Equal(575, profile.MaxWattOf(s => profile.WattAt(s) + 175));
        Assert.Equal(400, profile.MaxWattOf(profile.WattAt));
    }

    // ---- The offset box in the programs dialog ----

    /// <summary>Two programs: "A" from 70 to 130 W, "B" from 40 to 300 W; power limit 800 W.</summary>
    private static ReplayPickerForm ProgramsDialog()
    {
        var folder = TestFolder.New("offset_programs");
        File.WriteAllText(Path.Combine(folder, "A.json"), """{ "Name": "A", "Profile": [[0, 70], [60, 130], [120, 90], [180, 90]] }""");
        File.WriteAllText(Path.Combine(folder, "B.json"), """{ "Name": "B", "Profile": [[0, 40], [60, 300], [120, 300]] }""");
        var settings = new AppSettings { RecordingDirectory = TestFolder.New("offset_rec") };
        settings.Ride.MaxWattLimit = 800;
        return ReplayPickerForm.ForPrograms(folder, settings, _ => { });
    }

    private static void OnSta(Action<ReplayPickerForm> test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var dialog = ProgramsDialog();
                test(dialog);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
            throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    [Fact]
    public void Dialog_OpensAtZero_WithTheRangeOfTheFirstProgram()
    {
        OnSta(dialog =>
        {
            Assert.Equal(0, dialog.OffsetBox.Value);
            Assert.Equal(-70, dialog.OffsetBox.Minimum);
            Assert.Equal(670, dialog.OffsetBox.Maximum);
            Assert.Equal(5, dialog.OffsetBox.Increment);
            Assert.Equal(0, dialog.PowerOffset);
        });
    }

    [Fact]
    public void Dialog_LowerPowerLimit_NarrowsTheRange_AndPullsTheValueBack()
    {
        OnSta(dialog =>
        {
            dialog.OffsetBox.Value = 300;
            dialog.PowerLimitBox.Value = 200;
            Assert.Equal(70, dialog.OffsetBox.Maximum);
            Assert.Equal(70, dialog.PowerOffset);
            dialog.PowerLimitBox.Value = 800;
            Assert.Equal(670, dialog.OffsetBox.Maximum);
            Assert.Equal(70, dialog.PowerOffset);
        });
    }

    [Fact]
    public void Dialog_OtherProgram_TakesItsRange()
    {
        OnSta(dialog =>
        {
            dialog.OffsetBox.Value = -70;
            dialog.Select(1);
            Assert.Equal(-40, dialog.OffsetBox.Minimum);
            Assert.Equal(500, dialog.OffsetBox.Maximum);
            Assert.Equal(-40, dialog.PowerOffset);
        });
    }

    [Theory]
    [InlineData(7, 5)]
    [InlineData(12, 10)]
    [InlineData(13, 15)]
    [InlineData(-8, -10)]
    [InlineData(-2, 0)]
    public void Dialog_TypedValues_GoToFiveWattSteps(int typed, int expected)
    {
        OnSta(dialog =>
        {
            dialog.OffsetBox.Value = typed;
            Assert.Equal(expected, dialog.PowerOffset);
        });
    }

    [Fact]
    public void Session_StartsWithTheOffset_AndTheKeysGoOnFromThere()
    {
        var session = new ReplaySession(Profile(100, 150), 25, 800, offset: -20);
        Assert.Equal(-20, session.Offset);
        Assert.Equal(80, session.TargetWatt);
        Assert.Equal(85, session.ShiftOffset(5));
    }

    [Fact]
    public void Session_OffsetAboveTheKeyLimit_CanBeSteppedBack()
    {
        var session = new ReplaySession(Profile(25), 25, 800, offset: 500);
        Assert.Equal(525, session.TargetWatt);
        session.ShiftOffset(5);
        Assert.Equal(500, session.Offset);
        session.ShiftOffset(-5);
        Assert.Equal(495, session.Offset);
    }
}
