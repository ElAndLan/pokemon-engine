using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cgm.Core.Model;

namespace Cgm.Core.Serialization;

/// <summary>One entry in the pack's section index (EXPORT_PIPELINE_SPEC).</summary>
public sealed record PackSection(string Type, int Offset, int Length, string Codec);

/// <summary>The pack header manifest — verified before any content is touched.</summary>
public sealed record PackManifest(
    int PackFormatVersion,
    string RequiredRuntimeVersion,
    string GameName,
    string BuildTimestamp,
    string ContentHash,
    IReadOnlyList<PackSection> Sections);

/// <summary>Export-time options for the pack header.</summary>
public sealed record PackOptions(
    string GameName = "",
    string RequiredRuntimeVersion = "1.0.0",
    DateTime? BuildTimestampUtc = null);

/// <summary>
/// Reads and writes the <c>.cgmpack</c> container (EXPORT_PIPELINE_SPEC). The pack is just a
/// compressed, hash-verified carrier for a <see cref="GameDb"/>; <see cref="Read"/> reconstructs
/// the exact same GameDb <see cref="GameDb.FromProject"/> would from the raw folder (ADR-006).
/// </summary>
public static class CgmPack
{
    public const int FormatVersion = 1;
    private static readonly byte[] Magic = "CGMP"u8.ToArray();

    private sealed record PackData(string Settings, IReadOnlyList<PackEntity> Entities);
    private sealed record PackEntity(string Category, string Json);

    public static void Write(GameDb db, Stream output, PackOptions? options = null)
    {
        options ??= new PackOptions();
        byte[] payload = BuildDataPayload(db);
        string hash = Hash(payload);
        byte[] blob = Deflate(payload);

        var manifest = new PackManifest(
            FormatVersion,
            options.RequiredRuntimeVersion,
            options.GameName,
            (options.BuildTimestampUtc ?? DateTime.UtcNow).ToString("o"),
            hash,
            [new PackSection("data", 0, blob.Length, "deflate")]);
        byte[] manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, CgmJson.Options));

        using var w = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        w.Write(Magic);
        w.Write(FormatVersion);
        w.Write(manifestBytes.Length);
        w.Write(manifestBytes);
        w.Write(blob);
    }

    /// <summary>Reads just the manifest (for the runtime's version gate) without decoding content.</summary>
    public static PackManifest ReadManifest(Stream input) => ReadHeader(input).Manifest;

    public static GameDb Read(Stream input)
    {
        (PackManifest manifest, byte[] blobRegion) = ReadHeader(input);

        // Decompress every section, in index order, into one payload — the hash covers exactly this.
        byte[]? dataPayload = null;
        using var all = new MemoryStream();
        foreach (PackSection s in manifest.Sections)
        {
            if (s.Offset < 0 || s.Length < 0 || s.Offset + s.Length > blobRegion.Length)
                throw new InvalidDataException($"Section '{s.Type}' range is outside the blob region.");

            byte[] raw = s.Codec switch
            {
                "deflate" => Inflate(blobRegion, s.Offset, s.Length),
                _ => throw new InvalidDataException($"Unknown section codec '{s.Codec}'."),
            };
            all.Write(raw);
            if (s.Type == "data")
                dataPayload = raw;
        }

        if (Hash(all.ToArray()) != manifest.ContentHash)
            throw new InvalidDataException("Pack content hash mismatch — the file is corrupt or tampered.");
        if (dataPayload is null)
            throw new InvalidDataException("Pack has no 'data' section.");

        return DecodeData(dataPayload);
    }

    private static (PackManifest Manifest, byte[] BlobRegion) ReadHeader(Stream input)
    {
        using var r = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);

        byte[] magic = r.ReadBytes(4);
        if (!magic.AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException("Not a .cgmpack file (bad magic).");

        int version = r.ReadInt32();
        if (version != FormatVersion)
            throw new InvalidDataException($"Unsupported pack format version {version}; this loader reads {FormatVersion}.");

        int manifestLen = r.ReadInt32();
        if (manifestLen < 0)
            throw new InvalidDataException("Corrupt pack: negative manifest length.");
        byte[] manifestBytes = r.ReadBytes(manifestLen);
        if (manifestBytes.Length != manifestLen)
            throw new InvalidDataException("Corrupt pack: manifest truncated.");

        PackManifest manifest = JsonSerializer.Deserialize<PackManifest>(
            Encoding.UTF8.GetString(manifestBytes), CgmJson.Options)
            ?? throw new InvalidDataException("Corrupt pack: manifest did not parse.");

        using var blob = new MemoryStream();
        input.CopyTo(blob);
        return (manifest, blob.ToArray());
    }

    private static byte[] BuildDataPayload(GameDb db)
    {
        var entities = new List<PackEntity>(db.Entities.Count);
        foreach (IEntity e in db.Entities)
            entities.Add(new PackEntity(e.Id.Category.ToString().ToLowerInvariant(), CgmJson.SerializeEntity(e)));

        var data = new PackData(CgmJson.Serialize(db.Settings), entities);
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, CgmJson.Options));
    }

    private static GameDb DecodeData(byte[] payload)
    {
        PackData data = JsonSerializer.Deserialize<PackData>(Encoding.UTF8.GetString(payload), CgmJson.Options)
            ?? throw new InvalidDataException("Corrupt pack: data section did not parse.");

        ProjectSettings settings = CgmJson.DeserializeVersioned<ProjectSettings>(data.Settings);
        var entities = new List<IEntity>(data.Entities.Count);
        foreach (PackEntity pe in data.Entities)
        {
            if (!Enum.TryParse(pe.Category, ignoreCase: true, out EntityCategory category))
                throw new InvalidDataException($"Pack data has unknown entity category '{pe.Category}'.");
            entities.Add((IEntity)CgmJson.DeserializeVersioned(pe.Json, EntityRegistry.TypeFor(category)));
        }
        return new GameDb(settings, entities);
    }

    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static byte[] Deflate(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            ds.Write(data, 0, data.Length);
        return ms.ToArray();
    }

    private static byte[] Inflate(byte[] source, int offset, int length)
    {
        using var input = new MemoryStream(source, offset, length);
        using var ds = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        ds.CopyTo(output);
        return output.ToArray();
    }
}
