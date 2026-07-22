using Cgm.Runtime.Audio;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>The audio system's glue to the engine's <see cref="IAssetSource"/>: it loads and decodes
/// sound by project path, follows the active track, and treats a missing device or missing file as
/// silence with a single warning — never a failure.</summary>
public sealed class RuntimeAudioSystemTests
{
    private const string MusicKey = "audio/theme.wav";
    private const string SfxKey = "audio/hit.wav";

    private static byte[] Wav() => PcmWaveDecoderTests.Wave(1, 44100, new byte[8]);

    private static IAssetSource Assets(params string[] keys) =>
        new PackAssetSource(keys.ToDictionary(k => k, _ => Wav()));

    // --- Missing device: silent, one warning, still runnable --------------------------

    [Fact]
    public void WithNoDevice_MusicAndSfxAreSilentButDoNotThrow()
    {
        using var audio = new RuntimeAudioSystem(Assets(MusicKey, SfxKey), new NoAudioAdapter("no device"), 100, 100);

        audio.SyncMusic(MusicKey);   // decodes the file, but the no-audio adapter plays nothing
        audio.Tick();
        Assert.False(audio.PlaySfx(SfxKey));
        Assert.Equal("no device", audio.Warning);
    }

    // --- Missing asset: warn once, keep going -----------------------------------------

    [Fact]
    public void AMissingMusicFileWarnsAndPlaysNothing()
    {
        var adapter = new CountingAdapter();
        using var audio = new RuntimeAudioSystem(Assets(), adapter, 100, 100);   // no files at all

        audio.SyncMusic("audio/absent.wav");
        Assert.Equal(0, adapter.MusicCreated);
        Assert.Contains("missing", audio.Warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AMissingSfxFileReturnsFalseAndWarns()
    {
        var adapter = new CountingAdapter();
        using var audio = new RuntimeAudioSystem(Assets(), adapter, 100, 100);

        Assert.False(audio.PlaySfx("audio/absent.wav"));
        Assert.Contains("missing", audio.Warning, StringComparison.OrdinalIgnoreCase);
    }

    // --- Present asset: decoded and handed to the adapter -----------------------------

    [Fact]
    public void PresentMusicIsDecodedAndStarted()
    {
        var adapter = new CountingAdapter();
        using var audio = new RuntimeAudioSystem(Assets(MusicKey), adapter, 100, 100);

        audio.SyncMusic(MusicKey);
        Assert.Equal(1, adapter.MusicCreated);
    }

    [Fact]
    public void PresentSfxIsDecodedAndPlayed()
    {
        var adapter = new CountingAdapter();
        using var audio = new RuntimeAudioSystem(Assets(SfxKey), adapter, 100, 100);

        Assert.True(audio.PlaySfx(SfxKey));
        Assert.Equal(1, adapter.SfxPlayed);
    }

    // --- Track following --------------------------------------------------------------

    /// <summary>The same track requested repeatedly is decoded and started once; a null key stops it.</summary>
    [Fact]
    public void SyncingTheSameTrackDoesNotRestartItAndNullStops()
    {
        var adapter = new CountingAdapter();
        using var audio = new RuntimeAudioSystem(Assets(MusicKey), adapter, 100, 100);

        audio.SyncMusic(MusicKey);
        audio.SyncMusic(MusicKey);   // unchanged: no reload
        Assert.Equal(1, adapter.MusicCreated);

        audio.SyncMusic(null);       // leaving the map stops the music
        for (int i = 0; i < AudioMixer.CrossfadeTicks; i++)
            audio.Tick();
        Assert.Null(audio.Diagnostics.MusicKey);
    }

    /// <summary>An adapter that records whether it was asked to start music or play a sound, so the
    /// system's decode-and-dispatch can be asserted without a real device.</summary>
    private sealed class CountingAdapter : IAudioAdapter
    {
        private int _next;
        public int MusicCreated { get; private set; }
        public int SfxPlayed { get; private set; }

        public bool Available => true;
        public string? Warning => null;

        public AudioVoice CreateMusic(DecodedPcmWave wave, int queuedBuffers)
        {
            MusicCreated++;
            return new AudioVoice(++_next);
        }

        public AudioVoice PlaySfx(DecodedPcmWave wave, AudioVoice? reuse = null)
        {
            SfxPlayed++;
            return reuse ?? new AudioVoice(++_next);
        }

        public void SetGain(AudioVoice voice, float gain) { }
        public bool IsComplete(AudioVoice voice) => false;
        public void Destroy(AudioVoice voice) { }
        public void Tick() { }
        public void Dispose() { }
    }
}
