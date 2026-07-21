using System.Text;
using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Core.Tests.Serialization;

/// <summary>Asset sections in the pack container (EXPORT_PIPELINE_SPEC). Assets ride the same
/// content hash as the rules data, so a tampered image is caught exactly like tampered data.</summary>
public sealed class PackAssetTests
{
    private static readonly EntityId MapId = EntityId.Parse("map:field");

    private static GameDb Db() => new(
        new ProjectSettings { Name = "T", StartMap = MapId },
        [new Map { Id = MapId, Name = "Field", Width = 2, Height = 2, Layers = new MapLayers { Ground = [0, 0, 0, 0] } }]);

    private static byte[] Pack(IReadOnlyDictionary<string, byte[]>? assets)
    {
        using var ms = new MemoryStream();
        CgmPack.Write(Db(), ms, new PackOptions("T", BuildTimestampUtc: new DateTime(2026, 1, 1)), assets);
        return ms.ToArray();
    }

    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

    // --- Round trip -----------------------------------------------------------------

    [Fact]
    public void AssetsSurviveARoundTrip()
    {
        byte[] pack = Pack(new Dictionary<string, byte[]>
        {
            ["assets/a.png"] = Bytes("first"),
            ["assets/sub/b.png"] = Bytes("second"),
        });

        PackContent content = CgmPack.Read(new MemoryStream(pack));

        Assert.Equal(2, content.Assets.Count);
        Assert.Equal(Bytes("first"), content.Assets["assets/a.png"]);
        Assert.Equal(Bytes("second"), content.Assets["assets/sub/b.png"]);
        Assert.NotNull(content.Db.Find<Map>(MapId));
    }

    [Fact]
    public void APackWithNoAssetsReadsAsEmptyRatherThanNull()
    {
        PackContent content = CgmPack.Read(new MemoryStream(Pack(null)));
        Assert.Empty(content.Assets);
        Assert.NotNull(content.Db.Find<Map>(MapId));
    }

    [Fact]
    public void AnAssetOfZeroBytesRoundTrips()
    {
        byte[] pack = Pack(new Dictionary<string, byte[]> { ["assets/empty.png"] = [] });
        Assert.Empty(CgmPack.Read(new MemoryStream(pack)).Assets["assets/empty.png"]);
    }

    /// <summary>Binary payloads must survive untouched — an image is not text and must not be
    /// re-encoded on the way through.</summary>
    [Fact]
    public void ArbitraryBinaryBytesAreUnchanged()
    {
        byte[] blob = [.. Enumerable.Range(0, 256).Select(i => (byte)i)];
        byte[] pack = Pack(new Dictionary<string, byte[]> { ["assets/raw.bin"] = blob });
        Assert.Equal(blob, CgmPack.Read(new MemoryStream(pack)).Assets["assets/raw.bin"]);
    }

    [Fact]
    public void AssetPathsAreCanonicalizedOnWrite()
    {
        byte[] pack = Pack(new Dictionary<string, byte[]> { [@".\assets\a.png"] = Bytes("x") });
        Assert.Equal(["assets/a.png"], CgmPack.Read(new MemoryStream(pack)).Assets.Keys);
    }

    // --- Manifest -------------------------------------------------------------------

    [Fact]
    public void EachAssetGetsItsOwnStoredSection()
    {
        byte[] pack = Pack(new Dictionary<string, byte[]> { ["assets/a.png"] = Bytes("x") });
        PackManifest manifest = CgmPack.ReadManifest(new MemoryStream(pack));

        PackSection asset = Assert.Single(manifest.Sections, s => s.Type.StartsWith(CgmPack.AssetPrefix));
        Assert.Equal("asset:assets/a.png", asset.Type);
        Assert.Equal("stored", asset.Codec);
        Assert.Equal(1, manifest.Sections.Count(s => s.Type == "data"));
    }

    [Fact]
    public void WritingUsesTheCurrentFormatVersion() =>
        Assert.Equal(CgmPack.FormatVersion, CgmPack.ReadManifest(new MemoryStream(Pack(null))).PackFormatVersion);

    // --- Determinism ----------------------------------------------------------------

