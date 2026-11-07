namespace ErgoWattControl;

/// <summary>
/// The training programs ship inside the exe. At start they are written to the Programme folder wherever
/// a file is missing or differs from the shipped one (a changed file is overwritten). Other files there are left alone.
/// The rider's own programs belong in <see cref="CustomFolder"/>: the app never overwrites anything there except the
/// schema file and the CLAUDE.md / AGENTS.md, and puts an example in once, when it creates the folder.
/// </summary>
public static class ProgramFiles
{
    private const string ResourcePrefix = "Programme/";
    private const string SchemaPrefix = "Schema/";

    /// <summary>The JSON schema (like an XSD for XML) that describes a program file; editors validate against it.</summary>
    public const string SchemaFileName = "Trainingsprogramm.schema.json";

    /// <summary>Documents\ErgoWattControl\Programme, or next to the exe in portable mode.</summary>
    public static string Folder => Path.Combine(AppPaths.DataFolder, "Programme");

    /// <summary>The rider's own programs: Documents\ErgoWattControl\CustomPrograms, or next to the exe in portable mode.</summary>
    public static string CustomFolder => Path.Combine(AppPaths.DataFolder, "CustomPrograms");

    /// <summary>
    /// Program files of a folder: every *.json except the schema. A missing or unreadable folder simply has none
    /// (it may have been deleted or locked while the app runs).
    /// </summary>
    public static IEnumerable<string> ProgramsIn(string folder)
    {
        try
        {
            return Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*.json").Where(f => !f.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase)).ToList()
                : [];
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            Log(folder, ex);
            return [];
        }
    }

    /// <returns>Number of files written; failures are noted in Fehler.log and don't stop the start.</returns>
    public static int Install()
    {
        var assembly = typeof(ProgramFiles).Assembly;
        var written = 0;
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            if (WriteIfChanged(resource, Path.Combine(Folder, resource[ResourcePrefix.Length..])))
                written++;
        }
        return written + EnsureCustomFolder(CustomFolder);
    }

    /// <summary>
    /// Creates the folder for own programs if it is missing – at start and again whenever the programs dialog opens,
    /// so deleting it while the app runs does no harm – with the schema and the AI notes (kept current) and, only
    /// when the folder is new, an example to copy from. Never throws: if the folder can't be created (no rights, a
    /// file of that name, …) that is noted in Fehler.log and the dialog lists just the shipped programs.
    /// </summary>
    /// <returns>Number of files written.</returns>
    public static int EnsureCustomFolder(string folder)
    {
        bool newFolder;
        try
        {
            newFolder = !Directory.Exists(folder);
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            Log(folder, ex);
            return 0;
        }
        var written = 0;
        if (WriteIfChanged(SchemaPrefix + SchemaFileName, Path.Combine(folder, SchemaFileName)))
            written++;
        // Instructions for AI assistants on how to write a program, from one source: CLAUDE.md is read by Claude Code,
        // AGENTS.md by OpenAI Codex and other assistants (GitHub Copilot, Cursor, …).
        foreach (var name in new[] { "CLAUDE.md", "AGENTS.md" })
        {
            if (WriteIfChanged(SchemaPrefix + "CLAUDE.md", Path.Combine(folder, name)))
                written++;
        }
        if (newFolder && WriteIfChanged(SchemaPrefix + "Beispiel_Eigenes_Programm.json", Path.Combine(folder, "Beispiel_Eigenes_Programm.json")))
            written++;
        return written;
    }

    private static bool IsFileError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.Security.SecurityException;

    /// <summary>Fehler.log; <see cref="AppPaths.Log"/> itself never throws.</summary>
    private static void Log(string path, Exception ex) => AppPaths.Log(path, ex);

    private static bool WriteIfChanged(string resource, string path)
    {
        try
        {
            using var stream = typeof(ProgramFiles).Assembly.GetManifestResourceStream(resource);
            if (stream is null)
                return false;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var shipped = buffer.ToArray();
            if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(shipped))
                return false;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, shipped);
            return true;
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            Log(path, ex);
            return false;
        }
    }
}
