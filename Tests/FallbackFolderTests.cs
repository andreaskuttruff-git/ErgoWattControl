namespace ErgoWattControl.Tests;

/// <summary>
/// When the chosen recording folder can't be written (network drive or USB stick missing, no rights), the recording
/// goes to a fallback folder – and the export and replay dialogs still find it.
/// </summary>
public class FallbackFolderTests
{
    private static string DocumentsFolder => AppPaths.DefaultRecordingFolder;

    /// <summary>A path where a file blocks the folder, like a missing or read-only drive.</summary>
    private static string Blocked()
    {
        var path = Path.Combine(TestFolder.New("blockiert"), "Aufzeichnungen");
        File.WriteAllText(path, "");
        return path;
    }

    private static GpxRoute Hill() =>
        GpxRoute.FromTrack("Hügel", null, Enumerable.Range(0, 101).Select(i => (47.0 + i * 0.0001, 9.0, 400.0 + i * 0.5)).ToList());

    [Fact]
    public void Folders_ChosenFirst_ThenDocuments_ThenTheUsersProfile()
    {
        var chosen = TestFolder.New("netz");
        var folders = new AppSettings { RecordingDirectory = chosen }.RecordingFolders();

        Assert.Equal([chosen, DocumentsFolder, AppPaths.FallbackRecordingFolder], folders);
    }

    [Fact]
    public void Folders_WithTheDefaultChosen_ListItOnlyOnce()
    {
        var folders = new AppSettings { RecordingDirectory = DocumentsFolder + "\\" }.RecordingFolders();
        Assert.Equal(2, folders.Count);
        Assert.True(AppSettings.SameFolder(folders[1], AppPaths.FallbackRecordingFolder));
    }

    [Fact]
    public void BlockedFolder_RecordsInDocumentsInstead()
    {
        var settings = new AppSettings { RecordingDirectory = Blocked() };
        var training = new TrainingController(settings, new ErgoController());
        var mark = TestFolder.LogMark();
        training.BeginRoute(Hill(), customName: "Ausweich");

        var recorder = training.StartRecording();
        training.StopRecording();

        Assert.True(AppSettings.SameFolder(Path.GetDirectoryName(recorder.FilePath)!, DocumentsFolder));
        Assert.Equal("Ausweich.json", Path.GetFileName(recorder.FilePath)); // the typed name still counts
        Assert.True(settings.InFallbackFolder(recorder.FilePath));
        Assert.Contains("not possible", TestFolder.LogSince(mark)); // the reason is logged
        File.Delete(recorder.FilePath);
    }

    [Fact]
    public void ChosenAndDocumentsBlocked_RecordsInTheUsersProfile()
    {
        var settings = new AppSettings { RecordingDirectory = Blocked() };
        var training = new TrainingController(settings, new ErgoController());
        var documents = Blocked();
        training.RecordingFolders = () => [settings.RecordingDirectory, documents, AppPaths.FallbackRecordingFolder];

        var recorder = training.StartRecording();
        Assert.True(training.RecordingInFallbackFolder);
        training.StopRecording();

        Assert.True(AppSettings.SameFolder(Path.GetDirectoryName(recorder.FilePath)!, AppPaths.FallbackRecordingFolder));
    }

    [Fact]
    public void WritableFolder_IsNoFallback()
    {
        var settings = new AppSettings { RecordingDirectory = TestFolder.New("normal") };
        var training = new TrainingController(settings, new ErgoController());

        training.StartRecording();

        Assert.False(training.RecordingInFallbackFolder);
        training.StopRecording();
    }

    [Fact]
    public void RecordingFiles_FindRecordingsInAllFolders_AndSkipBlockedOnes()
    {
        var settings = new AppSettings { RecordingDirectory = Blocked() };
        Directory.CreateDirectory(AppPaths.FallbackRecordingFolder);
        var older = Path.Combine(DocumentsFolder, "Dokumente_Lauf.json");
        var newer = Path.Combine(AppPaths.FallbackRecordingFolder, "Profil_Lauf.json");
        Directory.CreateDirectory(DocumentsFolder);
        File.WriteAllText(older, "{}");
        File.WriteAllText(newer, "{}");
        File.SetLastWriteTime(older, DateTime.Now.AddHours(-1));
        try
        {
            var files = settings.RecordingFiles("*.json");

            Assert.Equal(newer, files[0]); // newest first
            Assert.Contains(older, files);
            Assert.All(files, f => Assert.True(settings.InFallbackFolder(f)));
        }
        finally
        {
            File.Delete(older);
            File.Delete(newer);
        }
    }
}
