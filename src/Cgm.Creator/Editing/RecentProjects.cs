using System.Text.Json;

namespace Cgm.Creator.Editing;

/// <summary>
/// The recent-projects list (CREATOR_APP_SPEC §10.6): up to ten canonical absolute folders,
/// newest first, deduplicated case-insensitively. Missing folders stay listed — the UI shows them
/// as missing with a remove action. A malformed file reads as empty, never as an error.
/// </summary>
public sealed class RecentProjects
{
    public const int Capacity = 10;

    private readonly string _path;
    private readonly List<string> _folders;

    public RecentProjects(string baseDir)
    {
        _path = Path.Combine(baseDir, "recent.json");
        _folders = Read(_path);
    }

    /// <summary>The production store under %APPDATA%/CreatureGameMaker.</summary>
    public static RecentProjects Default() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CreatureGameMaker"));

    public IReadOnlyList<string> Folders => _folders;

    /// <summary>The starting folder for file dialogs: the most recent project's parent.</summary>
    public string? LastFolder => _folders.Count > 0 ? _folders[0] : null;

    public void Add(string folder)
    {
        string canonical = Path.GetFullPath(folder);
        _folders.RemoveAll(f => string.Equals(f, canonical, StringComparison.OrdinalIgnoreCase));
        _folders.Insert(0, canonical);
        if (_folders.Count > Capacity)
            _folders.RemoveRange(Capacity, _folders.Count - Capacity);
        Write();
    }

    public void Remove(string folder)
    {
        _folders.RemoveAll(f => string.Equals(f, Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase));
        Write();
    }

    private static List<string> Read(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return []; // absent or malformed: empty list, not an error
        }
    }

    private void Write()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_folders));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort convenience state: two Creator instances may race on this file, and a
            // failed write must never break opening a project. The in-memory list stays correct.
        }
    }
}