    /// <summary>The same content must produce byte-identical packs whatever order the assets arrive
    /// in, or an export is not reproducible.</summary>
    [Fact]
    public void AssetOrderDoesNotChangeTheBytes()
    {
        byte[] forward = Pack(new Dictionary<string, byte[]>
        {
            ["assets/a.png"] = Bytes("one"), ["assets/b.png"] = Bytes("two"), ["assets/c.png"] = Bytes("three"),
        });
        byte[] reverse = Pack(new Dictionary<string, byte[]>
        {
            ["assets/c.png"] = Bytes("three"), ["assets/b.png"] = Bytes("two"), ["assets/a.png"] = Bytes("one"),
        });

        Assert.Equal(forward, reverse);
    }

    // --- Integrity ------------------------------------------------------------------

    /// <summary>The content hash must cover asset bytes, not just the data section.</summary>
    [Fact]
    public void TamperingWithAnAssetIsDetected()
    {
        byte[] pack = Pack(new Dictionary<string, byte[]> { ["assets/a.png"] = Bytes("original") });

        int index = Find(pack, Bytes("original"));
        Assert.True(index >= 0, "test setup: stored asset bytes should appear verbatim in the pack");
        pack[index] = (byte)'X';

        InvalidDataException ex = Assert.Throws<InvalidDataException>(
            () => CgmPack.Read(new MemoryStream(pack)));
        Assert.Contains("hash mismatch", ex.Message);
    }

    [Fact]
    public void ChangingAnAssetChangesTheContentHash()
    {
        string a = CgmPack.ReadManifest(new MemoryStream(
            Pack(new Dictionary<string, byte[]> { ["assets/a.png"] = Bytes("one") }))).ContentHash;
        string b = CgmPack.ReadManifest(new MemoryStream(
            Pack(new Dictionary<string, byte[]> { ["assets/a.png"] = Bytes("two") }))).ContentHash;

        Assert.NotEqual(a, b);
    }

    // --- Rejected input -------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("../escape.png")]
    [InlineData("/absolute.png")]
    public void AnUnsafeAssetPathIsRefusedAtWrite(string path) =>
        Assert.Throws<ArgumentException>(() => Pack(new Dictionary<string, byte[]> { [path] = Bytes("x") }));

    /// <summary>Two keys that canonicalize to one path would silently drop an asset.</summary>
    [Fact]
    public void PathsThatCollideAfterCanonicalizationAreRefused() =>
        Assert.Throws<ArgumentException>(() => Pack(new Dictionary<string, byte[]>
        {
            ["assets/a.png"] = Bytes("one"),
            [@"assets\a.png"] = Bytes("two"),
        }));

    // --- Backward compatibility -----------------------------------------------------

    /// <summary>Format 1 predates assets and carries only a data section. Such a pack must still
    /// load, or every pack built before this change becomes unreadable.</summary>
    [Fact]
    public void AFormatOnePackStillReads()
    {
        byte[] pack = Pack(null);
        pack[4] = 1;                                          // the container's version int
        Downgrade(pack, "packFormatVersion");

        PackContent content = CgmPack.Read(new MemoryStream(pack));
        Assert.Empty(content.Assets);
        Assert.NotNull(content.Db.Find<Map>(MapId));
    }

    [Fact]
    public void AFutureFormatVersionIsRefused()
    {
        byte[] pack = Pack(null);
        pack[4] = (byte)(CgmPack.FormatVersion + 1);

        InvalidDataException ex = Assert.Throws<InvalidDataException>(
            () => CgmPack.Read(new MemoryStream(pack)));
        Assert.Contains("Unsupported pack format version", ex.Message);
    }

    /// <summary>First index of <paramref name="needle"/> in <paramref name="haystack"/>, or -1.</summary>
    private static int Find(byte[] haystack, byte[] needle) =>
        haystack.AsSpan().IndexOf(needle);

    /// <summary>Rewrites a numeric manifest property to 1 in place. Scans to the first digit after
    /// the name rather than matching a literal, so the manifest's JSON spacing does not matter.</summary>
    private static void Downgrade(byte[] pack, string property)
    {
        int at = Find(pack, Bytes('"' + property + '"'));
        Assert.True(at >= 0, $"test setup: manifest should contain '{property}'");

        int digit = at + property.Length + 2;
        while (digit < pack.Length && pack[digit] is < (byte)'0' or > (byte)'9')
            digit++;
        Assert.True(digit < pack.Length, $"test setup: '{property}' should have a numeric value");
        pack[digit] = (byte)'1';
    }
}
