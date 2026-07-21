using System.IO.Compression;

namespace Cgm.Runtime.Tests;

/// <summary>
/// A minimal PNG encoder for test fixtures: 8-bit RGBA, no interlacing, filter 0. Tests that assert
/// decoding must feed the decoder genuine bytes — a hand-mocked "image" would prove nothing about
/// the adapter. Encoding is test-only; the product never writes images.
/// </summary>
public static class TestPng
{
    /// <summary>Encodes straight (non-premultiplied) RGBA, which is what a real authored PNG holds.</summary>
    public static byte[] Encode(int width, int height, byte[] rgba)
    {
        if (rgba.Length != width * height * 4)
            throw new ArgumentException($"Expected {width * height * 4} bytes, got {rgba.Length}.", nameof(rgba));

        using var raw = new MemoryStream();
        for (int y = 0; y < height; y++)
        {
            raw.WriteByte(0);                                     // filter type: none
            raw.Write(rgba, y * width * 4, width * 4);
        }

        using var png = new MemoryStream();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(png, "IHDR",
        [
            .. BigEndian(width), .. BigEndian(height),
            8,      // bit depth
            6,      // colour type: RGBA
            0, 0, 0 // compression, filter, interlace
        ]);
        Chunk(png, "IDAT", Deflate(raw.ToArray()));
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    /// <summary>A solid block of one colour, the usual fixture.</summary>
    public static byte[] Solid(int width, int height, byte r, byte g, byte b, byte a = 255)
    {
        var rgba = new byte[width * height * 4];
        for (int i = 0; i < rgba.Length; i += 4)
            (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) = (r, g, b, a);
        return Encode(width, height, rgba);
    }

    private static void Chunk(Stream output, string tag, byte[] data)
    {
        byte[] name = [.. tag.Select(c => (byte)c)];
        output.Write(BigEndian(data.Length));
        output.Write(name);
        output.Write(data);
        output.Write(BigEndian(unchecked((int)Crc32([.. name, .. data]))));
    }

    /// <summary>PNG stores IDAT as zlib, not bare deflate.</summary>
    private static byte[] Deflate(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var zlib = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(data);
        return ms.ToArray();
    }

    private static byte[] BigEndian(int value) =>
        [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ (0xEDB88320 & (uint)-(crc & 1));
        }
        return crc ^ 0xFFFFFFFF;
    }
}
