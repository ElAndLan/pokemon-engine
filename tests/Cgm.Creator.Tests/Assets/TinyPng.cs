using System.IO.Compression;
using System.Text;

namespace Cgm.Creator.Tests.Assets;

/// <summary>Minimal 8-bit RGBA PNG encoder for tests — real bytes so StbImageSharp can decode them.</summary>
internal static class TinyPng
{
    public static byte[] Encode(int w, int h, byte[] rgba)
    {
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]); // signature

        var ihdr = new byte[13];
        WriteBE(ihdr, 0, w);
        WriteBE(ihdr, 4, h);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // color type: RGBA
        WriteChunk(ms, "IHDR", ihdr);

        // Raw scanlines with a leading filter byte (0 = none).
        int stride = w * 4;
        var raw = new byte[h * (1 + stride)];
        for (int y = 0; y < h; y++)
            Array.Copy(rgba, y * stride, raw, y * (1 + stride) + 1, stride);

        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(raw);
        WriteChunk(ms, "IDAT", compressed.ToArray());

        WriteChunk(ms, "IEND", []);
        return ms.ToArray();
    }

    /// <summary>A fully-opaque solid image (all pixels white, alpha 255).</summary>
    public static byte[] Solid(int w, int h)
    {
        var rgba = new byte[w * h * 4];
        Array.Fill(rgba, (byte)255);
        return Encode(w, h, rgba);
    }

    private static void WriteBE(byte[] b, int o, int v)
    {
        b[o] = (byte)(v >> 24);
        b[o + 1] = (byte)(v >> 16);
        b[o + 2] = (byte)(v >> 8);
        b[o + 3] = (byte)v;
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        WriteBE(len, 0, data.Length);
        s.Write(len);

        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);

        var crc = new byte[4];
        WriteBE(crc, 0, (int)Crc32(typeBytes, data));
        s.Write(crc);
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        crc = Update(crc, type);
        crc = Update(crc, data);
        return crc ^ 0xFFFFFFFF;
    }

    private static uint Update(uint crc, byte[] buf)
    {
        foreach (byte b in buf)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return crc;
    }
}
