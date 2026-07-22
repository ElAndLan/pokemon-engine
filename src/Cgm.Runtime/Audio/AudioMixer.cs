using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Audio;

public readonly record struct AudioVoice(int Id);

public interface IAudioAdapter : IDisposable
{
    bool Available { get; }
    string? Warning { get; }
    AudioVoice CreateMusic(DecodedPcmWave wave, int queuedBuffers);
    AudioVoice PlaySfx(DecodedPcmWave wave, AudioVoice? reuse = null);
    void SetGain(AudioVoice voice, float gain);
    bool IsComplete(AudioVoice voice);
    void Destroy(AudioVoice voice);
    void Tick();
}

public sealed record AudioMixerDiagnostics(
    string? MusicKey,
    int ActiveSfxVoices,
    int DroppedSfxRequests,
    string? Warning);

public sealed class AudioMixer : IDisposable
{
    public const int CrossfadeTicks = 30;
    public const int MusicBufferCount = 4;
    public const int MaxSfxVoices = 16;

    private readonly IAudioAdapter _adapter;
    private readonly List<AudioVoice> _sfx = [];
    private AudioVoice? _music;
    private AudioVoice? _incoming;
    private string? _musicKey;
    private string? _incomingKey;
    private int _fadeTick;
    private bool _stopping;
    private bool _warned;
    private string? _warning;
    private bool _disposed;
    private int _masterVolume = 100;
    private int _musicVolume = 100;
    private int _sfxVolume = 100;
    private bool _muted;

    public AudioMixer(IAudioAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        CaptureWarning();
    }

    public string? MusicKey => _incomingKey ?? _musicKey;
    public int MasterVolume { get => _masterVolume; set { _masterVolume = Volume(value); ApplyGains(); } }
    public int MusicVolume { get => _musicVolume; set { _musicVolume = Volume(value); ApplyGains(); } }
    public int SfxVolume { get => _sfxVolume; set { _sfxVolume = Volume(value); ApplyGains(); } }
    public bool Muted { get => _muted; set { _muted = value; ApplyGains(); } }
    public int DroppedSfxRequests { get; private set; }
    public string? Warning => _warning;

    public void RequestMusic(string key, DecodedPcmWave wave)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(wave);
        if (!_adapter.Available)
        {
            CaptureWarning();
            return;
        }
        if (string.Equals(key, _incomingKey, StringComparison.Ordinal)
            || _incoming is null && string.Equals(key, _musicKey, StringComparison.Ordinal))
            return;

        if (_music is null)
        {
            _music = _adapter.CreateMusic(wave, MusicBufferCount);
            _musicKey = key;
            _stopping = false;
            _fadeTick = 0;
            ApplyGains();
            return;
        }

