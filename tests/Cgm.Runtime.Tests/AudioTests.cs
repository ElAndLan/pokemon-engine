using System.Buffers.Binary;
using System.Text;
using Cgm.Runtime.Audio;

namespace Cgm.Runtime.Tests;

public sealed class PcmWaveDecoderTests
{
    [Theory]
    [InlineData(1, 44100)]
    [InlineData(2, 44100)]
    [InlineData(1, 48000)]
    [InlineData(2, 48000)]
    public void Decode_AcceptsRequiredChannelAndRateMatrix(int channels, int sampleRate)
    {
        byte[] pcm = new byte[channels * sizeof(short) * 4];
        DecodedPcmWave wave = PcmWaveDecoder.Decode(Wave(channels, sampleRate, pcm));
        Assert.Equal(channels, wave.Channels);
        Assert.Equal(sampleRate, wave.SampleRate);
        Assert.Equal(4, wave.FrameCount);
        Assert.Equal(pcm, wave.Pcm16);
    }

    [Theory]
    [InlineData("encoding")]
    [InlineData("channels")]
    [InlineData("rate")]
    [InlineData("bits")]
    [InlineData("align")]
    [InlineData("byteRate")]
    [InlineData("riffLength")]
    [InlineData("dataLength")]
    [InlineData("missingFormat")]
    [InlineData("missingData")]
    [InlineData("truncatedChunk")]
    [InlineData("partialFrame")]
    public void Decode_RejectsMalformedCompressedAndUnsupportedInputWithConversionHint(string mutation)
    {
        byte[] bytes = Wave(2, 44100, new byte[8]);
        switch (mutation)
        {
            case "encoding": Write16(bytes, 20, 3); break;
            case "channels": Write16(bytes, 22, 3); break;
            case "rate": Write32(bytes, 24, 22050); break;
            case "bits": Write16(bytes, 34, 8); break;
            case "align": Write16(bytes, 32, 2); break;
            case "byteRate": Write32(bytes, 28, 1); break;
            case "riffLength": Write32(bytes, 4, bytes.Length); break;
            case "dataLength": Write32(bytes, 40, 500); break;
            case "missingFormat": Encoding.ASCII.GetBytes("JUNK").CopyTo(bytes, 12); break;
            case "missingData": Encoding.ASCII.GetBytes("JUNK").CopyTo(bytes, 36); break;
            case "truncatedChunk": bytes = [.. bytes, 1]; Write32(bytes, 4, bytes.Length - 8); break;
            case "partialFrame": bytes = Wave(2, 44100, new byte[6]); break;
        }

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => PcmWaveDecoder.Decode(bytes));
        Assert.Contains("Convert the file", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Decode_SkipsPaddedUnknownChunkWithoutChangingPcm()
    {
        byte[] bytes = Wave(1, 48000, [1, 0, 2, 0], includeOddJunk: true);
        Assert.Equal([1, 0, 2, 0], PcmWaveDecoder.Decode(bytes).Pcm16);
    }

    internal static byte[] Wave(int channels, int sampleRate, byte[] pcm, bool includeOddJunk = false)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8);
        writer.Write(0);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * sizeof(short));
        writer.Write((ushort)(channels * sizeof(short)));
        writer.Write((ushort)16);
        if (includeOddJunk)
        {
            writer.Write("JUNK"u8);
            writer.Write(1);
            writer.Write((byte)9);
            writer.Write((byte)0);
        }
        writer.Write("data"u8);
        writer.Write(pcm.Length);
        writer.Write(pcm);
        if ((pcm.Length & 1) != 0)
            writer.Write((byte)0);
        writer.Flush();
        byte[] bytes = stream.ToArray();
        Write32(bytes, 4, bytes.Length - 8);
        return bytes;
    }

    private static void Write16(byte[] bytes, int offset, int value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), (ushort)value);

    private static void Write32(byte[] bytes, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, 4), value);
}

public sealed class AudioMixerTests
{
    private static readonly DecodedPcmWave Clip = new(1, 44100, new byte[8]);

