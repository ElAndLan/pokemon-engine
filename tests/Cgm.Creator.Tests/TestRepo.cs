namespace Cgm.Creator.Tests;

/// <summary>Repo-relative paths + a helper to copy a sample project into a scratch folder.</summary>
internal static class TestRepo
{
    public static string Root { get; } = FindRoot();

    public static string Sample(string relative) => Path.Combine(Root, "samples", relative);

    /// <summary>Copies a sample project into a fresh temp folder; caller deletes it.</summary>
    public static string CopySampleToTemp(string sampleName)
    {
        string dest = Path.Combine(Path.GetTempPath(), "cgm-sess-" + Guid.NewGuid().ToString("N"));
        CopyDir(Sample(sampleName), dest);
        return dest;
    }

    private static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(from, to));
        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(from, to), overwrite: true);
    }

    private static string FindRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CreatureGameMaker.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Could not locate repo root.");
    }
}
