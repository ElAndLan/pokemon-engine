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

/// <summary>Everything a pack carries: the rules database and the asset bytes it references,
/// keyed by the project-relative path the owning <see cref="SpriteSheet.Asset"/> names.</summary>
public sealed record PackContent(GameDb Db, IReadOnlyDictionary<string, byte[]> Assets)
{
    public static PackContent Empty(GameDb db) => new(db, new Dictionary<string, byte[]>());
}

/// <summary>
/// Reads and writes the <c>.cgmpack</c> container (EXPORT_PIPELINE_SPEC). The pack is just a
/// compressed, hash-verified carrier for a <see cref="GameDb"/>; <see cref="Read"/> reconstructs
/// the exact same GameDb <see cref="GameDb.FromProject"/> would from the raw folder (ADR-006).
/// </summary>
public static class CgmPack
{
    public const int FormatVersion = 2;

    /// <summary>Format 1 packs carry only a data section. They still read, so a pack built before
    /// assets existed does not become unreadable.</summary>
    public const int MinReadableFormatVersion = 1;

    /// <summary>Section-type prefix for an embedded asset; the remainder is its project-relative
    /// path with forward slashes.</summary>
    public const string AssetPrefix = "asset:";

    private static readonly byte[] Magic = "CGMP"u8.ToArray();

    private sealed record PackData(string Settings, IReadOnlyList<PackEntity> Entities);
    private sealed record PackEntity(string Category, string Json);

    public static void Write(GameDb db, Stream output, PackOptions? options = null,
        IReadOnlyDictionary<string, byte[]>? assets = null)
    {
        options ??= new PackOptions();
        byte[] payload = BuildDataPayload(db);
        byte[] dataBlob = Deflate(payload);

        // Two streams: the blob is what gets stored, `hashed` is the decoded concatenation in
        // section-index order — exactly what Read rebuilds and checks against ContentHash.
        var sections = new List<PackSection> { new("data", 0, dataBlob.Length, "deflate") };
        using var blob = new MemoryStream();
        using var hashed = new MemoryStream();
        blob.Write(dataBlob);
        hashed.Write(payload);

        // PNGs are already deflate-compressed internally; storing them avoids a second pass that
        // costs time and saves nothing. Ordered so the same inputs always produce the same bytes.
        foreach ((string path, byte[] bytes) in NormalizeAssets(assets))
        {
            sections.Add(new PackSection(AssetPrefix + path, (int)blob.Length, bytes.Length, "stored"));
            blob.Write(bytes);
            hashed.Write(bytes);
        }

        string hash = Hash(hashed.ToArray());
        byte[] blobBytes = blob.ToArray();

        var manifest = new PackManifest(
            FormatVersion,
            options.RequiredRuntimeVersion,
            options.GameName,
            (options.BuildTimestampUtc ?? DateTime.UtcNow).ToString("o"),
            hash,
            sections);
        byte[] manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, CgmJson.Options));

        using var w = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        w.Write(Magic);
        w.Write(FormatVersion);
        w.Write(manifestBytes.Length);
        w.Write(manifestBytes);
        w.Write(blobBytes);
    }

    /// <summary>Canonicalizes asset keys to forward slashes and orders them, so the same project
    /// exports byte-identical packs regardless of dictionary iteration order or host separators.</summary>
    private static IEnumerable<KeyValuePair<string, byte[]>> NormalizeAssets(
        IReadOnlyDictionary<string, byte[]>? assets)
    {
        if (assets is null)
            yield break;

        var seen = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach ((string path, byte[] bytes) in assets)
        {
            string key = AssetPath.Normalize(path);
            if (key.Length == 0)
                throw new ArgumentException("An asset was supplied with an empty path.", nameof(assets));
            if (!seen.TryAdd(key, bytes))
                throw new ArgumentException($"Asset path '{key}' was supplied twice.", nameof(assets));
        }

        foreach (string key in seen.Keys.Order(StringComparer.Ordinal))
            yield return new KeyValuePair<string, byte[]>(key, seen[key]);
    }

    /// <summary>Reads just the manifest (for the runtime's version gate) without decoding content.</summary>
    public static PackManifest ReadManifest(Stream input) => ReadHeader(input).Manifest;

    public static PackContent Read(Stream input)
    {
        (PackManifest manifest, byte[] blobRegion) = ReadHeader(input);

        // Decode every section, in index order, into one payload — the hash covers exactly this.
        byte[]? dataPayload = null;
        var assets = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var all = new MemoryStream();
        foreach (PackSection s in manifest.Sections)
        {
            if (s.Offset < 0 || s.Length < 0 || s.Offset + s.Length > blobRegion.Length)
                throw new InvalidDataException($"Section '{s.Type}' range is outside the blob region.");

            byte[] raw = s.Codec switch
            {
                "deflate" => Inflate(blobRegion, s.Offset, s.Length),
                "stored" => blobRegion.AsSpan(s.Offset, s.Length).ToArray(),
                _ => throw new InvalidDataException($"Unknown section codec '{s.Codec}'."),
            };
            all.Write(raw);

            if (s.Type == "data")
                dataPayload = raw;
            else if (s.Type.StartsWith(AssetPrefix, StringComparison.Ordinal))
            {
                // A pack is untrusted input: re-canonicalize rather than trusting the stored key,
                // or a crafted pack could write outside a cache directory on extraction.
                string path = AssetPath.Normalize(s.Type[AssetPrefix.Length..]);
                if (path.Length == 0)
                    throw new InvalidDataException($"Pack asset section '{s.Type}' has an unsafe path.");
                if (!assets.TryAdd(path, raw))
                    throw new InvalidDataException($"Pack contains asset '{path}' twice.");
            }
        }

        if (Hash(all.ToArray()) != manifest.ContentHash)
            throw new InvalidDataException("Pack content hash mismatch — the file is corrupt or tampered.");
        if (dataPayload is null)
            throw new InvalidDataException("Pack has no 'data' section.");

        return new PackContent(DecodeData(dataPayload), assets);
    }

    private static (PackManifest Manifest, byte[] BlobRegion) ReadHeader(Stream input)
    {
        using var r = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);

        byte[] magic = r.ReadBytes(4);
        if (!magic.AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException("Not a .cgmpack file (bad magic).");

        int version = r.ReadInt32();
        if (version is < MinReadableFormatVersion or > FormatVersion)
            throw new InvalidDataException(
                $"Unsupported pack format version {version}; this loader reads {MinReadableFormatVersion}-{FormatVersion}.");

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
