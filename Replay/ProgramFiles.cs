namespace ErgoWattControl;

/// <summary>
/// The training programs ship inside the exe. At start they are written to the Programme folder wherever
/// a file is missing or differs from the shipped one (a changed file is overwritten). Other files there, such as the
/// rider's own programs, are left alone.
/// </summary>
public static class ProgramFiles
{
    private const string ResourcePrefix = "Programme/";

    /// <summary>Documents\ErgoWattControl\Programme, or next to the exe in portable mode.</summary>
    public static string Folder => Path.Combine(AppPaths.DataFolder, "Programme");

    /// <returns>Number of files written; failures are noted in Fehler.log and don't stop the start.</returns>
    public static int Install()
    {
        var assembly = typeof(ProgramFiles).Assembly;
        var written = 0;
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            var path = Path.Combine(Folder, resource[ResourcePrefix.Length..]);
            try
            {
                using var stream = assembly.GetManifestResourceStream(resource)!;
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                var shipped = buffer.ToArray();
                if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(shipped))
                    continue;
                Directory.CreateDirectory(Folder);
                File.WriteAllBytes(path, shipped);
                written++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AppPaths.Log(path, ex);
            }
        }
        return written;
    }
}
