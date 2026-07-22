using System.Diagnostics;
using System.Text.Json;
using Cgm.Creator.Editing;

namespace Cgm.Creator.Tests;

/// <summary>CREATOR_APP_SPEC §10.3: one writer per project across processes, reentrant within a
/// process, stale locks removed only after confirming the process is gone.</summary>
public sealed class ProjectLockTests : IDisposable
{
    private readonly string _dir = TestRepo.CopySampleToTemp("fixture-min");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string LockPath => Path.Combine(_dir, ".cgm", "lock.json");

    [Fact]
    public void Acquire_WritesLock_Release_RemovesIt()
    {
        ProjectLock.Acquire(_dir);
        Assert.True(File.Exists(LockPath));
        ProjectLock.Release(_dir);
        Assert.False(File.Exists(LockPath));
    }

    [Fact]
    public void Acquire_IsReentrantForTheSameProcess()
    {
        ProjectLock.Acquire(_dir);
        ProjectLock.Acquire(_dir); // reopening after save is not a second writer
        Assert.True(File.Exists(LockPath));
        ProjectLock.Release(_dir);
    }

    [Fact]
    public void Acquire_RefusesWhenAnotherLiveProcessHoldsIt()
    {
        // A real other process whose PID and start time are both live and correct: this test run's
        // parent won't do (unknowable), so start a short-lived cmd that outlives the assertion.
        using Process other = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 3 127.0.0.1")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        })!;
        try
        {
            Directory.CreateDirectory(Path.Combine(_dir, ".cgm"));
            File.WriteAllText(LockPath, JsonSerializer.Serialize(new
            {
                Pid = other.Id,
                ProcessStartUtc = other.StartTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"),
            }));

            var ex = Assert.Throws<InvalidOperationException>(() => ProjectLock.Acquire(_dir));
            Assert.Contains(other.Id.ToString(), ex.Message);
        }
        finally
        {
            other.Kill();
            other.WaitForExit();
        }
    }

    [Fact]
    public void Acquire_ReplacesAStaleLockFromADeadProcess()
    {
        // Long-lived enough to read StartTime reliably, then killed so the PID is dead by the
        // time the lock is checked.
        using Process dead = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 10 127.0.0.1")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        })!;
        string stamp = dead.StartTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss");
        dead.Kill();
        dead.WaitForExit();

        Directory.CreateDirectory(Path.Combine(_dir, ".cgm"));
        File.WriteAllText(LockPath, JsonSerializer.Serialize(new { Pid = dead.Id, ProcessStartUtc = stamp }));

        ProjectLock.Acquire(_dir); // stale: process absent → removed and re-acquired
        Assert.True(File.Exists(LockPath));
        ProjectLock.Release(_dir);
    }

    [Fact]
    public void Acquire_TreatsACorruptLockAsStale()
    {
        Directory.CreateDirectory(Path.Combine(_dir, ".cgm"));
        File.WriteAllText(LockPath, "not json");
        ProjectLock.Acquire(_dir);
        ProjectLock.Release(_dir);
        Assert.False(File.Exists(LockPath));
    }

    [Fact]
    public void Session_OpenAcquires_CloseReleases()
    {
        var session = ProjectSession.Open(_dir);
        Assert.True(File.Exists(LockPath));
        session.Close();
        Assert.False(File.Exists(LockPath));
    }
}
