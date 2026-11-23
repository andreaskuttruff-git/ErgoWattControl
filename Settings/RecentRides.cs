namespace ErgoWattControl;

/// <summary>
/// The files last ridden from the three start dialogs (full paths, newest first, at most <see cref="Count"/> each).
/// The menu offers them under "Kürzlich"; a choice opens the dialog with that file selected.
/// </summary>
public sealed class RecentRides
{
    public const int Count = 3;

    // A hand-edited null in settings.json becomes an empty list.
    public List<string> Routes { get => _routes; set => _routes = value ?? []; }
    public List<string> Recordings { get => _recordings; set => _recordings = value ?? []; }
    public List<string> Programs { get => _programs; set => _programs = value ?? []; }
    private List<string> _routes = [], _recordings = [], _programs = [];

    /// <summary>Puts <paramref name="path"/> first in <paramref name="list"/>: no duplicates, at most <see cref="Count"/> entries.</summary>
    public static void Remember(List<string> list, string path)
    {
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        if (list.Count > Count)
            list.RemoveRange(Count, list.Count - Count);
    }

    /// <summary>The entries of <paramref name="list"/> whose file still exists (deleted or moved ones are left out).</summary>
    public static IReadOnlyList<string> Existing(IEnumerable<string>? list) =>
        list is null ? [] : [.. list.Where(File.Exists).Take(Count)];
}
