using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Runtime.Engine;

public enum SaveLoadStatus
{
    /// <summary>Loaded and validated.</summary>
    Ok,

    /// <summary>No save exists yet; Continue should be unavailable.</summary>
    Missing,

    /// <summary>Unreadable or malformed, and no usable backup exists.</summary>
    Corrupt,

    /// <summary>Unreadable, but a backup is present and can be offered to the player.</summary>
    CorruptWithBackup,

    /// <summary>Written by a newer runtime than this one understands.</summary>
    NewerFormat,

    /// <summary>Written against different content, so entity IDs may no longer resolve.</summary>
    ContentMismatch,
}

public sealed record SaveLoadResult(SaveLoadStatus Status, SaveFile? Save, string? Message)
{
    public bool Succeeded => Status == SaveLoadStatus.Ok;
}

/// <summary>Durable single-slot save storage (ENGINE_RUNTIME_SPEC 16A/16E). Writes are
/// temp-file → flush → replace, retaining the previous file as <c>.bak</c>. Load tries the primary
/// only; corruption offers the validated backup rather than silently replacing state, because
/// quietly rolling a player back to an older save is worse than telling them.</summary>
public sealed class SaveRepository
{
    public const string FileName = "save.json";
    public const string BackupExtension = ".bak";
    public const string TempExtension = ".tmp";

    private readonly string _folder;

    public SaveRepository(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        _folder = folder;
    }

    public string Path => System.IO.Path.Combine(_folder, FileName);

    public string BackupPath => Path + BackupExtension;

    public bool Exists => File.Exists(Path);

    public bool BackupExists => File.Exists(BackupPath);

    /// <summary>Writes the slot durably. The temp file is flushed to disk before it replaces the
    /// primary, so a crash mid-write leaves the previous save intact rather than a truncated one.</summary>
    public void Write(SaveFile save)
    {
        ArgumentNullException.ThrowIfNull(save);
        Directory.CreateDirectory(_folder);

        string temp = Path + TempExtension;
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(CgmJson.Serialize(save));
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        if (Exists)
            File.Replace(temp, Path, BackupPath, ignoreMetadataErrors: true);
        else
            File.Move(temp, Path);   // first save: nothing to back up yet
    }

    /// <summary>Loads the primary slot. A corrupt primary never falls back silently — the caller is
    /// told a backup exists and decides.</summary>
    public SaveLoadResult Load(string contentHash) => Read(Path, contentHash, allowBackupOffer: true);

    /// <summary>Loads the backup, after the player accepted the offer.</summary>
    public SaveLoadResult LoadBackup(string contentHash) => Read(BackupPath, contentHash, allowBackupOffer: false);

    private SaveLoadResult Read(string path, string contentHash, bool allowBackupOffer)
    {
        if (!File.Exists(path))
            return new SaveLoadResult(SaveLoadStatus.Missing, null, "No save file.");

        SaveFile? save;
        try
        {
            save = CgmJson.Deserialize<SaveFile>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or System.Text.Json.JsonException or InvalidDataException)
        {
            return Damaged($"Save file is unreadable ({ex.GetType().Name}).", allowBackupOffer);
        }

        if (save is null)
            return Damaged("Save file is empty.", allowBackupOffer);
        if (save.SaveFormatVersion > CurrentFormatVersion)
            return new SaveLoadResult(SaveLoadStatus.NewerFormat, null,
                $"Save format {save.SaveFormatVersion} is newer than {CurrentFormatVersion}.");
        if (save.Map is null)
            return Damaged("Save file has no map.", allowBackupOffer);
        if (!string.IsNullOrEmpty(contentHash) && !string.IsNullOrEmpty(save.GameContentHash)
            && !string.Equals(save.GameContentHash, contentHash, StringComparison.Ordinal))
            return new SaveLoadResult(SaveLoadStatus.ContentMismatch, save,
                "Save was written against different game content.");

        return new SaveLoadResult(SaveLoadStatus.Ok, save, null);
    }

    private SaveLoadResult Damaged(string message, bool allowBackupOffer) =>
        allowBackupOffer && BackupExists
            ? new SaveLoadResult(SaveLoadStatus.CorruptWithBackup, null, message + " A backup is available.")
            : new SaveLoadResult(SaveLoadStatus.Corrupt, null, message);

    /// <summary>The newest save format this runtime writes and understands.</summary>
    public static int CurrentFormatVersion => new SaveFile().SaveFormatVersion;
}
