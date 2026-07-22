using System.Runtime.InteropServices;
using Silk.NET.OpenAL;

namespace Cgm.Runtime.Audio;

public sealed class OpenAlAudioAdapter : IAudioAdapter
{
    private const int StreamFramesPerBuffer = 4096;

    private readonly AudioContext _context;
    private readonly AL _al;
    private readonly Dictionary<int, Voice> _voices = [];
    private int _nextId;
    private bool _disposed;

    private OpenAlAudioAdapter(AudioContext context, AL al)
    {
        _context = context;
        _al = al;
    }

    public bool Available { get; private set; } = true;
    public string? Warning { get; private set; }

    public static IAudioAdapter CreateOrNoAudio()
    {
        if (!CanLoadNative())
            return new NoAudioAdapter("OpenAL native library is unavailable; continuing without audio.");
        AudioContext? context = null;
        AL? al = null;
        try
        {
            context = new AudioContext();
            al = AL.GetApi(soft: true);
            return new OpenAlAudioAdapter(context, al);
        }
        catch (Exception ex) when (ex is DllNotFoundException or FileNotFoundException
            or BadImageFormatException or AudioDeviceException or AudioContextException
            or NotSupportedException or InvalidOperationException or TypeInitializationException)
        {
            al?.Dispose();
            context?.Dispose();
            return new NoAudioAdapter($"Audio device is unavailable; continuing without audio. {ex.Message}");
        }
    }

    private static bool CanLoadNative()
    {
        string[] names = OperatingSystem.IsWindows()
            ? ["soft_oal.dll", "openal32.dll"]
            : OperatingSystem.IsMacOS()
                ? ["libopenal.dylib", "/System/Library/Frameworks/OpenAL.framework/OpenAL"]
                : ["libopenal.so.1", "libopenal.so"];
        foreach (string name in names)
        {
            foreach (string candidate in new[]
            {
                Path.Combine(AppContext.BaseDirectory, name),
                Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native", name),
                name,
            })
            {
                if (!NativeLibrary.TryLoad(candidate, out nint handle))
                    continue;
                NativeLibrary.Free(handle);
                return true;
            }
        }
        return false;
    }

    public AudioVoice CreateMusic(DecodedPcmWave wave, int queuedBuffers)
    {
        EnsureAvailable();
        if (queuedBuffers != AudioMixer.MusicBufferCount)
            throw new ArgumentOutOfRangeException(nameof(queuedBuffers));
        uint source = _al.GenSource();
        uint[] buffers = _al.GenBuffers(queuedBuffers);
        var voice = new Voice(source, buffers, wave, music: true);
        foreach (uint buffer in buffers)
        {
            FillMusicBuffer(voice, buffer);
            _al.SourceQueueBuffers(source, [buffer]);
        }
        _al.SourcePlay(source);
        return Own(voice);
    }

    public AudioVoice PlaySfx(DecodedPcmWave wave, AudioVoice? reuse = null)
    {
        EnsureAvailable();
        Voice voice;
        if (reuse is { } handle && _voices.TryGetValue(handle.Id, out Voice? existing) && !existing.Music)
        {
            voice = existing;
            _al.SourceStop(voice.Source);
            _al.SetSourceProperty(voice.Source, SourceInteger.Buffer, 0);
            _al.DeleteBuffers(voice.Buffers);
            voice.Buffers = [_al.GenBuffer()];
            voice.Wave = wave;
        }
        else
            voice = new Voice(_al.GenSource(), [_al.GenBuffer()], wave, music: false);

        _al.BufferData(voice.Buffers[0], Format(wave), wave.Pcm16, wave.SampleRate);
        _al.SetSourceProperty(voice.Source, SourceInteger.Buffer, voice.Buffers[0]);
        _al.SourcePlay(voice.Source);
        return reuse is { } reused && _voices.ContainsKey(reused.Id) ? reused : Own(voice);
    }

    public void SetGain(AudioVoice voice, float gain)
    {
        if (!Available || !_voices.TryGetValue(voice.Id, out Voice? owned))
            return;
        _al.SetSourceProperty(owned.Source, SourceFloat.Gain, Math.Clamp(gain, 0, 1));
        CheckError();
    }

