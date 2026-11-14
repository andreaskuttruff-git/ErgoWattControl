using System.Runtime.CompilerServices;

// The tests share AppPaths (one settings folder, one Fehler.log), so they run one after another.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ErgoWattControl.Tests;

/// <summary>
/// Before any test runs, settings, Fehler.log and data are moved to a fresh temporary folder – no test can touch the
/// rider's real settings.json or recordings.
/// </summary>
internal static class TestFolder
{
    public static string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ErgoWattControl.Tests", Guid.NewGuid().ToString("N"));

    [ModuleInitializer]
    internal static void Init()
    {
        Directory.CreateDirectory(Path);
        AppPaths.UseFolderForTests(Path);
    }

    /// <summary>A new empty folder below the test folder.</summary>
    public static string New(string name)
    {
        var folder = System.IO.Path.Combine(Path, name + "_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>Fehler.log lines written since <paramref name="mark"/> (taken with <see cref="LogMark"/>).</summary>
    public static string LogSince(long mark)
    {
        if (!File.Exists(AppPaths.LogFile))
            return "";
        using var stream = new FileStream(AppPaths.LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Position = Math.Min(mark, stream.Length);
        return new StreamReader(stream).ReadToEnd();
    }

    public static long LogMark() => File.Exists(AppPaths.LogFile) ? new FileInfo(AppPaths.LogFile).Length : 0;
}