    [Fact]
    public void OpenAlFactory_LoadsBundledNative_AndMissingDeviceStillReturnsUsableNoAudioAdapter()
    {
        Assert.True(File.Exists(Path.Combine(
            AppContext.BaseDirectory,
            "runtimes",
            "win-x64",
            "native",
            "soft_oal.dll")));
        using IAudioAdapter adapter = OpenAlAudioAdapter.CreateOrNoAudio();
        if (!adapter.Available)
        {
            Assert.Contains("continuing without audio", adapter.Warning, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("native library is unavailable", adapter.Warning, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SameTrackPreservesPlayback_AndNewTrackUsesExactThirtyTickCrossfadeWithFourBuffers()
    {
        var adapter = new RecordingAudioAdapter();
        using var mixer = new AudioMixer(adapter);
        mixer.RequestMusic("a", Clip);
        AudioVoice first = adapter.CreatedMusic.Single();
        Assert.Equal(AudioMixer.MusicBufferCount, adapter.QueuedBufferCounts.Single());
        mixer.RequestMusic("a", Clip);
        Assert.Single(adapter.CreatedMusic);

        mixer.RequestMusic("b", Clip);
        AudioVoice second = adapter.CreatedMusic[1];
        mixer.Tick();
        Assert.Equal(29f / 30f, adapter.Gain(first), 5);
        Assert.Equal(1f / 30f, adapter.Gain(second), 5);
        for (int tick = 1; tick < AudioMixer.CrossfadeTicks; tick++)
            mixer.Tick();

        Assert.Contains(first, adapter.Destroyed);
        Assert.Equal("b", mixer.MusicKey);
        Assert.Equal(1f, adapter.Gain(second));
    }

    [Fact]
    public void StopFadesToSilenceAndVolumeMuteDoNotStopPlaybackPosition()
    {
        var adapter = new RecordingAudioAdapter();
        using var mixer = new AudioMixer(adapter) { MasterVolume = 50, MusicVolume = 40 };
        mixer.RequestMusic("a", Clip);
        AudioVoice voice = adapter.CreatedMusic.Single();
        Assert.Equal(0.2f, adapter.Gain(voice), 5);
        mixer.Muted = true;
        mixer.Tick();
        Assert.Equal(0, adapter.Gain(voice));
        Assert.Equal(1, adapter.TickCount);
        mixer.Muted = false;

        mixer.StopMusic();
        mixer.Tick();
        Assert.Equal(0.2f * 29 / 30, adapter.Gain(voice), 5);
        for (int tick = 1; tick < AudioMixer.CrossfadeTicks; tick++)
            mixer.Tick();
        Assert.Null(mixer.MusicKey);
        Assert.Contains(voice, adapter.Destroyed);
    }

    [Fact]
    public void SixteenSfxVoicesAccept_SeventeenthDrops_AndCompletedVoiceIsReused()
    {
        var adapter = new RecordingAudioAdapter();
        using var mixer = new AudioMixer(adapter);
        for (int i = 0; i < AudioMixer.MaxSfxVoices; i++)
            Assert.True(mixer.PlaySfx(Clip));
        Assert.False(mixer.PlaySfx(Clip));
        Assert.Equal(1, mixer.DroppedSfxRequests);

        AudioVoice completed = adapter.CreatedSfx[3];
        adapter.Completed.Add(completed);
        Assert.True(mixer.PlaySfx(Clip));
        Assert.Equal(completed, adapter.Reused.Single());
        Assert.Equal(AudioMixer.MaxSfxVoices, adapter.CreatedSfx.Count);
    }

    [Fact]
    public void MissingDeviceAndDeviceLossCoalesceWarningAndDisposeVoicesBeforeAdapter()
    {
        using (var missing = new AudioMixer(new NoAudioAdapter("no device")))
        {
            Assert.False(missing.PlaySfx(Clip));
            missing.RequestMusic("a", Clip);
            missing.Tick();
            Assert.Equal("no device", missing.Warning);
        }

        var adapter = new RecordingAudioAdapter();
        var mixer = new AudioMixer(adapter);
        mixer.RequestMusic("a", Clip);
        mixer.PlaySfx(Clip);
        adapter.LoseOnTick = true;
        mixer.Tick();
        Assert.Equal("device lost", mixer.Warning);
        mixer.Dispose();
        Assert.Equal("dispose", adapter.Events[^1]);
        Assert.Equal(2, adapter.Destroyed.Count);
    }

    private sealed class RecordingAudioAdapter : IAudioAdapter
    {
        private readonly Dictionary<AudioVoice, float> _gains = [];
        private int _next;

        public bool Available { get; private set; } = true;
        public string? Warning { get; private set; }
        public bool LoseOnTick { get; set; }
        public int TickCount { get; private set; }
        public List<AudioVoice> CreatedMusic { get; } = [];
        public List<AudioVoice> CreatedSfx { get; } = [];
        public List<AudioVoice> Reused { get; } = [];
        public List<int> QueuedBufferCounts { get; } = [];
        public HashSet<AudioVoice> Completed { get; } = [];
        public List<AudioVoice> Destroyed { get; } = [];
        public List<string> Events { get; } = [];

        public AudioVoice CreateMusic(DecodedPcmWave wave, int queuedBuffers)
        {
            var voice = new AudioVoice(++_next);
            CreatedMusic.Add(voice);
            QueuedBufferCounts.Add(queuedBuffers);
            _gains.Add(voice, 0);
            return voice;
        }

        public AudioVoice PlaySfx(DecodedPcmWave wave, AudioVoice? reuse = null)
        {
            if (reuse is { } voice)
            {
                Completed.Remove(voice);
                Reused.Add(voice);
                return voice;
            }
            var created = new AudioVoice(++_next);
            CreatedSfx.Add(created);
            _gains.Add(created, 0);
            return created;
        }

        public void SetGain(AudioVoice voice, float gain) => _gains[voice] = gain;
        public float Gain(AudioVoice voice) => _gains[voice];
        public bool IsComplete(AudioVoice voice) => Completed.Contains(voice);

        public void Destroy(AudioVoice voice)
        {
            Destroyed.Add(voice);
            Events.Add($"destroy:{voice.Id}");
        }

        public void Tick()
        {
            TickCount++;
            if (LoseOnTick)
            {
                Available = false;
                Warning = "device lost";
            }
        }

        public void Dispose() => Events.Add("dispose");
    }
}
