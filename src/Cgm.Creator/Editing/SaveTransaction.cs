using System.Text.Json;

namespace Cgm.Creator.Editing;

/// <summary>
/// The atomic project write (CREATOR_APP_SPEC §10.2): stage every file, journal the swap, back up
/// originals, replace in canonical order, and roll back completely on any failure — including a
/// failure that killed the process, via <see cref="RollbackIfUnfinished"/> at the next open. The
/// project on disk is only ever the pre-save state or the post-save state, never a mix.
/// </summary>
public static class SaveTransaction
{
    private sealed record Journal(int SchemaVersion, string StartedUtc, List<Entry> Entries);
    private sealed record Entry(string Path, string Action, bool HadOriginal);

    private static string WorkDir(string folder) => System.IO.Path.Combine(folder, ".cgm");
    private static string JournalPath(string folder) => System.IO.Path.Combine(WorkDir(folder), "save-journal.json");
    private static string StagingDir(string folder) => System.IO.Path.Combine(WorkDir(folder), "staging");
    private static string BackupDir(string folder) => System.IO.Path.Combine(WorkDir(folder), "backup");

    /// <summary>Writes the given relative-path contents as one transaction. A null content deletes
    /// the file. Throws on failure with the project rolled back to its pre-save state.</summary>
    public static void Run(string folder, IReadOnlyList<(string RelPath, string? Content)> writes)
        => RunBytes(folder, writes.Select(w =>
            (w.RelPath, w.Content is null ? null : System.Text.Encoding.UTF8.GetBytes(w.Content))).ToList());

    /// <summary>Binary-capable form used by project Save so imported images/audio and their JSON
    /// metadata cross the same journal boundary.</summary>
    public static void RunBytes(string folder, IReadOnlyList<(string RelPath, byte[]? Content)> writes)
    {
        if (writes.Count == 0)
            return;

        // 1. Stage: all serialization/write work happens before any source file is touched.
        Directory.CreateDirectory(StagingDir(folder));
        foreach ((string relPath, byte[]? content) in writes)
        {
            if (content is null)
                continue;
            string staged = System.IO.Path.Combine(StagingDir(folder), relPath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(staged)!);
            File.WriteAllBytes(staged, content);
        }

        // 2. Journal: canonical relative-path order, recording whether each target existed.
        var entries = writes
            .OrderBy(w => w.RelPath, StringComparer.Ordinal)
            .Select(w => new Entry(w.RelPath, w.Content is null ? "delete" : "replace",
                File.Exists(System.IO.Path.Combine(folder, w.RelPath))))
            .ToList();
        var journal = new Journal(1, DateTime.UtcNow.ToString("O"), entries);
        File.WriteAllText(JournalPath(folder), JsonSerializer.Serialize(journal));

        // 3. Backup + swap, in journal order. Any failure rolls everything back.
        try
        {
            foreach (Entry entry in entries)
            {
                string target = System.IO.Path.Combine(folder, entry.Path);
                if (entry.HadOriginal)
                {
                    string backup = System.IO.Path.Combine(BackupDir(folder), entry.Path);
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(backup)!);
                    File.Move(target, backup);
                }
                if (entry.Action == "replace")
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
                    File.Move(System.IO.Path.Combine(StagingDir(folder), entry.Path), target);
                }
            }
        }
        catch
        {
            Rollback(folder, journal);
            throw;
        }

        // 4. Commit: the journal's absence is the commit marker, so it goes first.
        File.Delete(JournalPath(folder));
        DeleteWorkDirs(folder);
    }

    /// <summary>Completes the rollback of a save the previous process never finished. Called at
    /// project open, before loading. Returns true when an interrupted save was rolled back.</summary>
    public static bool RollbackIfUnfinished(string folder)
    {
        if (!File.Exists(JournalPath(folder)))
        {
            DeleteWorkDirs(folder); // a crash between commit steps can orphan the dirs
            return false;
        }

        var journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(JournalPath(folder)))!;
        Rollback(folder, journal);
        return true;
    }

    /// <summary>Restores every journaled file to its pre-save state: backed-up originals return,
    /// files that did not exist before the save are removed. Idempotent — entries not yet swapped
    /// (no backup, unchanged target) are left alone.</summary>
    private static void Rollback(string folder, Journal journal)
    {
        foreach (Entry entry in journal.Entries)
        {
            string target = System.IO.Path.Combine(folder, entry.Path);
            string backup = System.IO.Path.Combine(BackupDir(folder), entry.Path);
            if (entry.HadOriginal)
            {
                if (File.Exists(backup))
                {
                    File.Delete(target); // the staged replacement, if the swap got that far
                    File.Move(backup, target);
                }
            }
            else if (File.Exists(target))
            {
                File.Delete(target); // did not exist before the save
            }
        }

        File.Delete(JournalPath(folder));
        DeleteWorkDirs(folder);
    }

    private static void DeleteWorkDirs(string folder)
    {
        if (Directory.Exists(StagingDir(folder)))
            Directory.Delete(StagingDir(folder), recursive: true);
        if (Directory.Exists(BackupDir(folder)))
            Directory.Delete(BackupDir(folder), recursive: true);
    }
}
