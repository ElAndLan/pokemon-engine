namespace Cgm.Core.Model;

public enum TextSpeed { Slow, Medium, Fast }

/// <summary>
/// Player options (Phase 13 Options menu), persisted per save directory. Volumes are 0–100 slider
/// values; <see cref="Normalized"/> clamps them (deserialized/hand-edited files can be out of range).
/// </summary>
public sealed record GameOptions
{
    public int SchemaVersion { get; init; } = SchemaVersions.Current;
    public int BgmVolume { get; init; } = 100;
    public int SfxVolume { get; init; } = 100;
    public TextSpeed TextSpeed { get; init; } = TextSpeed.Medium;

    /// <summary>A copy with volumes clamped to 0–100.</summary>
    public GameOptions Normalized() => this with
    {
        BgmVolume = Math.Clamp(BgmVolume, 0, 100),
        SfxVolume = Math.Clamp(SfxVolume, 0, 100),
    };
}
