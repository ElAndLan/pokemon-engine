using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Core.Tests.Serialization;

public sealed class OptionsFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cgm-opts-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        GameOptions o = OptionsFile.Load(_dir);
        Assert.Equal(100, o.BgmVolume);
        Assert.Equal(100, o.SfxVolume);
        Assert.Equal(TextSpeed.Medium, o.TextSpeed);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var options = new GameOptions { BgmVolume = 40, SfxVolume = 75, TextSpeed = TextSpeed.Fast };
        OptionsFile.Save(_dir, options);

        GameOptions loaded = OptionsFile.Load(_dir);
        Assert.Equal(40, loaded.BgmVolume);
        Assert.Equal(75, loaded.SfxVolume);
        Assert.Equal(TextSpeed.Fast, loaded.TextSpeed);
    }

    [Fact]
    public void Save_ClampsOutOfRangeVolumes()
    {
        OptionsFile.Save(_dir, new GameOptions { BgmVolume = 150, SfxVolume = -20 });

        GameOptions loaded = OptionsFile.Load(_dir);
        Assert.Equal(100, loaded.BgmVolume);
        Assert.Equal(0, loaded.SfxVolume);
    }

    [Fact]
    public void Load_ClampsHandEditedFile()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, OptionsFile.FileName),
            """{ "bgmVolume": 999, "sfxVolume": -5, "textSpeed": "slow" }""");

        GameOptions loaded = OptionsFile.Load(_dir);
        Assert.Equal(100, loaded.BgmVolume);
        Assert.Equal(0, loaded.SfxVolume);
        Assert.Equal(TextSpeed.Slow, loaded.TextSpeed);
    }

    [Fact]
    public void Normalized_LeavesInRangeValuesUntouched()
    {
        var o = new GameOptions { BgmVolume = 50, SfxVolume = 0, TextSpeed = TextSpeed.Slow }.Normalized();
        Assert.Equal(50, o.BgmVolume);
        Assert.Equal(0, o.SfxVolume);
        Assert.Equal(TextSpeed.Slow, o.TextSpeed);
    }
}