    public bool IsComplete(AudioVoice voice)
    {
        if (!Available || !_voices.TryGetValue(voice.Id, out Voice? owned))
            return true;
        if (owned.Music)
            return false;
        _al.GetSourceProperty(owned.Source, GetSourceInteger.SourceState, out int state);
        CheckError();
        return (SourceState)state == SourceState.Stopped;
    }

    public void Destroy(AudioVoice voice)
    {
        if (!_voices.Remove(voice.Id, out Voice? owned))
            return;
        try
        {
            _al.SourceStop(owned.Source);
            if (owned.Music)
            {
                _al.GetSourceProperty(owned.Source, GetSourceInteger.BuffersQueued, out int queued);
                if (queued > 0)
                    _al.SourceUnqueueBuffers(owned.Source, new uint[queued]);
            }
            else
                _al.SetSourceProperty(owned.Source, SourceInteger.Buffer, 0);
            _al.DeleteBuffers(owned.Buffers);
            _al.DeleteSource(owned.Source);
        }
        catch (Exception ex) when (ex is InvalidOperationException or AudioDeviceException)
        {
            Disable(ex.Message);
        }
    }

    public void Tick()
    {
        if (!Available)
            return;
        try
        {
            foreach (Voice voice in _voices.Values.Where(voice => voice.Music).ToArray())
            {
                _al.GetSourceProperty(voice.Source, GetSourceInteger.BuffersProcessed, out int processed);
                if (processed > 0)
                {
                    var buffers = new uint[processed];
                    _al.SourceUnqueueBuffers(voice.Source, buffers);
                    foreach (uint buffer in buffers)
                    {
                        FillMusicBuffer(voice, buffer);
                        _al.SourceQueueBuffers(voice.Source, [buffer]);
                    }
                }
                _al.GetSourceProperty(voice.Source, GetSourceInteger.SourceState, out int state);
                if ((SourceState)state != SourceState.Playing)
                    _al.SourcePlay(voice.Source);
            }
            CheckError();
        }
        catch (Exception ex) when (ex is InvalidOperationException or AudioDeviceException)
        {
            Disable(ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        foreach (int id in _voices.Keys.ToArray())
            Destroy(new AudioVoice(id));
        _al.Dispose();
        _context.Dispose();
        _disposed = true;
        Available = false;
    }

    private AudioVoice Own(Voice voice)
    {
        int id = ++_nextId;
        _voices.Add(id, voice);
        CheckError();
        return new AudioVoice(id);
    }

    private void FillMusicBuffer(Voice voice, uint buffer)
    {
        int blockAlign = voice.Wave.Channels * sizeof(short);
        int length = Math.Min(StreamFramesPerBuffer * blockAlign, voice.Wave.Pcm16.Length);
        length -= length % blockAlign;
        var chunk = new byte[length];
        int written = 0;
        while (written < chunk.Length)
        {
            int copy = Math.Min(chunk.Length - written, voice.Wave.Pcm16.Length - voice.Cursor);
            voice.Wave.Pcm16.AsSpan(voice.Cursor, copy).CopyTo(chunk.AsSpan(written));
            written += copy;
            voice.Cursor = (voice.Cursor + copy) % voice.Wave.Pcm16.Length;
        }
        _al.BufferData(buffer, Format(voice.Wave), chunk, voice.Wave.SampleRate);
    }

    private void CheckError()
    {
        AudioError error = _al.GetError();
        if (error != AudioError.NoError)
            Disable($"OpenAL device error: {error}.");
    }

    private void EnsureAvailable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Available)
            throw new InvalidOperationException(Warning ?? "Audio device is unavailable.");
    }

    private void Disable(string detail)
    {
        Available = false;
        Warning ??= $"Audio device was lost; continuing without audio. {detail}";
    }

    private static BufferFormat Format(DecodedPcmWave wave) =>
        wave.Channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16;

    private sealed class Voice(uint source, uint[] buffers, DecodedPcmWave wave, bool music)
    {
        public uint Source { get; } = source;
        public uint[] Buffers { get; set; } = buffers;
        public DecodedPcmWave Wave { get; set; } = wave;
        public bool Music { get; } = music;
        public int Cursor { get; set; }
    }
}
