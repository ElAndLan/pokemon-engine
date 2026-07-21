using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

/// <summary>Authored asset paths become filesystem reads and pack section names, so the rules that
/// decide what is legal are a trust boundary (DATA_SCHEMA.md §4.6).</summary>
public sealed class AssetPathTests
{
    [Theory]
    [InlineData("assets/x.png", "assets/x.png")]
    [InlineData(@"assets\x.png", "assets/x.png")]            // host separators canonicalize
    [InlineData("./assets/x.png", "assets/x.png")]
    [InlineData("assets//x.png", "assets/x.png")]            // empty segments collapse
    [InlineData("  assets/x.png  ", "assets/x.png")]
    [InlineData("a/./b/./c.png", "a/b/c.png")]
    [InlineData("x.png", "x.png")]
    public void LegalPathsCanonicalize(string input, string expected) =>
        Assert.Equal(expected, AssetPath.Normalize(input));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("..")]
    [InlineData("../x.png")]
    [InlineData("assets/../../etc/passwd")]                  // traversal anywhere, not just in front
    [InlineData(@"assets\..\..\x.png")]
    [InlineData("/etc/passwd")]
    [InlineData(@"\\server\share\x.png")]
    [InlineData(@"C:\Windows\System32\x.png")]
    [InlineData("./")]                                       // canonicalizes to nothing
    public void UnsafeOrEmptyPathsAreRefused(string? input) =>
        Assert.Equal("", AssetPath.Normalize(input));

    /// <summary>A path that merely starts with ".." as text is a normal name, not traversal.</summary>
    [Fact]
    public void ADotPrefixedNameIsNotTraversal() =>
        Assert.Equal("..hidden/x.png", AssetPath.Normalize("..hidden/x.png"));

    [Fact]
    public void ResolveCombinesAgainstTheRoot() =>
        Assert.Equal(Path.Combine("root", "assets", "x.png"), AssetPath.Resolve("root", "assets/x.png"));

    [Fact]
    public void ResolveRefusesAnUnsafePath() =>
        Assert.Null(AssetPath.Resolve("root", "../x.png"));

    /// <summary>A rootless project has nowhere to read from; resolving must fail rather than land on
    /// the process working directory.</summary>
    [Fact]
    public void ResolveRefusesAnEmptyRoot() =>
        Assert.Null(AssetPath.Resolve("", "assets/x.png"));

    [Fact]
    public void ResolveRejectsANullRoot() =>
        Assert.Throws<ArgumentNullException>(() => AssetPath.Resolve(null!, "x.png"));
}
