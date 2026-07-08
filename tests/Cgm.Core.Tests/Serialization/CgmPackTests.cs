using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Core.Tests.Serialization;

public sealed class CgmPackTests
{
    private static GameDb FixtureDb() => GameDb.FromProject(ProjectLoader.Load(TestPaths.Sample("fixture-min")));

    private static byte[] Pack(GameDb db, PackOptions? options = null)
    {
        using var ms = new MemoryStream();
        CgmPack.Write(db, ms, options);
        return ms.ToArray();
    }

    private static GameDb Unpack(byte[] bytes) => CgmPack.Read(new MemoryStream(bytes));

    /// <summary>Canonical serialization used to compare two GameDbs by value.</summary>
    private static string Canonical(GameDb db)
    {
        var parts = new List<string> { CgmJson.Serialize(db.Settings) };
        parts.AddRange(db.Entities.Select(CgmJson.SerializeEntity));
        return string.Join("\n", parts);
    }

    // The critical unity test (ADR-006): a pack round-trips to the *same* GameDb as the raw folder.
    [Fact]
    public void PackRoundTrip_EqualsRawFolderGameDb()
    {
        GameDb fromFolder = FixtureDb();
        GameDb fromPack = Unpack(Pack(fromFolder));

        Assert.Equal(Canonical(fromFolder), Canonical(fromPack));
        Assert.Equal(fromFolder.Entities.Count, fromPack.Entities.Count);
        Assert.Equal(fromFolder.Settings.Name, fromPack.Settings.Name);
    }

    [Fact]
    public void PackRoundTrip_PreservesTypedLookups()
    {
        GameDb db = Unpack(Pack(FixtureDb()));
        Species leafcub = db.Find<Species>(EntityId.Parse("species:leafcub"))!;
        Assert.NotNull(leafcub);
        Assert.Equal(45, leafcub.BaseStats.Hp);
        Assert.Equal(2, db.All<TypeDef>().Count());
    }

    [Fact]
    public void Manifest_CarriesExportMetadata()
    {
        byte[] bytes = Pack(FixtureDb(),
            new PackOptions(GameName: "My Game", RequiredRuntimeVersion: "2.3.4",
                BuildTimestampUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        PackManifest m = CgmPack.ReadManifest(new MemoryStream(bytes));
        Assert.Equal(CgmPack.FormatVersion, m.PackFormatVersion);
        Assert.Equal("My Game", m.GameName);
        Assert.Equal("2.3.4", m.RequiredRuntimeVersion);
        Assert.Single(m.Sections);
        Assert.Equal("deflate", m.Sections[0].Codec);
        Assert.NotEmpty(m.ContentHash);
    }

    [Fact]
    public void ContentHash_IsTimestampIndependent()
    {
        GameDb db = FixtureDb();
        PackManifest a = CgmPack.ReadManifest(new MemoryStream(Pack(db,
            new PackOptions(BuildTimestampUtc: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)))));
        PackManifest b = CgmPack.ReadManifest(new MemoryStream(Pack(db,
            new PackOptions(BuildTimestampUtc: new DateTime(2030, 6, 6, 0, 0, 0, DateTimeKind.Utc)))));
        Assert.Equal(a.ContentHash, b.ContentHash); // same data → same hash regardless of build time
    }

    [Fact]
    public void Read_FlagsTruncatedBlob()
    {
        byte[] bytes = Pack(FixtureDb());
        byte[] truncated = bytes[..^8]; // lop off blob bytes → section no longer fits / stream incomplete

        Assert.Throws<InvalidDataException>(() => Unpack(truncated));
    }

    [Fact]
    public void Read_FlagsTamperedManifestHash()
    {
        // Corrupt within the manifest's contentHash hex (well before the blob) so decompression
        // still succeeds and the hash comparison is what rejects it.
        byte[] bytes = Pack(FixtureDb());
        int hashByte = FindContentHashDigitByte(bytes);
        bytes[hashByte] = bytes[hashByte] == (byte)'a' ? (byte)'b' : (byte)'a';

        InvalidDataException ex = Assert.Throws<InvalidDataException>(() => Unpack(bytes));
        Assert.Contains("hash", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Locate a hex digit inside the manifest's "contentHash":"..." value and return its byte index.
    private static int FindContentHashDigitByte(byte[] bytes)
    {
        string text = System.Text.Encoding.UTF8.GetString(bytes);
        int key = text.IndexOf("contentHash", StringComparison.Ordinal);
        int colon = text.IndexOf(':', key);
        int firstQuote = text.IndexOf('"', colon);
        return firstQuote + 3; // a couple chars into the hex string (ASCII → 1 byte per char here)
    }

    [Fact]
    public void Read_RefusesWrongFormatVersion()
    {
        byte[] bytes = Pack(FixtureDb());
        // Version int32 sits right after the 4-byte magic; bump it to an unsupported value.
        BitConverter.GetBytes(CgmPack.FormatVersion + 99).CopyTo(bytes, 4);

        Assert.Throws<InvalidDataException>(() => Unpack(bytes));
    }

    [Fact]
    public void Read_RefusesBadMagic()
    {
        Assert.Throws<InvalidDataException>(() => Unpack([0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07]));
    }

    [Fact]
    public void FromProject_OrdersEntitiesDeterministically()
    {
        Project p = ProjectLoader.Load(TestPaths.Sample("fixture-min"));
        var a = GameDb.FromProject(p).Entities.Select(e => e.Id.ToString()).ToList();
        var b = GameDb.FromProject(p).Entities.Select(e => e.Id.ToString()).ToList();
        Assert.Equal(a, b);
        Assert.Equal(a.OrderBy(s => s, StringComparer.Ordinal), a); // actually sorted
    }
}
