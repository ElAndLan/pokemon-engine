using System.Buffers.Binary;
using System.Text;

namespace Cgm.Creator.Tests.Assets;

/// <summary>Minimal valid RIFF/WAVE PCM16 bytes for import tests.</summary>
internal static class TinyWav
{
    public static byte[] Pcm16Mono(int frames = 8, int sampleRate = 44100)
    {
        byte[] fmt = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(0), 1);   // PCM
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(2), 1);   // mono
        BinaryPrimitives.WriteUInt32LittleEndian(fmt.AsSpan(4), (uint)sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(fmt.AsSpan(8), (uint)(sampleRate * 2));
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(12), 2);  // block align
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(14), 16); // bits

        byte[] data = new byte[frames * 2]; // silence

        using var ms = new MemoryStream();
        void Chunk(string id, byte[] payload)
        {
            ms.Write(Encoding.ASCII.GetBytes(id));
            Span<byte> len = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(len, (uint)payload.Length);
            ms.Write(len);
            ms.Write(payload);
        }

        ms.Write("RIFF"u8);
        Span<byte> riffLen = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(riffLen, (uint)(4 + 8 + fmt.Length + 8 + data.Length));
        ms.Write(riffLen);
        ms.Write("WAVE"u8);
        Chunk("fmt ", fmt);
        Chunk("data", data);
        return ms.ToArray();
    }
}
