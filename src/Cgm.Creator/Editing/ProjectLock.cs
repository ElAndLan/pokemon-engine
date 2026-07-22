using System.Diagnostics;
using System.Text.Json;

namespace Cgm.Creator.Editing;

/// <summary>
/// Single-writer guard for a project folder (CREATOR_APP_SPEC §10.3): <c>.cgm/lock.json</c> holds
/// the owning PID and its process start time, so a reused PID cannot masquerade as the holder. A
/// lock whose process is gone (or whose start time mismatches) is stale and silently replaced.
/// The same process may re-acquire its own lock — reopening after save is not a second writer.
/// </summary>
public static class ProjectLock
{
    private sealed record LockInfo(int Pid, string ProcessStartUtc);

    private static string LockPath(string folder) => Path.Combine(folder, ".cgm", "lock.json");

    /// <summary>Acquires the lock, throwing <see cref="InvalidOperationException"/> naming the
    /// holding PID when another live process owns it.</summary>
    public static void Acquire(string folder)
    {
        if (ReadHolder(folder) is { } holder && holder.Pid != Environment.ProcessId)
            throw new InvalidOperationException(
                $"Project is open in another Creator (PID {holder.Pid}). Close it first.");

        using Process self = Process.GetCurrentProcess();
        Directory.CreateDirectory(Path.Combine(folder, ".cgm"));
        File.WriteAllText(LockPath(folder), JsonSerializer.Serialize(
            new LockInfo(Environment.ProcessId, StartStamp(self))));
    }

    /// <summary>Releases only our own lock; another process's lock is never deleted here.</summary>
    public static void Release(string folder)
    {
        if (ReadHolder(folder) is { Pid: var pid } && pid == Environment.ProcessId)
            File.Delete(LockPath(folder));
    }

    /// <summary>The live holder, or null when the lock is absent, unreadable, or stale (process
    /// gone or start time mismatched — PID reuse). Stale locks are removed after that check.</summary>
    private static LockInfo? ReadHolder(string folder)
    {
        LockInfo? info = null;
        try
        {
            info = JsonSerializer.Deserialize<LockInfo>(File.ReadAllText(LockPath(folder)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Missing or corrupt lock file: treat as unheld; a corrupt one is stale by definition.
        }
        if (info is null)
            return null;

        try
        {
            using Process process = Process.GetProcessById(info.Pid);
            if (StartStamp(process) == info.ProcessStartUtc)
                return info;
        }
        catch (ArgumentException)
        {
            // No such process — stale.
        }
        catch (InvalidOperationException)
        {
            // Process exited between lookup and StartTime read — stale.
        }

        File.Delete(LockPath(folder));
        return null;
    }

    /// <summary>Whole-second resolution: StartTime precision varies between reads of the same
    /// process, so sub-second bits would false-negative the identity check.</summary>
    private static string StartStamp(Process process) =>
        process.StartTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss");
}