        if (_incoming is { } superseded)
            _adapter.Destroy(superseded);
        _incoming = _adapter.CreateMusic(wave, MusicBufferCount);
        _incomingKey = key;
        _stopping = false;
        _fadeTick = 0;
        ApplyGains();
    }

    public void StopMusic()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_music is null)
            return;
        if (_incoming is { } incoming)
        {
            _adapter.Destroy(incoming);
            _incoming = null;
            _incomingKey = null;
        }
        _stopping = true;
        _fadeTick = 0;
        ApplyGains();
    }

    public bool PlaySfx(DecodedPcmWave wave)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(wave);
        if (!_adapter.Available)
        {
            CaptureWarning();
            return false;
        }

        int completed = _sfx.FindIndex(_adapter.IsComplete);
        AudioVoice voice;
        if (completed >= 0)
        {
            voice = _adapter.PlaySfx(wave, _sfx[completed]);
            _sfx[completed] = voice;
        }
        else if (_sfx.Count < MaxSfxVoices)
        {
            voice = _adapter.PlaySfx(wave);
            _sfx.Add(voice);
        }
        else
        {
            DroppedSfxRequests++;
            return false;
        }
        _adapter.SetGain(voice, SfxGain);
        return true;
    }

    public void Tick()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _adapter.Tick();
        if (!_adapter.Available)
        {
            CaptureWarning();
            return;
        }

        if (_music is not null && (_incoming is not null || _stopping))
        {
            _fadeTick++;
            ApplyGains();
            if (_fadeTick == CrossfadeTicks)
            {
                _adapter.Destroy(_music.Value);
                _music = _incoming;
                _musicKey = _incomingKey;
                _incoming = null;
                _incomingKey = null;
                _stopping = false;
                _fadeTick = 0;
                ApplyGains();
            }
        }
        else
            ApplyGains();
    }

    public AudioMixerDiagnostics Diagnostics() => new(
        MusicKey,
        _sfx.Count(voice => !_adapter.IsComplete(voice)),
        DroppedSfxRequests,
        Warning);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (AudioVoice voice in _sfx)
            _adapter.Destroy(voice);
        if (_incoming is { } incoming)
            _adapter.Destroy(incoming);
        if (_music is { } music)
            _adapter.Destroy(music);
        _sfx.Clear();
        _adapter.Dispose();
    }

    private float MusicGain => BusGain(_musicVolume);
    private float SfxGain => BusGain(_sfxVolume);

    private float BusGain(int bus) => _muted ? 0 : _masterVolume / 100f * (bus / 100f);

    private void ApplyGains()
    {
        if (_disposed || !_adapter.Available)
            return;
        float musicGain = MusicGain;
        if (_music is { } music)
        {
            float factor = _incoming is not null || _stopping
                ? (CrossfadeTicks - _fadeTick) / (float)CrossfadeTicks
                : 1;
            _adapter.SetGain(music, musicGain * factor);
        }
        if (_incoming is { } incoming)
            _adapter.SetGain(incoming, musicGain * (_fadeTick / (float)CrossfadeTicks));
        foreach (AudioVoice voice in _sfx)
            if (!_adapter.IsComplete(voice))
                _adapter.SetGain(voice, SfxGain);
    }

    private void CaptureWarning()
    {
        if (_warned || string.IsNullOrWhiteSpace(_adapter.Warning))
            return;
        _warned = true;
        _warning = _adapter.Warning;
    }

    private static int Volume(int value) => value is >= 0 and <= 100
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value), "Volume must be in [0,100].");
}

public sealed class NoAudioAdapter : IAudioAdapter
{
    public NoAudioAdapter(string warning) => Warning = string.IsNullOrWhiteSpace(warning)
        ? "Audio device is unavailable; continuing without audio."
        : warning;

    public bool Available => false;
    public string Warning { get; }
    public AudioVoice CreateMusic(DecodedPcmWave wave, int queuedBuffers) => default;
    public AudioVoice PlaySfx(DecodedPcmWave wave, AudioVoice? reuse = null) => default;
    public void SetGain(AudioVoice voice, float gain) { }
    public bool IsComplete(AudioVoice voice) => true;
    public void Destroy(AudioVoice voice) { }
    public void Tick() { }
    public void Dispose() { }
}

public sealed class RuntimeAudioSystem : IDisposable
{
    private readonly IAssetSource _assets;
    private readonly AudioMixer _mixer;
    private string? _requestedMusic;
    private string? _warning;

    public RuntimeAudioSystem(IAssetSource assets, IAudioAdapter adapter, int musicVolume, int sfxVolume)
    {
        _assets = assets;
        _mixer = new AudioMixer(adapter) { MusicVolume = musicVolume, SfxVolume = sfxVolume };
    }

    public AudioMixerDiagnostics Diagnostics => _mixer.Diagnostics() with { Warning = Warning };
    public string? Warning => _warning ?? _mixer.Warning;

    public void SyncMusic(string? key)
    {
        if (string.Equals(key, _requestedMusic, StringComparison.Ordinal))
            return;
        _requestedMusic = key;
        if (string.IsNullOrWhiteSpace(key))
        {
            _mixer.StopMusic();
            return;
        }
        if (!_assets.TryRead(key, out byte[] bytes))
        {
            _warning ??= $"Optional audio '{key}' is missing; continuing without it.";
            _mixer.StopMusic();
            return;
        }
        _mixer.RequestMusic(key, PcmWaveDecoder.Decode(bytes));
    }

    public bool PlaySfx(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || !_assets.TryRead(key, out byte[] bytes))
        {
            if (!string.IsNullOrWhiteSpace(key))
                _warning ??= $"Optional audio '{key}' is missing; continuing without it.";
            return false;
        }
        return _mixer.PlaySfx(PcmWaveDecoder.Decode(bytes));
    }

    public void Tick() => _mixer.Tick();
    public void Dispose() => _mixer.Dispose();
}
