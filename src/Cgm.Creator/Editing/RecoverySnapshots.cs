using System.Security.Cryptography;
using System.Text;
using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Creator.Editing;

/// <summary>
/// Crash-recovery snapshots (CREATOR_APP_SPEC §10.4): the full in-memory project written to
/// app-data — never to project source. Snapshots surviving to the next open mean the last session
/// ended unclean; a clean close deletes them. Each snapshot uses the project folder layout, so
/// applying one is an ordinary <see cref="ProjectLoader"/> load.
/// </summary>
public sealed class RecoverySnapshots(string baseDir)
{
    public const int Keep = 5;

    /// <summary>The production store under %APPDATA%/CreatureGameMaker/recovery.</summary>
    public static RecoverySnapshots Default() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CreatureGameMaker", "recovery"));

    /// <summary>Snapshot directories for a project, newest first.</summary>
    public IReadOnlyList<string> For(string projectFolder)
    {
        string dir = ProjectDir(projectFolder);
        if (!Directory.Exists(dir))
            return [];
        return Directory.EnumerateDirectories(dir)
            .Where(d => !d.EndsWith(".tmp", StringComparison.Ordinal)) // torn writes are never offered
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Writes one snapshot of the session's current in-memory state and prunes to the
    /// newest <see cref="Keep"/>. Written to a .tmp directory first so a torn snapshot is never
    /// mistaken for a complete one.</summary>
    public void Write(ProjectSession session)
    {
        string final = Path.Combine(ProjectDir(session.Folder), DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        while (Directory.Exists(final))
            final += "0"; // same-millisecond writes stay distinct and ordinally newer
        string tmp = final + ".tmp";

        Directory.CreateDirectory(tmp);
        File.WriteAllText(Path.Combine(tmp, ProjectFile.FileName), CgmJson.Serialize(session.Settings));
        foreach (IEntity entity in session.Snapshot().Entities)
        {
            string path = Path.Combine(tmp, "data", entity.Id.Prefix, entity.Id.Slug + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, CgmJson.SerializeEntity(entity));
        }
        Directory.Move(tmp, final);

        foreach (string old in For(session.Folder).Skip(Keep))
            Directory.Delete(old, recursive: true);
    }

    /// <summary>A clean close (saved or explicitly discarded) removes the project's snapshots.</summary>
    public void Discard(string projectFolder)
    {
        string dir = ProjectDir(projectFolder);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    private string ProjectDir(string projectFolder) => Path.Combine(baseDir, KeyFor(projectFolder));

    /// <summary>Filesystem-safe stable key for a project path (§10.4).</summary>
    private static string KeyFor(string projectFolder) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(projectFolder).ToUpperInvariant())))[..16];
}
