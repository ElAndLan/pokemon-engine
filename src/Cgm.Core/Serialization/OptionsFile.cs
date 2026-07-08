using Cgm.Core.Model;

namespace Cgm.Core.Serialization;

/// <summary>
/// Reads/writes <c>options.json</c> in a save directory (Phase 13). Missing file → defaults (first
/// run), and values are normalized on both load and save so an absent or hand-edited file never
/// yields out-of-range volumes. Mirrors <see cref="ProjectFile"/>; serialization stays disk-free in CgmJson.
/// </summary>
public static class OptionsFile
{
    public const string FileName = "options.json";

    public static GameOptions Load(string saveDir)
    {
        string path = Path.Combine(saveDir, FileName);
        if (!File.Exists(path))
            return new GameOptions();
        return CgmJson.DeserializeVersioned<GameOptions>(File.ReadAllText(path)).Normalized();
    }

    public static void Save(string saveDir, GameOptions options)
    {
        Directory.CreateDirectory(saveDir);
        File.WriteAllText(Path.Combine(saveDir, FileName), CgmJson.Serialize(options.Normalized()));
    }
}
