using System.Text;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>The 16A asset seam: raw-folder and packed sources answer the same questions the same
/// way, so a scene cannot tell which mode it is running in (ADR-006).</summary>
public sealed class AssetSourceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("cgm-assets").FullName;

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

    private void Write(string relative, string content)
    {
        string full = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    /// <summary>Both implementations, loaded with identical content, for parity assertions.</summary>
    private IEnumerable<IAssetSource> BothSources(string path, string content)
    {
        Write(path, content);
        yield return new FolderAssetSource(_root);
        yield return new PackAssetSource(new Dictionary<string, byte[]> { [path] = Bytes(content) });
    }

    // --- Parity ----------------------------------------------------------------------

    [Fact]
    public void BothSourcesReturnTheSameBytes()
    {
        foreach (IAssetSource source in BothSources("assets/a.png", "payload"))
        {
            Assert.True(source.TryRead("assets/a.png", out byte[] bytes));
            Assert.Equal(Bytes("payload"), bytes);
        }
    }

    [Fact]
    public void BothSourcesAcceptHostSeparators()
    {
        foreach (IAssetSource source in BothSources("assets/a.png", "payload"))
            Assert.True(source.TryRead(@"assets\a.png", out _));
    }

    [Fact]
    public void BothSourcesReportAMissingAssetWithoutThrowing()
    {
        foreach (IAssetSource source in BothSources("assets/a.png", "payload"))
        {
            Assert.False(source.TryRead("assets/missing.png", out byte[] bytes));
            Assert.Empty(bytes);
        }
    }

    /// <summary>Authored data is untrusted. Neither source may reach outside its content.</summary>
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("assets/../../outside.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    public void NeitherSourceEscapesItsContent(string path)
    {
        foreach (IAssetSource source in BothSources("assets/a.png", "payload"))
            Assert.False(source.TryRead(path, out _));
    }

    // --- Folder specifics -------------------------------------------------------------

    /// <summary>Traversal is refused even when the target genuinely exists, so a passing read can
    /// never be mistaken for proof that containment worked.</summary>
    [Fact]
    public void TraversalToARealFileOutsideTheRootIsRefused()
    {
        string outside = Path.Combine(Path.GetDirectoryName(_root)!, "cgm-outside-secret.txt");
        File.WriteAllText(outside, "secret");
        try
        {
            var source = new FolderAssetSource(_root);
            Assert.False(source.TryRead("../cgm-outside-secret.txt", out _));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void AnEmptyRootReadsNothing() =>
        Assert.False(new FolderAssetSource("").TryRead("assets/a.png", out _));

    [Fact]
    public void ANullRootIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => new FolderAssetSource(null!));

    [Fact]
    public void AFolderIsNotReadableAsAnAsset()
    {
        Write("assets/a.png", "payload");
        Assert.False(new FolderAssetSource(_root).TryRead("assets", out _));
    }

    // --- Pack specifics ---------------------------------------------------------------

    [Fact]
    public void ANullAssetMapIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => new PackAssetSource(null!));

    [Fact]
    public void AnEmptyPackReadsNothing() =>
        Assert.False(new PackAssetSource(new Dictionary<string, byte[]>()).TryRead("a.png", out _));

    [Fact]
    public void PackKeysAreMatchedAfterCanonicalization()
    {
        var source = new PackAssetSource(new Dictionary<string, byte[]> { ["assets/a.png"] = Bytes("x") });
        Assert.True(source.TryRead("./assets/a.png", out _));
    }
}
