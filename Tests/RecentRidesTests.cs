using Xunit;

namespace ErgoWattControl.Tests;

/// <summary>"Kürzlich" in the menu: the last files ridden, and the start dialogs opening with one of them selected.</summary>
public class RecentRidesTests
{
    [Fact]
    public void Remember_PutsTheNewestFirst_WithoutDuplicates_AtMostThree()
    {
        var list = new List<string>();
        foreach (var file in new[] { "a", "b", "c", "A", "d" })
            RecentRides.Remember(list, file);
        // "A" is "a" ridden again (file names ignore case): it moved up instead of appearing twice.
        Assert.Equal(["d", "A", "c"], list);
    }

    [Fact]
    public void Existing_LeavesOutDeletedFiles()
    {
        var folder = TestFolder.New("recent");
        var kept = Path.Combine(folder, "kept.gpx");
        File.WriteAllText(kept, "");
        Assert.Equal([kept], RecentRides.Existing([Path.Combine(folder, "gone.gpx"), kept]));
        Assert.Empty(RecentRides.Existing(null));
    }

    [Fact]
    public void Settings_KeepTheLists()
    {
        var settings = new AppSettings();
        RecentRides.Remember(settings.Recent.Routes, @"C:\GPX\Runde.gpx");
        RecentRides.Remember(settings.Recent.Programs, @"C:\Programs\P01.json");
        var copy = settings.Snapshot();
        Assert.Equal([@"C:\GPX\Runde.gpx"], copy.Recent.Routes);
        Assert.Equal([@"C:\Programs\P01.json"], copy.Recent.Programs);
        Assert.Empty(copy.Recent.Recordings);
    }

    [Fact]
    public void Settings_WithoutOrWithNullLists_StartEmpty()
    {
        var old = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("""{ "Recent": { "Routes": null } }""")!;
        Assert.Empty(old.Recent.Routes);
        Assert.Empty(System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{}")!.Recent.Programs);
    }

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

    private static (ReplayPickerForm Dialog, string A, string B) Programs()
    {
        var folder = TestFolder.New("recent_programs");
        string a = Path.Combine(folder, "A.json"), b = Path.Combine(folder, "B.json");
        File.WriteAllText(a, """{ "Name": "A", "Profile": [[0, 70], [60, 130]] }""");
        File.WriteAllText(b, """{ "Name": "B", "Profile": [[0, 40], [60, 300]] }""");
        var settings = new AppSettings { RecordingDirectory = TestFolder.New("recent_rec") };
        return (ReplayPickerForm.ForPrograms(folder, settings, _ => { }), a, b);
    }

    [Fact]
    public void ProgramsDialog_OpensWithTheRecentProgramSelected()
    {
        OnSta(() =>
        {
            var (dialog, a, b) = Programs();
            using (dialog)
            {
                Assert.Equal(a, dialog.SelectedFile);
                dialog.Preselect(b);
                Assert.Equal(b, dialog.SelectedFile);
                Assert.Equal("B", dialog.Workout?.Name);
            }
        });
    }

    [Fact]
    public void ProgramsDialog_ProgramNoLongerThere_OpensAsUsual()
    {
        OnSta(() =>
        {
            var (dialog, a, _) = Programs();
            using (dialog)
            {
                var elsewhere = Path.Combine(TestFolder.New("recent_elsewhere"), "C.json");
                File.WriteAllText(elsewhere, """{ "Name": "C", "Profile": [[0, 100], [60, 100]] }""");
                dialog.Preselect(elsewhere);
                dialog.Preselect(Path.Combine(TestFolder.New("recent_missing"), "gone.json"));
                Assert.Equal(a, dialog.SelectedFile);
            }
        });
    }

    [Fact]
    public void RouteDialog_OpensWithTheRecentRouteSelected_AlsoFromAnotherFolder()
    {
        const string gpx = """
            <?xml version="1.0"?>
            <gpx version="1.1" creator="test" xmlns="http://www.topografix.com/GPX/1/1"><trk><name>{0}</name><trkseg>
            <trkpt lat="48.60" lon="8.90"><ele>400</ele></trkpt><trkpt lat="48.61" lon="8.90"><ele>420</ele></trkpt>
            <trkpt lat="48.62" lon="8.90"><ele>430</ele></trkpt></trkseg></trk></gpx>
            """;
        OnSta(() =>
        {
            var folder = TestFolder.New("recent_gpx");
            string first = Path.Combine(folder, "A.gpx"), second = Path.Combine(folder, "B.gpx");
            File.WriteAllText(first, gpx.Replace("{0}", "A"));
            File.WriteAllText(second, gpx.Replace("{0}", "B"));
            var other = Path.Combine(TestFolder.New("recent_gpx_other"), "C.gpx");
            File.WriteAllText(other, gpx.Replace("{0}", "C"));
            using var dialog = new RoutePickerForm(new AppSettings { GpxDirectory = folder }, 800);

            Assert.Equal(first, dialog.SelectedFile);
            dialog.Preselect(second);
            Assert.Equal(second, dialog.SelectedFile);
            dialog.Preselect(other);
            Assert.Equal(other, dialog.SelectedFile);
            Assert.Equal("C", dialog.Route?.Name);
        });
    }
}
