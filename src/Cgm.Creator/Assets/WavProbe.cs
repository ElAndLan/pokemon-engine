using System.Buffers.Binary;

namespace Cgm.Creator.Assets;

/// <summary>
/// Import-time gate for audio files (ASSET_PIPELINE_SPEC 17B): verifies the RIFF/WAVE container
/// shape so obviously wrong files never enter the project. Deliberately NOT a decoder — the
/// Runtime's PcmWaveDecoder is the one authority on playable PCM, per the no-second-decoder rule;
/// a container-valid file with an unplayable format surfaces there with a conversion hint.
/// </summary>
public static class WavProbe
{
    public static bool LooksLikeWave(ReadOnlySpan<byte> bytes, out string? error)
    {
        if (bytes.Length < 12 || !bytes[..4].SequenceEqual("RIFF"u8)
            || !bytes.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            error = "Missing RIFF/WAVE header — not a .wav file.";
            return false;
        }
        if ((ulong)BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4)) + 8 != (ulong)bytes.Length)
        {
            error = "RIFF length does not match the file — the file is truncated or corrupt.";
            return false;
        }
        error = null;
        return true;
    }
}
