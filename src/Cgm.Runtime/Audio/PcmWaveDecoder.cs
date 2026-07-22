using System.Buffers.Binary;
using System.Text;

namespace Cgm.Runtime.Audio;

public sealed record DecodedPcmWave(int Channels, int SampleRate, byte[] Pcm16)
{
    public int FrameCount => Pcm16.Length / (Channels * sizeof(short));
}

public static class PcmWaveDecoder
{
    private const string ConversionHint =
        "Convert the file to RIFF/WAVE signed 16-bit PCM, mono or stereo, at 44100 or 48000 Hz.";

    public static DecodedPcmWave Decode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return Decode(bytes.ToArray());
    }

    public static DecodedPcmWave Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12 || !bytes[..4].SequenceEqual("RIFF"u8)
            || !bytes.Slice(8, 4).SequenceEqual("WAVE"u8))
            throw Invalid("Missing RIFF/WAVE header.");

        uint riffSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
        if ((ulong)riffSize + 8 != (ulong)bytes.Length)
            throw Invalid("RIFF length is truncated or has unindexed trailing bytes.");

        WaveFormat? format = null;
        byte[]? pcm = null;
        int offset = 12;
        while (offset < bytes.Length)
        {
            if (bytes.Length - offset < 8)
                throw Invalid("A chunk header is truncated.");
            string id = Encoding.ASCII.GetString(bytes.Slice(offset, 4));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + 4, 4));
            offset += 8;
            if (size > int.MaxValue || (ulong)offset + size > (ulong)bytes.Length)
                throw Invalid($"The '{id}' chunk is truncated.");
            ReadOnlySpan<byte> payload = bytes.Slice(offset, (int)size);

            if (id == "fmt ")
            {
                if (format is not null)
                    throw Invalid("The file contains multiple format chunks.");
                format = ReadFormat(payload);
            }
            else if (id == "data")
            {
                if (pcm is not null)
                    throw Invalid("The file contains multiple data chunks.");
                pcm = payload.ToArray();
            }

            offset += (int)size;
            if ((size & 1) != 0)
            {
                if (offset >= bytes.Length)
                    throw Invalid($"The '{id}' chunk is missing its pad byte.");
                offset++;
            }
        }

        if (format is null)
            throw Invalid("The format chunk is missing.");
        if (pcm is null || pcm.Length == 0)
            throw Invalid("The PCM data chunk is missing or empty.");
        if (pcm.Length % format.BlockAlign != 0)
            throw Invalid("PCM data ends in a partial sample frame.");
        return new DecodedPcmWave(format.Channels, format.SampleRate, pcm);
    }

    private static WaveFormat ReadFormat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16)
            throw Invalid("The format chunk is truncated.");
        ushort encoding = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        ushort channels = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(2, 2));
        int sampleRate = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4, 4));
        int byteRate = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8, 4));
        ushort blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(12, 2));
        ushort bits = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(14, 2));
        if (encoding != 1)
            throw Invalid("Compressed or floating-point WAVE encoding is unsupported.");
        if (channels is not (1 or 2))
            throw Invalid("Channel count must be mono or stereo.");
        if (sampleRate is not (44100 or 48000))
            throw Invalid("Sample rate must be 44100 or 48000 Hz.");
        if (bits != 16)
            throw Invalid("Samples must be signed 16-bit PCM.");
        int expectedAlign = channels * sizeof(short);
        if (blockAlign != expectedAlign || byteRate != sampleRate * expectedAlign)
            throw Invalid("Format byte rate or block alignment is inconsistent.");
        return new WaveFormat(channels, sampleRate, blockAlign);
    }

    private static InvalidDataException Invalid(string reason) =>
        new($"{reason} {ConversionHint}");

    private sealed record WaveFormat(int Channels, int SampleRate, int BlockAlign);
}
